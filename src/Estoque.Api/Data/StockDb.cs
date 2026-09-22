using Estoque.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Estoque.Api.Data;

public class StockDb(DbContextOptions<StockDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>().ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Product_Stock", "Quantidade >= 0 AND Reservada >= 0 AND Reservada <= Quantidade");
            t.HasCheckConstraint("CK_Product_Price", "CAST(Preco AS REAL) > 0");
        });
        model.Entity<Product>().Property(p => p.Nome).HasMaxLength(150);
        model.Entity<Reservation>().HasKey(r => r.PedidoId);
        model.Entity<Reservation>().HasMany(r => r.Itens).WithOne().HasForeignKey(i => i.ReservationPedidoId);
        model.Entity<ReservationItem>().HasIndex(i => new { i.ReservationPedidoId, i.ProdutoId }).IsUnique();
        model.Entity<ReservationItem>().HasOne<Product>().WithMany().HasForeignKey(i => i.ProdutoId);
        model.Entity<ProcessedEvent>().HasIndex(e => e.PedidoId).IsUnique();
    }
}

public class StockDbFactory : IDesignTimeDbContextFactory<StockDb>
{
    public StockDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<StockDb>()
        .UseSqlite("Data Source=estoque.db").Options);
}
