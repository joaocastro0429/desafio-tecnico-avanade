using Microsoft.EntityFrameworkCore;
using Vendas.Api.Data;

namespace Vendas.Api.Services;

public class PendingOrderWorker(IServiceScopeFactory scopes, ILogger<PendingOrderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SalesDb>();
                var ids = await db.Orders.Where(o => o.Status == "Pendente").OrderBy(o => o.CriadoEm)
                    .Select(o => o.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var orderScope = scopes.CreateScope();
                        await orderScope.ServiceProvider.GetRequiredService<OrderService>().Advance(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogError(ex, "Falha na recuperação do pedido {PedidoId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao consultar pedidos pendentes"); }
            try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
