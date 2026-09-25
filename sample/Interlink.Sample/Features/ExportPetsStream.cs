using Interlink.Contracts;
using Interlink.Sample.Data;
using Interlink.Sample.Entities;
using System.Runtime.CompilerServices;

namespace Interlink.Sample.Features;

public class ExportPetsStream
{
    public record Stream : IStreamRequest<Pet>;

    public class Handler(AppDbContext context) : IStreamRequestHandler<Stream, Pet>
    {
        public async IAsyncEnumerable<Pet> Handle(Stream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var pet in context.Pets.AsAsyncEnumerable().WithCancellation(cancellationToken))
                yield return pet;
        }
    }
}
