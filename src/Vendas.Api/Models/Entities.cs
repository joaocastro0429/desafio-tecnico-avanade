namespace Vendas.Api.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UsuarioId { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pendente";
    public string? Motivo { get; set; }
    public decimal Total { get; set; }
    public List<OrderItem> Itens { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? PublicadoEm { get; set; }
}
