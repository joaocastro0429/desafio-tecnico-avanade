using System.Security.Cryptography;

namespace Gateway.Auth;

public record DemoUser(string Id, string Username, string Role, string PasswordHash);

public class DemoUsers
{
    private readonly List<DemoUser> users;

    public DemoUsers(IConfiguration config)
    {
        users = [
            new("admin", "admin", "Administrador", Required(config, "Demo:AdminHash")),
            new("cliente", "cliente", "Cliente", Required(config, "Demo:ClientHash")),
            new("cliente2", "cliente2", "Cliente", Required(config, "Demo:Client2Hash"))
        ];
    }

    private static string Required(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException($"Configure {key} com o script de preparação.");

    public DemoUser? Authenticate(string username, string password)
    {
        var user = users.SingleOrDefault(u => u.Username == username);
        // Executar o hash também para nomes inexistentes reduz diferenças de tempo.
        var encoded = (user ?? users[0]).PasswordHash.Split(':');
        var salt = Convert.FromBase64String(encoded[0]);
        var expected = Convert.FromBase64String(encoded[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        var valid = CryptographicOperations.FixedTimeEquals(actual, expected);
        return valid ? user : null;
    }
}
