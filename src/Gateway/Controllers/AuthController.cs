using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gateway.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Shared;

namespace Gateway.Controllers;

public record LoginRequest([Required, StringLength(100)] string Username, [Required, StringLength(200)] string Password);

[ApiController, Route("auth"), EnableRateLimiting("login")]
public class AuthController(DemoUsers users, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(LoginRequest input)
    {
        var user = users.Authenticate(input.Username, input.Password);
        if (user is null) return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
        var expires = DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            issuer: "desafio-avanade", audience: "desafio-api",
            claims: [new Claim("sub", user.Id), new Claim("role", user.Role), new Claim("jti", Guid.NewGuid().ToString())],
            notBefore: DateTime.UtcNow, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.RequiredSecret("Jwt:Key"))), SecurityAlgorithms.HmacSha256));
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresAt = expires, role = user.Role });
    }
}
