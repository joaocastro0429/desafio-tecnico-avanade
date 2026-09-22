using Estoque.Api.Data;
using Estoque.Api.DTOs;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Desafio.Tests;

public class StockTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"stock-test-{Guid.NewGuid()}.db");
    private StockDb Db() => new(new DbContextOptionsBuilder<StockDb>().UseSqlite($"Data Source={path};Pooling=False").Options);
    private static StockService Service(StockDb db) => new(db, NullLogger<StockService>.Instance);

    private async Task<Guid> Product(int quantity = 10)
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        return (await Service(db).Create(new CreateProduct("Teclado", "USB", 50.25m, quantity), default)).Id;
    }

    [Fact]
    public async Task Sale_updates_stock_once_and_preserves_available_quantity()
    {
        var id = await Product();
        var order = Guid.NewGuid();
        var items = new List<ItemRequest> { new(id, 2) };
        await using (var db = Db())
        {
            var reservation = await Service(db).Reserve(order, items, default);
            Assert.Equal(50.25m, reservation.Itens.Single().PrecoUnitario);
            var product = await db.Products.SingleAsync();
            Assert.Equal(10, product.Quantidade);
            Assert.Equal(2, product.Reservada);
        }
        var message = new SaleConfirmed(Guid.NewGuid(), order, items);
        for (var i = 0; i < 3; i++)
        {
            await using var db = Db();
            await Service(db).ApplySale(i == 2 ? message with { EventoId = Guid.NewGuid() } : message, default);
        }
        await using var check = Db();
        var final = await check.Products.SingleAsync();
        Assert.Equal(8, final.Quantidade);
        Assert.Equal(0, final.Reservada);
        Assert.Single(await check.ProcessedEvents.ToListAsync());
    }

    [Fact]
    public async Task Reservation_is_idempotent_and_groups_duplicate_items()
    {
        var id = await Product();
        var order = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            await using var db = Db();
            var result = await Service(db).Reserve(order, [new(id, 1), new(id, 2)], default);
            Assert.Equal(3, result.Itens.Single().Quantidade);
        }
        await using var check = Db();
        Assert.Equal(3, (await check.Products.SingleAsync()).Reservada);
    }

    [Fact]
    public async Task Insufficient_item_rolls_back_entire_reservation()
    {
        var first = await Product();
        var second = await Product(0);
        await using (var db = Db())
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => Service(db).Reserve(Guid.NewGuid(), [new(first, 2), new(second, 1)], default));
            Assert.Equal(409, exception.StatusCode);
        }
        await using var check = Db();
        Assert.All(await check.Products.ToListAsync(), p => Assert.Equal(0, p.Reservada));
        Assert.Empty(await check.Reservations.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_reservations_cannot_sell_last_unit_twice()
    {
        var id = await Product(1);
        async Task<bool> Attempt()
        {
            await using var db = Db();
            try { await Service(db).Reserve(Guid.NewGuid(), [new(id, 1)], default); return true; }
            catch (BusinessException ex) when (ex.StatusCode == 409) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        Assert.Single(results, success => success);
        await using var check = Db();
        Assert.Equal(1, (await check.Products.SingleAsync()).Reservada);
    }

    [Fact]
    public async Task Release_is_idempotent_and_blocks_late_reservation()
    {
        var id = await Product();
        var order = Guid.NewGuid();
        await using (var db = Db()) await Service(db).Reserve(order, [new(id, 2)], default);
        for (var i = 0; i < 2; i++)
        {
            await using var db = Db();
            await Service(db).Release(order, default);
        }
        await using var check = Db();
        Assert.Equal(0, (await check.Products.SingleAsync()).Reservada);
        await Assert.ThrowsAsync<BusinessException>(() => Service(check).Reserve(order, [new(id, 2)], default));
    }

    [Fact]
    public async Task Mismatched_event_does_not_change_stock()
    {
        var id = await Product();
        var order = Guid.NewGuid();
        await using (var db = Db()) await Service(db).Reserve(order, [new(id, 2)], default);
        await using (var db = Db())
            await Assert.ThrowsAsync<BusinessException>(() => Service(db).ApplySale(new(Guid.NewGuid(), order, [new(id, 3)]), default));
        await using var check = Db();
        Assert.Equal(10, (await check.Products.SingleAsync()).Quantidade);
        Assert.Empty(await check.ProcessedEvents.ToListAsync());
    }

    [Fact]
    public async Task Adjustment_cannot_consume_reserved_stock()
    {
        var id = await Product();
        await using (var db = Db()) await Service(db).Reserve(Guid.NewGuid(), [new(id, 3)], default);
        await using var check = Db();
        await Assert.ThrowsAsync<BusinessException>(() => Service(check).Adjust(id, 2, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public async Task Invalid_price_is_rejected(decimal price)
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        await Assert.ThrowsAsync<BusinessException>(() => Service(db).Create(new("Produto", "Descrição", price, 1), default));
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
    }
}
