using DocQA.Auth;
using DocQA.Data;
using DocQA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace DocQA.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email };
        var result = await users.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // The user name is the email, so skip the duplicate "user name taken" message.
            foreach (var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
                ModelState.AddModelError(error.Code, error.Description);

            return ValidationProblem(ModelState);
        }

        return Issue(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);

        // The same message for "no such user", "locked out" and "wrong password",
        // so the API doesn't reveal which emails are registered.
        if (user is null || await users.IsLockedOutAsync(user)) return InvalidLogin();

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user); // 5 failures lock the account for 5 minutes
            return InvalidLogin();
        }

        await users.ResetAccessFailedCountAsync(user);
        return Issue(user);
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<object> Me() => new
    {
        id = User.GetUserId(),
        email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
    };

    private ActionResult<AuthResponse> InvalidLogin() =>
        Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private AuthResponse Issue(AppUser user)
    {
        var (token, expiresAt) = tokens.Create(user);
        return new AuthResponse(token, expiresAt, user.Email!);
    }
}