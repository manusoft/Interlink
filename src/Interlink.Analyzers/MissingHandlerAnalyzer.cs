using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Interlink.Analyzers;

/// <summary>
/// Analyzer that reports diagnostics for Interlink request/handler consistency:
/// <list type="bullet">
/// <item><see cref="MissingHandlerId"/> — request with no handler</item>
/// <item><see cref="DuplicateHandlerId"/> — more than one handler for the same request</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingHandlerAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic id for a missing request handler.</summary>
    public const string MissingHandlerId = "ILINK001";

    /// <summary>Diagnostic id for duplicate request handlers.</summary>
    public const string DuplicateHandlerId = "ILINK002";

    private static readonly DiagnosticDescriptor MissingHandlerRule = new DiagnosticDescriptor(
        id: MissingHandlerId,
        title: "Missing request handler",
        messageFormat: "No handler found for request type '{0}'. Implement IRequestHandler<{0}, TResponse>.",
        category: "Interlink",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every type that implements IRequest<TResponse> should have a corresponding IRequestHandler implementation defined in the compilation.",
        helpLinkUri: null,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    private static readonly DiagnosticDescriptor DuplicateHandlerRule = new DiagnosticDescriptor(
        id: DuplicateHandlerId,
        title: "Duplicate request handler",
        messageFormat: "Multiple handlers found for request type '{0}'. Only one IRequestHandler should exist per request type.",
        category: "Interlink",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each IRequest<TResponse> should have exactly one IRequestHandler implementation in the compilation.",
        helpLinkUri: null,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingHandlerRule, DuplicateHandlerRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var compilation = context.Compilation;

        var requestInterface = compilation.GetTypeByMetadataName("Interlink.Contracts.IRequest`1");
        var handlerInterface = compilation.GetTypeByMetadataName("Interlink.IRequestHandler`2");

        if (requestInterface is null || handlerInterface is null)
            return;

        var requestTypes = new List<INamedTypeSymbol>();
        // request type -> list of handler types that handle it
        var handlersByRequest = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var type in GetAllTypes(compilation.GlobalNamespace))
        {
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                continue;

            if (type.IsAbstract)
                continue;

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, requestInterface) &&
                    iface.TypeArguments.Length == 1)
                {
                    requestTypes.Add(type);
                }

                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, handlerInterface) &&
                    iface.TypeArguments.Length == 2 &&
                    iface.TypeArguments[0] is INamedTypeSymbol handledRequest)
                {
                    if (!handlersByRequest.TryGetValue(handledRequest, out var list))
                    {
                        list = new List<INamedTypeSymbol>();
                        handlersByRequest[handledRequest] = list;
                    }

                    list.Add(type);
                }
            }
        }

        var reportedRequests = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var requestType in requestTypes)
        {
            if (!handlersByRequest.TryGetValue(requestType, out var handlers) || handlers.Count == 0)
            {
                if (!reportedRequests.Add(requestType))
                    continue;

                var location = requestType.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        MissingHandlerRule,
                        location,
                        requestType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        foreach (var pair in handlersByRequest)
        {
            if (pair.Value.Count <= 1)
                continue;

            var requestName = pair.Key.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            // Report on each duplicate handler
            foreach (var handlerType in pair.Value)
            {
                var location = handlerType.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DuplicateHandlerRule,
                        location,
                        requestName));
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers())
        {
            foreach (var nested in GetAllTypesRecursive(type))
                yield return nested;
        }

        foreach (var childNs in root.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNs))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypesRecursive(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in GetAllTypesRecursive(nested))
                yield return t;
        }
    }
}
