using System.Collections.Concurrent;
using System.Reflection;

namespace Interlink;

/// <summary>
/// Internal helper that scans assemblies for concrete implementations of open generic interfaces.
/// Results are cached per assembly + open-generic type pair.
/// </summary>
internal static class TypeScanner
{
    private static readonly ConcurrentDictionary<(Assembly Assembly, Type OpenGenericType), List<(Type ServiceType, Type ImplementationType)>> Cache = new();

    /// <summary>
    /// Scans the given assembly for non-abstract, non-interface types that implement
    /// the specified open generic interface.
    /// Open generic type definitions are skipped (register those via <c>AddBehavior</c> /
    /// <c>AddStreamBehavior</c> / <c>AddNotificationBehavior</c>).
    /// </summary>
    public static IEnumerable<(Type ServiceType, Type ImplementationType)> Scan(Assembly assembly, Type openGenericType)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        if (openGenericType is null) throw new ArgumentNullException(nameof(openGenericType));

        return Cache.GetOrAdd((assembly, openGenericType), key =>
        {
            var results = new List<(Type, Type)>();

            Type[] types;
            try
            {
                types = key.Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }
            catch
            {
                return results;
            }

            foreach (var type in types)
            {
                if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType &&
                        iface.GetGenericTypeDefinition() == key.OpenGenericType)
                    {
                        results.Add((iface, type));
                    }
                }
            }

            return results;
        });
    }
}