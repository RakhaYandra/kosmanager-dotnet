using KosManager.Domain;

namespace KosManager.Application;

public interface IUserRepository
{
    Task<User?> ByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> ByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface IRoomRepository
{
    Task<List<Room>> ListAsync(CancellationToken ct = default);
    Task<Room?> ByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task RemoveAsync(Room room, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountFilledAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface ITenantRepository
{
    Task<List<Tenant>> ListWithRoomAsync(CancellationToken ct = default);
    Task<List<Tenant>> WithRoomAsync(CancellationToken ct = default);
    Task<Tenant?> ByIdAsync(int id, CancellationToken ct = default);
    Task<int?> TenantIdByUserAsync(int userId, CancellationToken ct = default);
    Task<bool> BelongsToUserAsync(int tenantId, int userId, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task RemoveAsync(Tenant tenant, CancellationToken ct = default);
    Task<Dictionary<int, string>> NamesByRoomAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface IBillRepository
{
    Task<List<Bill>> ListAsync(string? status, int? tenantId, CancellationToken ct = default);
    Task<List<Bill>> OpenAsync(CancellationToken ct = default);
    Task<List<Bill>> ByPeriodAsync(string period, CancellationToken ct = default);
    Task<Bill?> ByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsAsync(int tenantId, string period, CancellationToken ct = default);
    Task AddAsync(Bill bill, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task<Payment?> ByIdWithBillAsync(int id, CancellationToken ct = default);
    Task<List<Payment>> UnverifiedQueueAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface INotificationLogRepository
{
    Task AddAsync(NotificationLog log, CancellationToken ct = default);
    Task<int> CountSentAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
