using KosManager.Application;
using KosManager.Application.Auth;
using KosManager.Application.Billing;
using KosManager.Application.Notify;
using KosManager.Infrastructure.Data;
using KosManager.Infrastructure.Notify;
using KosManager.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KosManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration cfg, string connString, string jwtSecret)
    {
        services.AddDbContext<AppDbContext>(o => o.UseMySql(connString, ServerVersion.AutoDetect(connString)));
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRoomRepository, EfRoomRepository>();
        services.AddScoped<ITenantRepository, EfTenantRepository>();
        services.AddScoped<IBillRepository, EfBillRepository>();
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<INotificationLogRepository, EfNotificationLogRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<Application.Billing.IReceiptService, Pdf.QuestPdfReceiptService>();
        services.AddSingleton<IJwtIssuer>(new JwtIssuer(jwtSecret));
        services.AddScoped<AuthService>();
        services.AddScoped<BillingService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<RoomService>();
        services.AddScoped<TenantService>();
        services.AddSingleton<INotificationSender>(sp => CreateSender(cfg, sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        services.AddHostedService<ReminderService>();
        return services;
    }

    public static INotificationSender CreateSender(IConfiguration cfg, HttpClient http) =>
        cfg["NOTIFY_CHANNEL"] switch
        {
            "telegram" when !string.IsNullOrWhiteSpace(cfg["TELEGRAM_BOT_TOKEN"])
                => new TelegramSender(http, cfg["TELEGRAM_BOT_TOKEN"]!),
            "fonnte" when !string.IsNullOrWhiteSpace(cfg["FONNTE_TOKEN"])
                => new FonnteSender(http, cfg["FONNTE_TOKEN"]!),
            _ => new MockSender(),
        };
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
