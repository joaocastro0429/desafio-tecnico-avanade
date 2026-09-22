using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Shared;
using Vendas.Api.Data;

namespace Vendas.Api.Services;

public class OutboxPublisher(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await Rabbit.Connect(config, stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), stoppingToken);
                await Rabbit.Declare(channel, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SalesDb>();
                    var pending = await db.Outbox.Where(m => m.PublicadoEm == null).OrderBy(m => m.CriadoEm).Take(50).ToListAsync(stoppingToken);
                    foreach (var message in pending)
                    {
                        var properties = new BasicProperties
                        {
                            Persistent = true, ContentType = "application/json",
                            MessageId = message.Id.ToString(), Type = "VendaConfirmada.v1"
                        };
                        // O await só termina após confirmação do broker (ou falha).
                        await channel.BasicPublishAsync("", Rabbit.Queue, mandatory: true, properties,
                            Encoding.UTF8.GetBytes(message.Payload), stoppingToken);
                        message.PublicadoEm = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Evento {EventoId} publicado, pedido {PedidoId}", message.Id, message.PedidoId);
                    }
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Publicação interrompida; outbox preservada para nova tentativa");
                try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
