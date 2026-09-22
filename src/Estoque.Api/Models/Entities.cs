namespace Estoque.Api.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public decimal Preco { get; set; }
    public int Quantidade { get; set; }
    public int Reservada { get; set; }
}

public class Reservation
{
    public Guid PedidoId { get; set; }
    public string Status { get; set; } = "Ativa";
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public List<ReservationItem> Itens { get; set; } = [];
}

public class ReservationItem
{
    public int Id { get; set; }
    public Guid ReservationPedidoId { get; set; }
    public Guid ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}

public class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public DateTime ProcessadoEm { get; set; } = DateTime.UtcNow;
}
