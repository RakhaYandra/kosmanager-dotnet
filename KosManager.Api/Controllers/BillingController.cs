using System.Security.Claims;
using System.Text;
using KosManager.Application.Billing;
using KosManager.Application.Notify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KosManager.Api.Controllers;

public record VerifyIn(bool Approve);
public record PaymentIn(int BillId, string Method = "tunai", string? ProofPath = null);
public record TestIn(string ChatId);

[ApiController]
[Route("api/bills")]
[Authorize]
public class BillsController(BillingService billing) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwner => User.IsInRole("owner");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        int? tenantId = null;
        if (!IsOwner) tenantId = await billing.TenantIdByUserAsync(UserId);
        return Ok(await billing.ListAsync(status, tenantId));
    }

    [HttpPost("generate")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Generate([FromQuery] string periode) =>
        Ok(new { generated = await billing.GenerateAsync(periode) });
}

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(PaymentService payments) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwner => User.IsInRole("owner");

    [HttpPost]
    public async Task<IActionResult> Create(PaymentIn input)
    {
        var p = await payments.CreateAsync(input.BillId, input.Method, input.ProofPath, UserId, IsOwner);
        return Created($"/api/payments/{p.Id}", p);
    }

    [HttpPost("{id:int}/verify")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Verify(int id, VerifyIn input) =>
        Ok(await payments.VerifyAsync(id, input.Approve, UserId));

    [HttpGet("queue")]
    [Authorize(Policy = "owner")]
    public async Task<IActionResult> Queue() => Ok(await payments.QueueAsync());
}

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "owner")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? periode) =>
        Ok(await dashboard.GetAsync(periode ?? DateTime.Now.ToString("yyyy-MM")));

    [HttpGet("report.csv")]
    public async Task<IActionResult> Report([FromQuery] string? periode)
    {
        var csv = await dashboard.ReportCsvAsync(periode ?? DateTime.Now.ToString("yyyy-MM"));
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"rekap-{periode}.csv");
    }
}

[ApiController]
[Route("api/notify")]
[Authorize(Policy = "owner")]
public class NotifyController(INotificationSender sender) : ControllerBase
{
    [HttpPost("test")]
    public async Task<IActionResult> Test(TestIn input)
    {
        var ok = await sender.SendAsync(input.ChatId, "Test KosManager: notifikasi tersambung.");
        return Ok(new { channel = sender.Channel, sent = ok });
    }
}
