using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SMS.Api.Infrastructure;

public static class JwtTokenFactory
{
    public static string CreateToken(JwtOptions options, IReadOnlyCollection<Claim> claims, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}


