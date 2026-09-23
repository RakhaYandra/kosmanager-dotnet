using System.Text.Json;
using System.Net.Http.Json;
using KosManager.Application.Notify;

namespace KosManager.Infrastructure.Notify;

public class HttpTelegramClient(HttpClient http, string botToken) : ITelegramClient
{
    public async Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken ct = default)
    {
        var res = await http.GetAsync(
            $"https://api.telegram.org/bot{botToken}/getUpdates?offset={offset}&timeout=5", ct);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var list = new List<TelegramUpdate>();
        foreach (var u in doc.GetProperty("result").EnumerateArray())
        {
            if (!u.TryGetProperty("message", out var m)) continue;
            list.Add(new TelegramUpdate(
                u.GetProperty("update_id").GetInt64(),
                m.GetProperty("chat").GetProperty("id").GetRawText().Trim('"'),
                m.TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""));
        }
        return list;
    }
}

/// <summary>Fake deterministik untuk verifikasi lokal/CI: membaca antrean
/// updates dari file JSON (TELEGRAM_FAKE_FILE). Tanpa token asli.
/// Semantik offset meniru API asli: pesan gagal diproses akan dikirim ulang
/// (tidak dihapus dari antrean).</summary>
public class FakeTelegramClient : ITelegramClient
{
    private readonly List<TelegramUpdate> _all = [];

    public FakeTelegramClient(string? fakeFile)
    {
        if (fakeFile is not null && File.Exists(fakeFile))
        {
            var doc = JsonDocument.Parse(File.ReadAllText(fakeFile));
            foreach (var u in doc.RootElement.EnumerateArray())
                _all.Add(new TelegramUpdate(
                    u.GetProperty("update_id").GetInt64(),
                    u.GetProperty("chat_id").GetString()!,
                    u.GetProperty("text").GetString()!));
        }
    }

    public Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken ct = default) =>
        Task.FromResult(_all.Where(u => u.UpdateId >= offset).OrderBy(u => u.UpdateId).ToList());
}
