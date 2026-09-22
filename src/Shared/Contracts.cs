namespace Shared;

public record ItemRequest(Guid ProdutoId, int Quantidade);
public record ReservationRequest(List<ItemRequest> Itens);
public record ReservedItem(Guid ProdutoId, int Quantidade, decimal PrecoUnitario);
public record ReservationResponse(Guid PedidoId, string Status, List<ReservedItem> Itens);
public record SaleConfirmed(Guid EventoId, Guid PedidoId, List<ItemRequest> Itens);

public sealed class BusinessException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class ItemRules
{
    public static List<ItemRequest> Normalize(List<ItemRequest>? items)
    {
        if (items is null || items.Count == 0 || items.Count > 100)
            throw new BusinessException(400, "Informe de 1 a 100 itens.");
        if (items.Any(i => i is null || i.ProdutoId == Guid.Empty || i.Quantidade <= 0 || i.Quantidade > 1000000))
            throw new BusinessException(400, "Produto e quantidade de cada item devem ser válidos.");
        var result = items.GroupBy(i => i.ProdutoId)
            .Select(g => new ItemRequest(g.Key, checked(g.Sum(i => i.Quantidade)))).ToList();
        if (result.Any(i => i.Quantidade > 1000000))
            throw new BusinessException(400, "Quantidade máxima por produto: 1000000.");
        return result;
    }
}
