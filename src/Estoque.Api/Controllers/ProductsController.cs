using Estoque.Api.Data;
using Estoque.Api.DTOs;
using Estoque.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Api.Controllers;

[ApiController, Route("produtos"), Authorize(Policy = "User")]
public class ProductsController(StockDb db, StockService service) : ControllerBase
{
    [HttpPost, Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create(CreateProduct input, CancellationToken ct)
    {
        var product = await service.Create(input, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await db.Products.AsNoTracking().OrderBy(p => p.Nome).ToListAsync(ct)).Select(StockService.ToResponse));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? NotFound() : Ok(StockService.ToResponse(product));
    }

    [HttpPatch("{id:guid}/estoque"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> Adjust(Guid id, AdjustStock input, CancellationToken ct) => Ok(await service.Adjust(id, input.Quantidade, ct));
}
