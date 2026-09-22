using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Shared;

public static class WebSetup
{
    public static string RequiredSecret(this IConfiguration config, string key)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32)
            throw new InvalidOperationException($"Configure {key} com pelo menos 32 bytes.");
        return value;
    }

    public static void AddApiSecurity(this IServiceCollection services, IConfiguration config)
    {
        var key = config.RequiredSecret("Jwt:Key");
        config.RequiredSecret("Internal:Key");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "desafio-avanade",
                    ValidateAudience = true, ValidAudience = "desafio-api",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10),
                    NameClaimType = "sub", RoleClaimType = "role",
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };
            })
            .AddScheme<AuthenticationSchemeOptions, InternalAuthHandler>("Internal", _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("User", p => p.AddAuthenticationSchemes("Bearer").RequireAuthenticatedUser().RequireRole("Administrador", "Cliente"));
            options.AddPolicy("Admin", p => p.AddAuthenticationSchemes("Bearer").RequireAuthenticatedUser().RequireRole("Administrador"));
            options.AddPolicy("Internal", p => p.AddAuthenticationSchemes("Internal").RequireAuthenticatedUser());
        });
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
    }

    public static void UseApiSecurity(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
    }
}

public sealed class InternalAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration config) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var supplied = Request.Headers["X-Internal-Key"].ToString();
        var expected = config.RequiredSecret("Internal:Key");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
            return Task.FromResult(AuthenticateResult.Fail("Credencial interna inválida."));
        var identity = new ClaimsIdentity([new Claim("sub", "vendas-service")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception is BusinessException business ? business.StatusCode : 500;
        if (status == 500) logger.LogError(exception, "Falha inesperada. TraceId {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: status,
            title: status == 500 ? "Não foi possível concluir a operação." : exception.Message,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}
