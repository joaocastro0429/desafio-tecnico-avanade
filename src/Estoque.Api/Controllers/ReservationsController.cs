using Estoque.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Estoque.Api.Controllers;

[ApiController, Route("internal/reservas"), Authorize(Policy = "Internal")]
public class ReservationsController(StockService service) : ControllerBase
{
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Reserve(Guid id, ReservationRequest input, CancellationToken ct) =>
        Ok(await service.Reserve(id, input.Itens, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var reservation = await service.GetReservation(id, ct);
        return reservation is null ? NotFound() : Ok(reservation);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        await service.Release(id, ct);
        return NoContent();
    }
}
