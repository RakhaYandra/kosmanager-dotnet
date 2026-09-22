using KosManager.Application;
using KosManager.Application.Notify;
using KosManager.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KosManager.Infrastructure.Notify;

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
        var bills = scope.ServiceProvider.GetRequiredService<IBillRepository>();
        var logs = scope.ServiceProvider.GetRequiredService<INotificationLogRepository>();
        var open = await bills.OpenAsync(ct);
        var sent = 0;
        foreach (var b in open)
        {
            var days = b.DueDate.DayNumber - today.DayNumber;
            var stage = days == 3 ? 1 : days == 1 ? 2 : days < 0 && b.RemindedStage < 3 ? 3 : 0;
            if (stage == 0 || stage <= b.RemindedStage) continue;
            var chatId = b.Tenant?.TelegramChatId;
            if (string.IsNullOrWhiteSpace(chatId)) continue;
            var ok = await sender.SendAsync(chatId, Templates.For(stage, b.Tenant!.Name, b.Period, b.Amount, b.DueDate), ct);
            await logs.AddAsync(new NotificationLog
            {
                BillId = b.Id,
                Channel = sender.Channel,
                Stage = stage == 1 ? "H-3" : stage == 2 ? "H-1" : "H+1",
                Status = ok ? "sent" : "failed",
            }, ct);
            if (ok) { b.RemindedStage = stage; sent++; }
        }
        await bills.SaveAsync(ct);
        await logs.SaveAsync(ct);
        return sent;
    }
}
