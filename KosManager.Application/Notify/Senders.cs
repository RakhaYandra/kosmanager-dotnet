using System.Net.Http.Json;

namespace KosManager.Application.Notify;

public interface INotificationSender
{
    string Channel { get; }
    Task<bool> SendAsync(string to, string text, CancellationToken ct = default);
}

public class MockSender : INotificationSender
{
    public string Channel => "mock";
    public List<(string To, string Text)> Outbox { get; } = [];
    public Task<bool> SendAsync(string to, string text, CancellationToken ct = default)
    {
        Outbox.Add((to, text));
        return Task.FromResult(true);
    }
}

public class TelegramSender(HttpClient http, string botToken) : INotificationSender
{
    public string Channel => "telegram";
    public async Task<bool> SendAsync(string chatId, string text, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new { chat_id = chatId, text }, ct);
        return res.IsSuccessStatusCode;
    }
}

public class FonnteSender(HttpClient http, string token) : INotificationSender
{
    public string Channel => "fonnte";
    public async Task<bool> SendAsync(string to, string text, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.fonnte.com/send");
        req.Headers.Add("Authorization", token);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["target"] = to,
            ["message"] = text,
        });
        var res = await http.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
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
