using System.Security.Claims;
using KosManager.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KosManager.Api.Controllers;

public record RegisterIn(string Email, string Password, string Role = "penghuni");
public record LoginIn(string Email, string Password);

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterIn input)
    {
        // Registrasi publik hanya boleh role penghuni; owner dibuat via seed.
        if (input.Role != "penghuni") throw new BadRequestException("role must be penghuni");
        var u = await auth.RegisterAsync(input.Email, input.Password);
        return Ok(new { id = u.Id, email = u.Email, role = u.Role });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginIn input) =>
        Ok(new { token = await auth.LoginAsync(input.Email, input.Password) });

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var u = await auth.MeAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return Ok(new { id = u.Id, email = u.Email, role = u.Role });
    }
}
