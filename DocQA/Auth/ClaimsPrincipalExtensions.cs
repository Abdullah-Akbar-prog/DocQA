using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DocQA.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("The token has no 'sub' claim.");
}