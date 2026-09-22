using System.ComponentModel.DataAnnotations;

namespace Estoque.Api.DTOs;

public record CreateProduct(
    [Required, StringLength(150, MinimumLength = 1)] string Nome,
    [Required, StringLength(2000)] string Descricao,
    [Range(typeof(decimal), "0.01", "1000000000")] decimal Preco,
    [Range(0, 1000000)] int Quantidade);

public record AdjustStock([Range(0, 1000000)] int Quantidade);
public record ProductResponse(Guid Id, string Nome, string Descricao, decimal Preco,
    int Quantidade, int Reservada, int Disponivel);
