using Microsoft.EntityFrameworkCore;
using Shared;
using Vendas.Api.Data;
using Vendas.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddControllers();
var dataDir = builder.Configuration["Data:Directory"] ?? Path.Combine(builder.Environment.ContentRootPath, ".data");
Directory.CreateDirectory(dataDir);
builder.Services.AddDbContext<SalesDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "vendas.db")};Default Timeout=30"));
builder.Services.AddHttpClient<StockClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Stock"] ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration.RequiredSecret("Internal:Key"));
});
builder.Services.AddSingleton<OrderGate>();
builder.Services.AddScoped<OrderService>();
if (!builder.Configuration.GetValue<bool>("Workers:Disabled"))
{
    builder.Services.AddHostedService<PendingOrderWorker>();
    builder.Services.AddHostedService<OutboxPublisher>();
}
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<SalesDb>().Database.MigrateAsync();
app.UseApiSecurity();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "vendas" }));
app.Run();

public partial class Program;
