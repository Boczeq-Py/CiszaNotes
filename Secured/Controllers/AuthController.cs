using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace PrivateNotes;

[ApiController, Route("api/auth")]
public sealed class AuthController(
    SignInManager<IdentityUser> signIn,
    UserManager<IdentityUser> users,
    IAntiforgery csrf) : ControllerBase
{

    [HttpGet("csrf")]
    public IActionResult Token() =>
        Ok(new { token = csrf.GetAndStoreTokens(HttpContext).RequestToken });


    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, input.Password,
                isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
                return Ok(new { email = user.Email });
        }

        return Unauthorized(new { message = "Nieprawidłowy e-mail lub hasło albo konto chwilowo zablokowane." });
    }

    [Authorize, HttpGet("me")]
    public IActionResult Me() => Ok(new { email = User.Identity!.Name });

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }
}
