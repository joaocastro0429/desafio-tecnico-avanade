using Estoque.Api.Data;
using Estoque.Api.DTOs;
using Estoque.Api.Models;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace Estoque.Api.Services;

public class StockService(StockDb db, ILogger<StockService> logger)
{
    public static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Nome, p.Descricao, p.Preco, p.Quantidade, p.Reservada, p.Quantidade - p.Reservada);

    public async Task<ProductResponse> Create(CreateProduct input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Nome) || input.Preco <= 0 || input.Preco > 1000000000 ||
            input.Quantidade < 0 || input.Quantidade > 1000000 || decimal.Round(input.Preco, 2) != input.Preco)
            throw new BusinessException(400, "Informe nome, preço positivo com até duas casas decimais e quantidade válida.");
        var product = new Product { Nome = input.Nome.Trim(), Descricao = input.Descricao.Trim(), Preco = input.Preco, Quantidade = input.Quantidade };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> Adjust(Guid id, int quantity, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var product = await db.Products.FindAsync([id], ct) ?? throw new BusinessException(404, "Produto não encontrado.");
        if (quantity < product.Reservada || quantity < 0 || quantity > 1000000)
            throw new BusinessException(409, "A quantidade física não pode ser menor que a reservada nem exceder o limite.");
        product.Quantidade = quantity;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(product);
    }

    public async Task<ReservationResponse> Reserve(Guid orderId, List<ItemRequest> input, CancellationToken ct)
    {
        if (orderId == Guid.Empty) throw new BusinessException(400, "Pedido inválido.");
        var items = ItemRules.Normalize(input);
        // A transação de escrita do SQLite serializa a verificação e a reserva.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Reservations.Include(r => r.Itens).SingleOrDefaultAsync(r => r.PedidoId == orderId, ct);
        if (existing is not null)
        {
            if (existing.Status == "Liberada" || !SameItems(existing, items))
                throw new BusinessException(409, "Reserva liberada ou pedido repetido com itens diferentes.");
            return ToResponse(existing);
        }
        var reservation = new Reservation { PedidoId = orderId };
        foreach (var item in items.OrderBy(i => i.ProdutoId))
        {
            var product = await db.Products.FindAsync([item.ProdutoId], ct)
                ?? throw new BusinessException(404, "Produto não encontrado.");
            if (product.Quantidade - product.Reservada < item.Quantidade)
                throw new BusinessException(409, "Estoque insuficiente.");
            product.Reservada += item.Quantidade;
            reservation.Itens.Add(new ReservationItem
            {
                ProdutoId = product.Id, Quantidade = item.Quantidade, PrecoUnitario = product.Preco
            });
        }
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Reserva criada para pedido {PedidoId}", orderId);
        return ToResponse(reservation);
    }

    public async Task<ReservationResponse?> GetReservation(Guid id, CancellationToken ct)
    {
        var reservation = await db.Reservations.AsNoTracking().Include(r => r.Itens).SingleOrDefaultAsync(r => r.PedidoId == id, ct);
        return reservation is null ? null : ToResponse(reservation);
    }

    public async Task Release(Guid orderId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservation = await db.Reservations.Include(r => r.Itens).SingleOrDefaultAsync(r => r.PedidoId == orderId, ct);
        if (reservation is null)
        {
            // Tombstone: impede que uma requisição atrasada recrie uma reserva já liberada.
            db.Reservations.Add(new Reservation { PedidoId = orderId, Status = "Liberada" });
        }
        else if (reservation.Status == "Concluida")
            throw new BusinessException(409, "A baixa já foi concluída.");
        else if (reservation.Status == "Ativa")
        {
            foreach (var item in reservation.Itens)
            {
                var product = await db.Products.SingleAsync(p => p.Id == item.ProdutoId, ct);
                product.Reservada -= item.Quantidade;
            }
            reservation.Status = "Liberada";
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ApplySale(SaleConfirmed message, CancellationToken ct)
    {
        if (message.EventoId == Guid.Empty || message.PedidoId == Guid.Empty)
            throw new BusinessException(400, "Evento inválido.");
        var items = ItemRules.Normalize(message.Itens);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.ProcessedEvents.AnyAsync(e => e.Id == message.EventoId, ct)) return;
        var reservation = await db.Reservations.Include(r => r.Itens).SingleOrDefaultAsync(r => r.PedidoId == message.PedidoId, ct)
            ?? throw new BusinessException(409, "Reserva não encontrada para o evento.");
        if (!SameItems(reservation, items)) throw new BusinessException(409, "Itens do evento não correspondem à reserva.");
        if (reservation.Status == "Concluida") return; // Protege também contra outro ID para o mesmo pedido.
        if (reservation.Status != "Ativa") throw new BusinessException(409, "Reserva não está ativa.");
        foreach (var item in reservation.Itens)
        {
            var product = await db.Products.SingleAsync(p => p.Id == item.ProdutoId, ct);
            product.Quantidade -= item.Quantidade;
            product.Reservada -= item.Quantidade;
        }
        reservation.Status = "Concluida";
        db.ProcessedEvents.Add(new ProcessedEvent { Id = message.EventoId, PedidoId = message.PedidoId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Baixa concluída. Pedido {PedidoId}, evento {EventoId}", message.PedidoId, message.EventoId);
    }

    private static bool SameItems(Reservation reservation, List<ItemRequest> items) =>
        reservation.Itens.Count == items.Count && items.All(i => reservation.Itens.Any(r => r.ProdutoId == i.ProdutoId && r.Quantidade == i.Quantidade));

    private static ReservationResponse ToResponse(Reservation r) => new(r.PedidoId, r.Status,
        r.Itens.Select(i => new ReservedItem(i.ProdutoId, i.Quantidade, i.PrecoUnitario)).ToList());
}
