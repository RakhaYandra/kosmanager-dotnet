using KosManager.Infrastructure;
using KosManager.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Konfigurasi via environment (user-secrets lokal, dummy di CI). Fail-fast.
string Req(string key)
{
    var v = builder.Configuration[key];
    if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException($"Missing config: {key}");
    return v;
}
var conn = builder.Configuration["DB_CONN"] ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing config: DB_CONN");
var jwtSecret = Req("JWT_SECRET");

builder.Services.AddHttpClient();
builder.Services.AddInfrastructure(builder.Configuration, conn, jwtSecret);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("owner", p => p.RequireRole("owner"))
    .AddPolicy("penghuni", p => p.RequireRole("penghuni", "owner"));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseDomainExceptions();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.Run();
