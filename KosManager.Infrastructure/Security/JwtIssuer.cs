using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KosManager.Application.Auth;
using KosManager.Domain;
using Microsoft.IdentityModel.Tokens;

namespace KosManager.Infrastructure.Security;

public class JwtIssuer(string secret) : IJwtIssuer
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    public string Issue(User u)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new Claim(ClaimTypes.Role, u.Role),
            new Claim(JwtRegisteredClaimNames.Email, u.Email),
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(24), claims: claims, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
