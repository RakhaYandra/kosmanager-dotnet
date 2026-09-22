using KosManager.Application;
using KosManager.Domain;
using KosManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Infrastructure.Data;

public class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> ByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
    public async Task<User?> ByIdAsync(int id, CancellationToken ct = default) =>
        await db.Users.FindAsync([id], ct);
    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class EfRoomRepository(AppDbContext db) : IRoomRepository
{
    public Task<List<Room>> ListAsync(CancellationToken ct = default) =>
        db.Rooms.AsNoTracking().OrderBy(r => r.Number).ToListAsync(ct);
    public async Task<Room?> ByIdAsync(int id, CancellationToken ct = default) =>
        await db.Rooms.FindAsync([id], ct);
    public async Task AddAsync(Room room, CancellationToken ct = default) =>
        await db.Rooms.AddAsync(room, ct);
    public Task RemoveAsync(Room room, CancellationToken ct = default)
    {
        db.Rooms.Remove(room);
        return Task.CompletedTask;
    }
    public Task<int> CountAsync(CancellationToken ct = default) => db.Rooms.AsNoTracking().CountAsync(ct);
    public Task<int> CountFilledAsync(CancellationToken ct = default) =>
        db.Rooms.AsNoTracking().CountAsync(r => r.Status == RoomStatuses.Isi, ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class EfTenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<List<Tenant>> ListWithRoomAsync(CancellationToken ct = default) =>
        db.Tenants.AsNoTracking().Include(t => t.Room).OrderBy(t => t.Name).ToListAsync(ct);
    public Task<List<Tenant>> WithRoomAsync(CancellationToken ct = default) =>
        db.Tenants.AsNoTracking().Include(t => t.Room).Where(t => t.RoomId != null).ToListAsync(ct);
    public async Task<Tenant?> ByIdAsync(int id, CancellationToken ct = default) =>
        await db.Tenants.FindAsync([id], ct);
    public Task<int?> TenantIdByUserAsync(int userId, CancellationToken ct = default) =>
        db.Tenants.AsNoTracking().Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
    public Task<bool> BelongsToUserAsync(int tenantId, int userId, CancellationToken ct = default) =>
        db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId && t.UserId == userId, ct);
    public async Task AddAsync(Tenant tenant, CancellationToken ct = default) =>
        await db.Tenants.AddAsync(tenant, ct);
    public Task RemoveAsync(Tenant tenant, CancellationToken ct = default)
    {
        db.Tenants.Remove(tenant);
        return Task.CompletedTask;
    }
    public async Task<Dictionary<int, string>> NamesByRoomAsync(CancellationToken ct = default) =>
        await db.Tenants.AsNoTracking().Where(t => t.RoomId != null)
            .ToDictionaryAsync(t => t.RoomId!.Value, t => t.Name, ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class EfBillRepository(AppDbContext db) : IBillRepository
{
    public Task<List<Bill>> ListAsync(string? status, int? tenantId, CancellationToken ct = default)
    {
        var q = db.Bills.AsNoTracking().Include(b => b.Tenant).AsQueryable();
        if (tenantId is not null) q = q.Where(b => b.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(b => b.Status == status);
        return q.OrderByDescending(b => b.Period).ToListAsync(ct);
    }
    public Task<List<Bill>> OpenAsync(CancellationToken ct = default) =>
        db.Bills.Include(b => b.Tenant)
            .Where(b => b.Status == BillStatuses.Unpaid || b.Status == BillStatuses.Pending)
            .ToListAsync(ct);
    public Task<List<Bill>> ByPeriodAsync(string period, CancellationToken ct = default) =>
        db.Bills.AsNoTracking().Include(b => b.Tenant).Where(b => b.Period == period).ToListAsync(ct);
    public async Task<Bill?> ByIdAsync(int id, CancellationToken ct = default) =>
        await db.Bills.FindAsync([id], ct);
    public Task<Bill?> ByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        db.Bills.Include(b => b.Tenant).ThenInclude(t => t!.Room)
            .AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
    public Task<bool> ExistsAsync(int tenantId, string period, CancellationToken ct = default) =>
        db.Bills.AsNoTracking().AnyAsync(b => b.TenantId == tenantId && b.Period == period, ct);
    public async Task AddAsync(Bill bill, CancellationToken ct = default) =>
        await db.Bills.AddAsync(bill, ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class EfPaymentRepository(AppDbContext db) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken ct = default) =>
        await db.Payments.AddAsync(payment, ct);
    public Task<Payment?> ByIdWithBillAsync(int id, CancellationToken ct = default) =>
        db.Payments.Include(p => p.Bill).FirstOrDefaultAsync(p => p.Id == id, ct);
    public Task<List<Payment>> UnverifiedQueueAsync(CancellationToken ct = default) =>
        db.Payments.AsNoTracking().Include(p => p.Bill).ThenInclude(b => b!.Tenant)
            .Where(p => !p.Verified).OrderBy(p => p.CreatedAt).ToListAsync(ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class EfNotificationLogRepository(AppDbContext db) : INotificationLogRepository
{
    public async Task AddAsync(NotificationLog log, CancellationToken ct = default) =>
        await db.NotificationLogs.AddAsync(log, ct);
    public Task<int> CountSentAsync(CancellationToken ct = default) =>
        db.NotificationLogs.AsNoTracking().CountAsync(l => l.Status == "sent", ct);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
