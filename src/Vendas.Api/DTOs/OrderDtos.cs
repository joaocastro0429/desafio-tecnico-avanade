using Shared;

namespace Vendas.Api.DTOs;

public record CreateOrder(List<ItemRequest> Itens);
public record OrderItemResponse(Guid ProdutoId, int Quantidade, decimal PrecoUnitario);
public record OrderResponse(Guid Id, DateTime CriadoEm, string Status, string? Motivo, decimal Total, List<OrderItemResponse> Itens);
