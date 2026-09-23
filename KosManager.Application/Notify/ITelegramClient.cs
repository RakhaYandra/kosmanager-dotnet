namespace KosManager.Application.Notify;

public record TelegramUpdate(long UpdateId, string ChatId, string Text);

public interface ITelegramClient
{
    /// <summary>Ambil updates dengan id > offset. Implementasi live memakai
    /// getUpdates; fake memakai antrean script.</summary>
    Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken ct = default);
}
