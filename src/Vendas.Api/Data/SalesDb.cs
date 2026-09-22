using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Vendas.Api.Models;

namespace Vendas.Api.Data;

public class SalesDb(DbContextOptions<SalesDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Order>().HasMany(o => o.Itens).WithOne().HasForeignKey(i => i.OrderId);
        model.Entity<Order>().HasIndex(o => new { o.UsuarioId, o.CriadoEm });
        model.Entity<Order>().HasIndex(o => o.Status);
        model.Entity<OrderItem>().HasIndex(i => new { i.OrderId, i.ProdutoId }).IsUnique();
        model.Entity<OutboxMessage>().HasIndex(m => m.PedidoId).IsUnique();
        model.Entity<OutboxMessage>().HasIndex(m => m.PublicadoEm);
    }
}

public class SalesDbFactory : IDesignTimeDbContextFactory<SalesDb>
{
    public SalesDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SalesDb>()
        .UseSqlite("Data Source=vendas.db").Options);
}
