using Interlink.Sample.Entities;
using Interlink.Sample.Features;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;

namespace Interlink.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PetController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllPets(CancellationToken cancellationToken)
    {
        var pets = await mediator.Send(new GetAllPets.Query(), cancellationToken);
        return Ok(pets);
    }

    [HttpGet("stream")]
    public async IAsyncEnumerable<Pet> GetAllPetsStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var pet in mediator.CreateStream(new ExportPetsStream.Stream(), cancellationToken))
        {
            yield return pet;
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePet([FromBody] CreatePet.Command command, CancellationToken cancellationToken)
    {
        var pet = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAllPets), new { id = pet.Id }, pet);
    }

    [HttpPut]
    public async Task<IActionResult> UpdatePet([FromBody] UpdatePet.Command command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePet(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePet.Command(id), cancellationToken);
        return NoContent();
    }
}
