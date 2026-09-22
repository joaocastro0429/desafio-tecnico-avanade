using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendas.Api.Data;
using Vendas.Api.DTOs;
using Vendas.Api.Services;

namespace Vendas.Api.Controllers;

[ApiController, Route("pedidos"), Authorize(Policy = "User")]
public class OrdersController(SalesDb db, OrderService service) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrder input, CancellationToken ct)
    {
        var order = await service.Create(UserId, input.Itens, ct);
        if (order.Status == "Pendente") return AcceptedAtAction(nameof(Get), new { id = order.Id }, order);
        if (order.Status == "Rejeitado") return Conflict(order);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(
        (await db.Orders.AsNoTracking().Include(o => o.Itens).Where(o => o.UsuarioId == UserId)
            .OrderByDescending(o => o.CriadoEm).ToListAsync(ct)).Select(OrderService.ToResponse));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Itens)
            .SingleOrDefaultAsync(o => o.Id == id && o.UsuarioId == UserId, ct);
        // 404 evita revelar a existência de pedidos de outra pessoa.
        return order is null ? NotFound() : Ok(OrderService.ToResponse(order));
    }
}
