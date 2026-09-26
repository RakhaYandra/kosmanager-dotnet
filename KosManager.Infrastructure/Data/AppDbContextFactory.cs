using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KosManager.Infrastructure.Data;

/// Design-time DbContext untuk `dotnet ef` (migrate, script, dbcontext optimize).
///
/// Alasan: Program.cs menolak start tanpa JWT_SECRET (fail-fast, lihat appsettings.json
/// yang sengaja dikosongkan). Tanpa factory ini, `dotnet ef` ikut membangun host
/// dan gagal dengan pesan "Missing config: JWT_SECRET" yang menyesatkan — padahal
/// migrasi tidak butuh signing key sama sekali.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DB_CONN");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(
                "DB_CONN wajib diisi untuk operasi design-time (migrate/script). "
                + "Contoh: server=localhost;port=3308;database=kosmanager;user=kos;password=...");
        }

        var o = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(conn, ServerVersion.AutoDetect(conn))
            .Options;

        return new AppDbContext(o);
    }
}
