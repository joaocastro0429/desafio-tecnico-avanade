using Estoque.Api.Data;
using Estoque.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddControllers();
var dataDir = builder.Configuration["Data:Directory"] ?? Path.Combine(builder.Environment.ContentRootPath, ".data");
Directory.CreateDirectory(dataDir);
builder.Services.AddDbContext<StockDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "estoque.db")};Default Timeout=30"));
builder.Services.AddScoped<StockService>();
if (!builder.Configuration.GetValue<bool>("Workers:Disabled")) builder.Services.AddHostedService<SaleConsumer>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<StockDb>().Database.MigrateAsync();
app.UseApiSecurity();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "estoque" }));
app.Run();

public partial class Program;
