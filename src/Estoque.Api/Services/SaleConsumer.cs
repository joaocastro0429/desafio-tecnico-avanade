using System.Text.Json;
using RabbitMQ.Client;
using Shared;

namespace Estoque.Api.Services;

public class SaleConsumer(IServiceScopeFactory scopes, IConfiguration config, ILogger<SaleConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await Rabbit.Connect(config, stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await Rabbit.Declare(channel, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Polling deliberadamente simples para o volume do desafio.
                    var delivery = await channel.BasicGetAsync(Rabbit.Queue, autoAck: false, stoppingToken);
                    if (delivery is null) { await Task.Delay(500, stoppingToken); continue; }
                    var completed = false;
                    for (var attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            var message = JsonSerializer.Deserialize<SaleConfirmed>(delivery.Body.Span)
                                ?? throw new JsonException("Evento vazio.");
                            using var scope = scopes.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<StockService>().ApplySale(message, stoppingToken);
                            completed = true;
                            break;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Falha no evento {MessageId}, tentativa {Attempt}", delivery.BasicProperties.MessageId, attempt);
                            if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(attempt), stoppingToken);
                        }
                    }
                    if (completed) await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    else
                    {
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, requeue: false, stoppingToken);
                        logger.LogError("Evento {MessageId} encaminhado à fila de erros", delivery.BasicProperties.MessageId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consumidor desconectado; nova tentativa em 5 segundos");
                try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
