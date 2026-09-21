using KosManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Api.Services;

// Sapu tagihan tiap jam, kirim H-3/H-1/H+1 sekali per tagihan (idempoten via RemindedStage).
public class ReminderService(IServiceScopeFactory scopes, INotificationSender sender, ILogger<ReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await SweepAsync(DateOnly.FromDateTime(DateTime.Now), ct); }
            catch (Exception ex) { log.LogError(ex, "reminder sweep gagal"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    public async Task<int> SweepAsync(DateOnly today, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bills = await db.Bills.Include(b => b.Tenant)
            .Where(b => b.Status == "unpaid" || b.Status == "pending")
            .ToListAsync(ct);
        var sent = 0;
        foreach (var b in bills)
        {
            var days = b.DueDate.DayNumber - today.DayNumber;
            var stage = days == 3 ? 1 : days == 1 ? 2 : days < 0 && b.RemindedStage < 3 ? 3 : 0;
            if (stage == 0 || stage <= b.RemindedStage) continue;
            var chatId = b.Tenant?.TelegramChatId;
            if (string.IsNullOrWhiteSpace(chatId)) continue;
            var ok = await sender.SendAsync(chatId, Templates.For(stage, b.Tenant!.Name, b.Period, b.Amount, b.DueDate), ct);
            db.NotificationLogs.Add(new Models.NotificationLog
            {
                BillId = b.Id,
                Channel = sender.Channel,
                Stage = stage == 1 ? "H-3" : stage == 2 ? "H-1" : "H+1",
                Status = ok ? "sent" : "failed",
            });
            if (ok) { b.RemindedStage = stage; sent++; }
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }
}

public static class Templates
{
    public static string For(int stage, string nama, string periode, decimal nominal, DateOnly tgl)
    {
        var rp = nominal.ToString("N0", new System.Globalization.CultureInfo("id-ID"));
        return stage switch
        {
            1 => $"Halo {nama}, tagihan kos periode {periode} Rp{rp} jatuh tempo {tgl:dd MMM yyyy}. Balas SUDAH jika sudah bayar.",
            2 => $"Pengingat: tagihan kos periode {periode} Rp{rp} jatuh tempo BESOK {tgl:dd MMM yyyy}. Balas SUDAH jika sudah bayar.",
            _ => $"Tagihan kos periode {periode} Rp{rp} sudah lewat jatuh tempo {tgl:dd MMM yyyy} ({(DateOnly.FromDateTime(DateTime.Now).DayNumber - tgl.DayNumber)} hari). Mohon segera dibayar.",
        };
    }
}
