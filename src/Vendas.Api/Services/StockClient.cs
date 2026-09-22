using System.Net;
using Shared;

namespace Vendas.Api.Services;

public class StockClient(HttpClient http)
{
    public async Task<ReservationResponse> Reserve(Guid orderId, List<ItemRequest> items, CancellationToken ct)
    {
        // PUT repetido retorna a mesma reserva e os preços originais.
        using var response = await http.PutAsJsonAsync($"internal/reservas/{orderId}", new ReservationRequest(items), ct);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            throw new BusinessException((int)response.StatusCode,
                response.StatusCode == HttpStatusCode.NotFound ? "Produto não encontrado." : "Não foi possível reservar os itens: verifique disponibilidade e quantidades.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReservationResponse>(ct)
            ?? throw new HttpRequestException("Resposta vazia do estoque.");
    }
}
