using System.Security.Claims;
using KosManager.Api.Auth;
using KosManager.Api.Data;
using KosManager.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Api.Controllers;

public record RegisterIn(string Email, string Password, string Role = "penghuni");
public record LoginIn(string Email, string Password);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterIn input)
    {
        // Registrasi publik hanya boleh role penghuni; owner dibuat via seed.
        if (input.Role != "penghuni") return BadRequest(new { error = "role must be penghuni" });
        if (await db.Users.AnyAsync(u => u.Email == input.Email))
            return Conflict(new { error = "email taken" });
        var u = new User { Email = input.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.Password), Role = "penghuni" };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { token = jwt.Issue(u) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginIn input)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Email == input.Email);
        if (u is null || !BCrypt.Net.BCrypt.Verify(input.Password, u.PasswordHash))
            return Unauthorized(new { error = "email atau kata sandi salah" });
        return Ok(new { token = jwt.Issue(u) });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await db.Users.FindAsync(id);
        return Ok(new { id = u!.Id, email = u.Email, role = u.Role });
    }
}
