using DocQA.Auth;
using DocQA.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using static DocQA.Models.Options;

namespace DocQA.Tests;

public class TokenServiceTests
{
    private static TokenService Service(int minutes = 60) =>
        new(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = new string('k', 40),
            ExpiryMinutes = minutes
        }));

    private static AppUser User() => new() { Id = "u1", Email = "a@b.com" };

    [Fact]
    public void Token_carries_the_user_id_email_issuer_and_audience()
    {
        var (token, _) = Service().Create(User());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("u1", jwt.Subject);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal("a@b.com", jwt.Claims.First(c => c.Type == "email").Value);
    }

    [Fact]
    public void Token_expires_after_the_configured_time()
    {
        var (_, expiresAt) = Service(minutes: 30).Create(User());

        Assert.InRange(expiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void Token_signed_with_another_key_is_rejected()
    {
        var (token, _) = Service().Create(User());
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 40)))
        };

        Assert.ThrowsAny<SecurityTokenException>(
            () => new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _));
    }
}