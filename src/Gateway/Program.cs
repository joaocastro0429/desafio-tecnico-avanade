using System.Threading.RateLimiting;
using Gateway.Auth;
using Microsoft.AspNetCore.RateLimiting;
using Shared;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSingleton<DemoUsers>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var stock = builder.Configuration["Services:Stock"] ?? "http://localhost:5001";
var sales = builder.Configuration["Services:Sales"] ?? "http://localhost:5002";
builder.Services.AddReverseProxy().LoadFromMemory(
    [new RouteConfig { RouteId = "produtos", ClusterId = "estoque", AuthorizationPolicy = "User",
        Match = new RouteMatch { Path = "/produtos/{**rest}" } },
     new RouteConfig { RouteId = "pedidos", ClusterId = "vendas", AuthorizationPolicy = "User",
        Match = new RouteMatch { Path = "/pedidos/{**rest}" } }],
    [new ClusterConfig { ClusterId = "estoque", Destinations = new Dictionary<string, DestinationConfig> { ["api"] = new() { Address = stock } } },
     new ClusterConfig { ClusterId = "vendas", Destinations = new Dictionary<string, DestinationConfig> { ["api"] = new() { Address = sales } } }]);
var app = builder.Build();
_ = app.Services.GetRequiredService<DemoUsers>();
app.UseApiSecurity();
app.UseRateLimiter();
app.MapControllers();
app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));
app.Run();

public partial class Program;
