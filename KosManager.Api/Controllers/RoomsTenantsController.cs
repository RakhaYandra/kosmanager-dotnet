using KosManager.Api.Data;
using KosManager.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize(Policy = "owner")]
public class RoomsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List()
    {
        var rooms = await db.Rooms.OrderBy(r => r.Number).ToListAsync();
        var tenantByRoom = await db.Tenants.Where(t => t.RoomId != null)
            .ToDictionaryAsync(t => t.RoomId!.Value, t => t.Name);
        return Ok(rooms.Select(r => new
        {
            r.Id, r.Number, r.Type, r.MonthlyPrice, r.Status,
            tenant = tenantByRoom.GetValueOrDefault(r.Id),
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Room input)
    {
        db.Rooms.Add(input);
        await db.SaveChangesAsync();
        return Created($"/api/rooms/{input.Id}", input);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Room input)
    {
        var r = await db.Rooms.FindAsync(id);
        if (r is null) return NotFound();
        r.Number = input.Number; r.Type = input.Type; r.MonthlyPrice = input.MonthlyPrice; r.Status = input.Status;
        await db.SaveChangesAsync();
        return Ok(r);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await db.Rooms.FindAsync(id);
        if (r is null) return NotFound();
        db.Rooms.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "owner")]
public class TenantsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.Tenants.Include(t => t.Room).OrderBy(t => t.Name).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Tenant input)
    {
        db.Tenants.Add(input);
        await SyncRoom(input);
        await db.SaveChangesAsync();
        return Created($"/api/tenants/{input.Id}", input);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Tenant input)
    {
        var t = await db.Tenants.FindAsync(id);
        if (t is null) return NotFound();
        t.Name = input.Name; t.Phone = input.Phone; t.TelegramChatId = input.TelegramChatId;
        t.RoomId = input.RoomId; t.MoveInDate = input.MoveInDate;
        await SyncRoom(t);
        await db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await db.Tenants.FindAsync(id);
        if (t is null) return NotFound();
        db.Tenants.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is null || file.Length > 2 * 1024 * 1024) return BadRequest(new { error = "csv maks 2MB" });
        using var reader = new StreamReader(file.OpenReadStream());
        var ok = 0; var failed = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var parts = line.Split(',');
            if (parts.Length < 3) { failed.Add(line); continue; }
            db.Tenants.Add(new Tenant { Name = parts[0].Trim(), Phone = parts[1].Trim(), MoveInDate = DateOnly.Parse(parts[2].Trim()) });
            ok++;
        }
        await db.SaveChangesAsync();
        return Ok(new { imported = ok, failed = failed.Count });
    }

    private async Task SyncRoom(Tenant t)
    {
        if (t.RoomId is null) return;
        var room = await db.Rooms.FindAsync(t.RoomId);
        if (room is not null) room.Status = "isi";
    }
}
