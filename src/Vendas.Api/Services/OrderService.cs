using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared;
using Vendas.Api.Data;
using Vendas.Api.DTOs;
using Vendas.Api.Models;

namespace Vendas.Api.Services;

// Coordena requisições e recuperação dentro da instância local do desafio.
public sealed class OrderGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}

public class OrderService(SalesDb db, StockClient stock, OrderGate gate, ILogger<OrderService> logger)
{
    public async Task<OrderResponse> Create(string userId, List<ItemRequest> input, CancellationToken ct)
    {
        var items = ItemRules.Normalize(input);
        var order = new Order
        {
            UsuarioId = userId,
            Itens = items.Select(i => new OrderItem { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade }).ToList()
        };
        db.Orders.Add(order);
        // Persistir primeiro permite retomar se o processo cair após reservar o estoque.
        await db.SaveChangesAsync(ct);
        return await Advance(order.Id, ct);
    }

    public async Task<OrderResponse> Advance(Guid orderId, CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            db.ChangeTracker.Clear();
            var order = await db.Orders.Include(o => o.Itens).SingleAsync(o => o.Id == orderId, ct);
            if (order.Status != "Pendente") return ToResponse(order);
            ReservationResponse reservation;
            try
            {
                reservation = await stock.Reserve(order.Id,
                    order.Itens.Select(i => new ItemRequest(i.ProdutoId, i.Quantidade)).ToList(), ct);
            }
            catch (BusinessException ex)
            {
                order.Status = "Rejeitado";
                order.Motivo = ex.Message;
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Pedido {PedidoId} rejeitado: {Motivo}", order.Id, order.Motivo);
                return ToResponse(order);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Pedido {PedidoId} permanece pendente; estoque indisponível", order.Id);
                return ToResponse(order);
            }
            foreach (var item in order.Itens)
            {
                var reserved = reservation.Itens.Single(i => i.ProdutoId == item.ProdutoId);
                item.PrecoUnitario = reserved.PrecoUnitario;
            }
            order.Total = order.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
            order.Status = "Confirmado";
            var message = new SaleConfirmed(Guid.NewGuid(), order.Id,
                order.Itens.Select(i => new ItemRequest(i.ProdutoId, i.Quantidade)).ToList());
            db.Outbox.Add(new OutboxMessage { Id = message.EventoId, PedidoId = order.Id, Payload = JsonSerializer.Serialize(message) });
            // SaveChanges é transacional: confirmação e outbox são persistidas juntas.
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pedido {PedidoId} confirmado; evento {EventoId} na outbox", order.Id, message.EventoId);
            return ToResponse(order);
        }
        finally { gate.Semaphore.Release(); }
    }

    public static OrderResponse ToResponse(Order order) => new(order.Id, order.CriadoEm, order.Status, order.Motivo,
        order.Total, order.Itens.Select(i => new OrderItemResponse(i.ProdutoId, i.Quantidade, i.PrecoUnitario)).ToList());
}
