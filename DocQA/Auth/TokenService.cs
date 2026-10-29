using DocQA.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static DocQA.Models.Options;

namespace DocQA.Auth;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresAt) Create(AppUser user)
    {
        var jwt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}