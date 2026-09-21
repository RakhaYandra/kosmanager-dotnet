namespace KosManager.Api.Services;

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

public static class NotifyFactory
{
    // Driver dipilih via env NOTIFY_CHANNEL=telegram|fonnte|mock (default mock = aman).
    public static INotificationSender Create(IConfiguration cfg, HttpClient http)
    {
        return cfg["NOTIFY_CHANNEL"] switch
        {
            "telegram" when !string.IsNullOrWhiteSpace(cfg["TELEGRAM_BOT_TOKEN"])
                => new TelegramSender(http, cfg["TELEGRAM_BOT_TOKEN"]!),
            "fonnte" when !string.IsNullOrWhiteSpace(cfg["FONNTE_TOKEN"])
                => new FonnteSender(http, cfg["FONNTE_TOKEN"]!),
            _ => new MockSender(),
        };
    }
}
