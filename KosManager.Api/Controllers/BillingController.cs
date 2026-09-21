using System.Security.Claims;
using System.Text;
using KosManager.Api.Data;
using KosManager.Api.Models;
using KosManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Api.Controllers;

public record VerifyIn(bool Approve);

[ApiController]
[Route("api/bills")]
[Authorize]
public class BillsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwner => User.IsInRole("owner");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = db.Bills.Include(b => b.Tenant).AsQueryable();
        if (!IsOwner)
        {
            var tenantId = await db.Tenants.Where(t => t.UserId == UserId).Select(t => t.Id).FirstOrDefaultAsync();
            q = q.Where(b => b.TenantId == tenantId);
        }
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(b => b.Status == status);
        var today = DateOnly.FromDateTime(DateTime.Now);
        return Ok(await q.OrderByDescending(b => b.Period).Select(b => new
        {
            b.Id, b.TenantId, tenant = b.Tenant!.Name, b.Period, b.Amount, b.DueDate, b.Status,
            daysLate = today.DayNumber - b.DueDate.DayNumber,
        }).ToListAsync());
    }

    [HttpPost("generate")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Generate([FromQuery] string periode)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(periode ?? "", @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "periode format YYYY-MM" });
        var year = int.Parse(periode[..4]); var month = int.Parse(periode[5..]);
        var due = new DateOnly(year, month, 10);
        var tenants = await db.Tenants.Include(t => t.Room).Where(t => t.RoomId != null).ToListAsync();
        var made = 0;
        foreach (var t in tenants)
        {
            if (await db.Bills.AnyAsync(b => b.TenantId == t.Id && b.Period == periode)) continue;
            db.Bills.Add(new Bill { TenantId = t.Id, Period = periode, Amount = t.Room!.MonthlyPrice, DueDate = due });
            made++;
        }
        await db.SaveChangesAsync();
        return Ok(new { generated = made });
    }
}

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwner => User.IsInRole("owner");

    [HttpPost]
    public async Task<IActionResult> Create(Payment input)
    {
        var bill = await db.Bills.FindAsync(input.BillId);
        if (bill is null) return NotFound(new { error = "tagihan tidak ada" });
        if (!IsOwner)
        {
            var mine = await db.Tenants.AnyAsync(t => t.Id == bill.TenantId && t.UserId == UserId);
            if (!mine) return Forbid();
        }
        input.Verified = false;
        db.Payments.Add(input);
        bill.Status = "pending";
        await db.SaveChangesAsync();
        return Created($"/api/payments/{input.Id}", input);
    }

    [HttpPost("{id:int}/verify")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Verify(int id, VerifyIn input)
    {
        var p = await db.Payments.Include(x => x.Bill).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        p.Verified = input.Approve;
        p.VerifiedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        p.Bill!.Status = input.Approve ? "paid" : "unpaid";
        await db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpGet("queue")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Queue() =>
        Ok(await db.Payments.Include(p => p.Bill).ThenInclude(b => b!.Tenant)
            .Where(p => !p.Verified).OrderBy(p => p.CreatedAt).ToListAsync());
}

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "owner")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? periode)
    {
        periode ??= DateTime.Now.ToString("yyyy-MM");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var rooms = await db.Rooms.CountAsync();
        var filled = await db.Rooms.CountAsync(r => r.Status == "isi");
        var bills = await db.Bills.Include(b => b.Tenant).Where(b => b.Period == periode).ToListAsync();
        return Ok(new
        {
            occupancy = new { filled, total = rooms },
            kas = bills.Where(b => b.Status == "paid").Sum(b => b.Amount),
            tunggakan = bills.Where(b => b.Status != "paid").Sum(b => b.Amount),
            overdue = bills.Where(b => b.Status != "paid" && b.DueDate < today)
                .Select(b => new { tenant = b.Tenant!.Name, b.Amount, b.DueDate, daysLate = today.DayNumber - b.DueDate.DayNumber })
                .OrderByDescending(x => x.daysLate).ToList(),
            reminders = await db.NotificationLogs.CountAsync(l => l.Status == "sent"),
        });
    }

    [HttpGet("report.csv")]
    public async Task<IActionResult> Report([FromQuery] string? periode)
    {
        periode ??= DateTime.Now.ToString("yyyy-MM");
        var bills = await db.Bills.Include(b => b.Tenant).Where(b => b.Period == periode).ToListAsync();
        var sb = new StringBuilder("penghuni,periode,nominal,jatuh_tempo,status\n");
        foreach (var b in bills) sb.AppendLine($"{b.Tenant!.Name},{b.Period},{b.Amount},{b.DueDate:yyyy-MM-dd},{b.Status}");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"rekap-{periode}.csv");
    }
}

[ApiController]
[Route("api/notify")]
[Authorize(Policy = "owner")]
public class NotifyController(AppDbContext db, INotificationSender sender) : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestIn input)
    {
        var ok = await sender.SendAsync(input.ChatId, "Test KosManager: notifikasi tersambung.");
        return Ok(new { channel = sender.Channel, sent = ok });
    }
    public record TestIn(string ChatId);
}
