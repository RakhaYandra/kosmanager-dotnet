using KosManager.Application;
using KosManager.Application.Billing;
using KosManager.Application.Notify;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KosManager.Infrastructure.Notify;

/// <summary>Polling getUpdates (bukan webhook): jalan lokal tanpa URL publik.
/// Offset persisten di file agar redelivery tak diproses ganda.</summary>
public class TelegramPollingService(
    IServiceScopeFactory scopes,
    ITelegramClient client,
    INotificationSender sender,
    ILogger<TelegramPollingService> log) : BackgroundService
{
    private readonly string _offsetFile =
        Environment.GetEnvironmentVariable("TELEGRAM_OFFSET_FILE") ?? "telegram-offset.dat";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var offset = ReadOffset();
                var updates = await client.GetUpdatesAsync(offset, ct);
                foreach (var u in updates.OrderBy(x => x.UpdateId))
                    await HandleAsync(u, ct);
                if (updates.Count > 0)
                    await File.WriteAllTextAsync(_offsetFile, (updates.Max(x => x.UpdateId) + 1).ToString(), ct);
            }
            catch (Exception ex) { log.LogError(ex, "telegram poll gagal"); }
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }

    private long ReadOffset()
    {
        if (long.TryParse(File.Exists(_offsetFile) ? File.ReadAllText(_offsetFile) : null, out var o)) return o;
        return 1;
    }

    internal async Task HandleAsync(TelegramUpdate u, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserRepository>();
        var tenants = sp.GetRequiredService<ITenantRepository>();
        var payments = sp.GetRequiredService<PaymentService>();

        var text = (u.Text ?? "").Trim();
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var email = text["/start".Length..].Trim();
            var user = string.IsNullOrEmpty(email) ? null : await users.ByEmailAsync(email, ct);
            var tenantId = user is null ? null : await tenants.TenantIdByUserAsync(user.Id, ct);
            if (tenantId is null)
            {
                await sender.SendAsync(u.ChatId, "Format: /start <email> (email akun penghunimu).", ct);
                return;
            }
            var tenant = tenantId is null
                ? null
                : await tenants.ByIdAsync(tenantId.Value, ct);
            if (tenant is null)
            {
                await sender.SendAsync(u.ChatId, "Format: /start <email> (email akun penghunimu).", ct);
                return;
            }
            tenant.TelegramChatId = u.ChatId;
            await tenants.SaveAsync(ct);
            await sender.SendAsync(u.ChatId, $"Tertaut ke {tenant.Name}. Reminder tagihan akan masuk ke sini.", ct);
            return;
        }

        if (text.Equals("SUDAH", StringComparison.OrdinalIgnoreCase))
        {
            var tenant = (await tenants.ListWithRoomAsync(ct)).FirstOrDefault(t => t.TelegramChatId == u.ChatId);
            if (tenant is null || tenant.UserId is null)
            {
                await sender.SendAsync(u.ChatId, "Belum tertaut. Kirim /start <email> dulu.", ct);
                return;
            }
            var bills = sp.GetRequiredService<IBillRepository>();
            var unpaid = (await bills.ListAsync("unpaid", tenant.Id, ct)).OrderByDescending(b => b.Period).FirstOrDefault();
            if (unpaid is null)
            {
                await sender.SendAsync(u.ChatId, "Tidak ada tagihan belum bayar.", ct);
                return;
            }
            await payments.CreateAsync(unpaid.Id, "transfer", null, tenant.UserId.Value, false, ct);
            await sender.SendAsync(u.ChatId, $"Tercatat untuk periode {unpaid.Period}, menunggu verifikasi owner.", ct);
            return;
        }

        await sender.SendAsync(u.ChatId, "Perintah: /start <email> | SUDAH", ct);
    }
}
