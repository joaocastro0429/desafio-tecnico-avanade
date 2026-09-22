using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Shared;

public static class Rabbit
{
    public const string Queue = "estoque.venda-confirmada.v1";
    public const string DeadQueue = "estoque.venda-confirmada.erros.v1";
    public const string DeadExchange = "estoque.erros";

    public static async Task<IConnection> Connect(IConfiguration config, CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["Rabbit:Host"] ?? "localhost",
            Port = config.GetValue("Rabbit:Port", 5672),
            UserName = config["Rabbit:User"] ?? throw new InvalidOperationException("Configure Rabbit:User."),
            Password = config["Rabbit:Password"] ?? throw new InvalidOperationException("Configure Rabbit:Password."),
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            ClientProvidedName = "desafio-avanade"
        };
        return await factory.CreateConnectionAsync(ct);
    }

    public static async Task Declare(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(DeadExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadQueue, DeadExchange, "erro", cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadExchange,
                ["x-dead-letter-routing-key"] = "erro"
            }, cancellationToken: ct);
    }
}
