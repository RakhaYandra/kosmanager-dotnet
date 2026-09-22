using System.Text;
using System.Text.RegularExpressions;
using KosManager.Application.Cache;
using KosManager.Domain;

namespace KosManager.Application.Billing;

public record BillDto(int Id, int TenantId, string Tenant, string Period, decimal Amount, DateOnly DueDate, string Status, int DaysLate);
public record DashboardDto(object Occupancy, decimal Kas, decimal Tunggakan, List<OverdueDto> Overdue, int Reminders);
public record OverdueDto(string Tenant, decimal Amount, DateOnly DueDate, int DaysLate);

public class BillingService(IBillRepository bills, ITenantRepository tenants, CacheHelper cache)
{
    public async Task<int> GenerateAsync(string periode, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(periode ?? "", @"^\d{4}-\d{2}$"))
            throw new Auth.BadRequestException("periode format YYYY-MM");
        var due = new DateOnly(int.Parse(periode[..4]), int.Parse(periode[5..]), 10);
        var made = 0;
        foreach (var t in await tenants.WithRoomAsync(ct))
        {
            if (await bills.ExistsAsync(t.Id, periode, ct)) continue;
            await bills.AddAsync(new Bill
            {
                TenantId = t.Id, Period = periode, Amount = t.Room!.MonthlyPrice, DueDate = due,
            }, ct);
            made++;
        }
        await bills.SaveAsync(ct);
        cache.InvalidatePrefix("bills");
        cache.InvalidatePrefix("dash");
        return made;
    }

    public async Task<List<BillDto>> ListAsync(string? status, int? tenantId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(CacheKeys.Bills(tenantId, status ?? ""), async () =>
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return (await bills.ListAsync(status, tenantId, ct))
                .Select(b => new BillDto(b.Id, b.TenantId, b.Tenant!.Name, b.Period, b.Amount, b.DueDate, b.Status,
                    today.DayNumber - b.DueDate.DayNumber))
                .ToList();
        }, TimeSpan.FromSeconds(30));
    }

    public Task<int?> TenantIdByUserAsync(int userId, CancellationToken ct = default) =>
        tenants.TenantIdByUserAsync(userId, ct);
}

public class PaymentService(IBillRepository bills, IPaymentRepository payments, ITenantRepository tenants, CacheHelper cache)
{
    public async Task<Payment> CreateAsync(int billId, string method, string? proofPath, int userId, bool isOwner, CancellationToken ct = default)
    {
        var bill = await bills.ByIdAsync(billId, ct) ?? throw new Auth.NotFoundException("tagihan tidak ada");
        if (!isOwner && !await tenants.BelongsToUserAsync(bill.TenantId, userId, ct))
            throw new Auth.ForbiddenException("bukan tagihanmu");
        var p = new Payment { BillId = billId, Method = method, ProofPath = proofPath };
        await payments.AddAsync(p, ct);
        bill.Status = BillStatuses.Pending;
        await bills.SaveAsync(ct);
        await payments.SaveAsync(ct);
        cache.InvalidatePrefix("bills");
        cache.InvalidatePrefix("dash");
        cache.InvalidatePrefix("queue");
        return p;
    }

    public async Task<Payment> VerifyAsync(int id, bool approve, int verifierId, CancellationToken ct = default)
    {
        var p = await payments.ByIdWithBillAsync(id, ct) ?? throw new Auth.NotFoundException("pembayaran tidak ada");
        p.Verified = approve;
        p.VerifiedBy = verifierId;
        p.Bill!.Status = approve ? BillStatuses.Paid : BillStatuses.Unpaid;
        await payments.SaveAsync(ct);
        await bills.SaveAsync(ct);
        cache.InvalidatePrefix("bills");
        cache.InvalidatePrefix("dash");
        cache.InvalidatePrefix("queue");
        return p;
    }

    public Task<List<Payment>> QueueAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CacheKeys.Queue, () => payments.UnverifiedQueueAsync(ct), TimeSpan.FromSeconds(30));
}

public class DashboardService(IBillRepository bills, IRoomRepository rooms, INotificationLogRepository logs, CacheHelper cache)
{
    public Task<DashboardDto> GetAsync(string periode, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CacheKeys.Dashboard(periode), () => BuildAsync(periode, ct), TimeSpan.FromSeconds(60));

    private async Task<DashboardDto> BuildAsync(string periode, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var list = await bills.ByPeriodAsync(periode, ct);
        var overdue = list
            .Where(b => b.Status != BillStatuses.Paid && b.DueDate < today)
            .Select(b => new OverdueDto(b.Tenant!.Name, b.Amount, b.DueDate, today.DayNumber - b.DueDate.DayNumber))
            .OrderByDescending(x => x.DaysLate)
            .ToList();
        return new DashboardDto(
            new { filled = await rooms.CountFilledAsync(ct), total = await rooms.CountAsync(ct) },
            list.Where(b => b.Status == BillStatuses.Paid).Sum(b => b.Amount),
            list.Where(b => b.Status != BillStatuses.Paid).Sum(b => b.Amount),
            overdue,
            await logs.CountSentAsync(ct));
    }

    public async Task<string> ReportCsvAsync(string periode, CancellationToken ct = default)
    {
        var sb = new StringBuilder("penghuni,periode,nominal,jatuh_tempo,status\n");
        foreach (var b in await bills.ByPeriodAsync(periode, ct))
            sb.AppendLine($"{b.Tenant!.Name},{b.Period},{b.Amount},{b.DueDate:yyyy-MM-dd},{b.Status}");
        return sb.ToString();
    }
}

public class RoomService(IRoomRepository rooms, ITenantRepository tenants, CacheHelper cache)
{
    public Task<object> ListAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<object>(CacheKeys.Rooms, async () =>
        {
            var names = await tenants.NamesByRoomAsync(ct);
            return (await rooms.ListAsync(ct)).Select(r => new
            {
                r.Id, r.Number, r.Type, r.MonthlyPrice, r.Status,
                tenant = names.GetValueOrDefault(r.Id),
            }).ToList();
        }, TimeSpan.FromSeconds(60));

    public async Task<Room> CreateAsync(Room input, CancellationToken ct = default)
    {
        await rooms.AddAsync(input, ct);
        await rooms.SaveAsync(ct);
        EvictCatalog();
        return input;
    }

    public async Task<Room> UpdateAsync(int id, Room input, CancellationToken ct = default)
    {
        var r = await rooms.ByIdAsync(id, ct) ?? throw new Auth.NotFoundException("kamar tidak ada");
        r.Number = input.Number; r.Type = input.Type; r.MonthlyPrice = input.MonthlyPrice; r.Status = input.Status;
        await rooms.SaveAsync(ct);
        EvictCatalog();
        return r;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var r = await rooms.ByIdAsync(id, ct) ?? throw new Auth.NotFoundException("kamar tidak ada");
        await rooms.RemoveAsync(r, ct);
        await rooms.SaveAsync(ct);
        EvictCatalog();
    }

    private void EvictCatalog()
    {
        cache.InvalidatePrefix("rooms");
        cache.InvalidatePrefix("tenants");
        cache.InvalidatePrefix("dash");
        cache.InvalidatePrefix("bills");
    }
}

public class TenantService(ITenantRepository tenants, IRoomRepository rooms, CacheHelper cache)
{
    public Task<List<Tenant>> ListAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CacheKeys.Tenants, () => tenants.ListWithRoomAsync(ct), TimeSpan.FromSeconds(60));

    public async Task<Tenant> CreateAsync(Tenant input, CancellationToken ct = default)
    {
        await tenants.AddAsync(input, ct);
        await SyncRoomAsync(input, ct);
        await tenants.SaveAsync(ct);
        await rooms.SaveAsync(ct);
        EvictCatalog();
        return input;
    }

    public async Task<Tenant> UpdateAsync(int id, Tenant input, CancellationToken ct = default)
    {
        var t = await tenants.ByIdAsync(id, ct) ?? throw new Auth.NotFoundException("penghuni tidak ada");
        t.Name = input.Name; t.Phone = input.Phone; t.TelegramChatId = input.TelegramChatId;
        t.RoomId = input.RoomId; t.MoveInDate = input.MoveInDate;
        await SyncRoomAsync(t, ct);
        await tenants.SaveAsync(ct);
        await rooms.SaveAsync(ct);
        EvictCatalog();
        return t;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var t = await tenants.ByIdAsync(id, ct) ?? throw new Auth.NotFoundException("penghuni tidak ada");
        await tenants.RemoveAsync(t, ct);
        await tenants.SaveAsync(ct);
        EvictCatalog();
    }

    public async Task<(int Imported, int Failed)> ImportAsync(Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv);
        var ok = 0; var failed = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            var parts = line.Split(',');
            if (parts.Length < 3) { failed++; continue; }
            await tenants.AddAsync(new Tenant
            {
                Name = parts[0].Trim(), Phone = parts[1].Trim(),
                MoveInDate = DateOnly.Parse(parts[2].Trim()),
            }, ct);
            ok++;
        }
        await tenants.SaveAsync(ct);
        EvictCatalog();
        return (ok, failed);
    }

    private void EvictCatalog()
    {
        cache.InvalidatePrefix("tenants");
        cache.InvalidatePrefix("rooms");
        cache.InvalidatePrefix("dash");
        cache.InvalidatePrefix("bills");
    }

    private async Task SyncRoomAsync(Tenant t, CancellationToken ct)
    {
        if (t.RoomId is null) return;
        var room = await rooms.ByIdAsync(t.RoomId.Value, ct);
        if (room is not null) room.Status = RoomStatuses.Isi;
    }
}
