using KosManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KosManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Room>().HasIndex(r => r.Number).IsUnique();
        b.Entity<Room>().Property(r => r.MonthlyPrice).HasPrecision(14, 2);
        b.Entity<Bill>().Property(x => x.Amount).HasPrecision(14, 2);
        b.Entity<Bill>().HasIndex(x => new { x.TenantId, x.Period }).IsUnique();
    }
}
