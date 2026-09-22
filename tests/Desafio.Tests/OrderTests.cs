using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Vendas.Api.Data;
using Vendas.Api.Services;
using Xunit;

namespace Desafio.Tests;

public class OrderTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"sales-test-{Guid.NewGuid()}.db");
    private readonly OrderGate gate = new();
    private SalesDb Db() => new(new DbContextOptionsBuilder<SalesDb>().UseSqlite($"Data Source={path};Pooling=False").Options);
    private OrderService Service(SalesDb db, HttpMessageHandler handler) => new(db,
        new StockClient(new HttpClient(handler) { BaseAddress = new Uri("http://estoque") }), gate, NullLogger<OrderService>.Instance);

    private sealed class StockHandler(bool unavailable = false, bool insufficient = false) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (unavailable) throw new HttpRequestException("Estoque fora do ar");
            if (insufficient) return new(HttpStatusCode.Conflict);
            var input = await request.Content!.ReadFromJsonAsync<ReservationRequest>(ct);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ReservationResponse(Guid.Parse(request.RequestUri!.Segments.Last()), "Ativa",
                    input!.Itens.Select(i => new ReservedItem(i.ProdutoId, i.Quantidade, 12.50m)).ToList()))
            };
        }
    }

    [Fact]
    public async Task Confirmation_saves_price_total_and_outbox_together()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        var id = Guid.NewGuid();
        var service = Service(db, new StockHandler());
        var order = await service.Create("cliente", [new(id, 1), new(id, 2)], default);
        Assert.Equal("Confirmado", order.Status);
        Assert.Equal(37.50m, order.Total);
        Assert.Single(order.Itens);
        Assert.Equal(12.50m, order.Itens.Single().PrecoUnitario);
        var message = Assert.Single(await db.Outbox.ToListAsync());
        Assert.Equal(order.Id, message.PedidoId);
        Assert.Null(message.PublicadoEm);
        await service.Advance(order.Id, default);
        Assert.Single(await db.Outbox.ToListAsync());
    }

    [Fact]
    public async Task Pending_order_is_recovered_after_stock_returns()
    {
        Guid id;
        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            var order = await Service(db, new StockHandler(unavailable: true)).Create("cliente", [new(Guid.NewGuid(), 1)], default);
            Assert.Equal("Pendente", order.Status);
            Assert.Empty(await db.Outbox.ToListAsync());
            id = order.Id;
        }
        await using var recovered = Db();
        var confirmed = await Service(recovered, new StockHandler()).Advance(id, default);
        Assert.Equal("Confirmado", confirmed.Status);
        Assert.Single(await recovered.Outbox.ToListAsync());
    }

    [Fact]
    public async Task Insufficient_stock_rejects_order_without_event()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        var order = await Service(db, new StockHandler(insufficient: true)).Create("cliente", [new(Guid.NewGuid(), 1)], default);
        Assert.Equal("Rejeitado", order.Status);
        Assert.Empty(await db.Outbox.ToListAsync());
    }

    [Fact]
    public async Task Invalid_items_do_not_persist_order()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        await Assert.ThrowsAsync<BusinessException>(() => Service(db, new StockHandler()).Create("cliente", [new(Guid.NewGuid(), 0)], default));
        Assert.Empty(await db.Orders.ToListAsync());
    }

    public void Dispose()
    {
        gate.Dispose();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
    }
}
