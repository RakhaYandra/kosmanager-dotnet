# kosmanager-dotnet

Kos management REST API — **ASP.NET Core 8 + EF Core + MySQL + JWT + BackgroundService**. Single-kos MVP: rooms, tenants, auto-generate tagihan, verifikasi bayar, dashboard tunggakan, reminder Telegram H-3/H-1/H+1. Data contoh fiktif.

## Purpose, Output & Expectations

**Purpose.** Pemilik kos menagih manual via chat: jatuh tempo lupa, tunggakan tak tercatat. KosManager mengganti dengan satu API: kamar, tagihan otomatis, dan reminder terjadwal.

**Output.** API (`:8090`) + JWT 2 role (owner/penghuni), tagihan bulanan auto-generate (tenor tgl-10), reminder Telegram/Fonnte via `INotificationSender`, dashboard tunggakan + Newman 22/22.

## Quickstart 5 menit

```bash
docker run -d --name kos-mysql -e MYSQL_ROOT_PASSWORD=rootpass -e MYSQL_DATABASE=kosmanager \
  -e MYSQL_USER=kos -e MYSQL_PASSWORD=kospass -p 3308:3306 mysql:8.4
dotnet tool install -g dotnet-ef --version 8.0.13
dotnet ef database update --project KosManager.Api
docker exec -i kos-mysql mysql -ukos -pkospass kosmanager < KosManager.Api/seed/seed.sql
dotnet run --project KosManager.Api --urls http://localhost:8090
curl -s localhost:8090/healthz  # {"status":"ok"}
```

Login seed: `owner@kos.local / owner123`, `sinta@kos.local / penghuni123`.

## Contoh curl

```bash
B=localhost:8090
T=$(curl -s -X POST $B/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"owner@kos.local","password":"owner123"}' | cut -d'"' -f4)
A="Authorization: Bearer $T"
curl -s $B/api/dashboard -H "$A"
curl -s -X POST "$B/api/bills/generate?periode=2026-10" -H "$A"
npx newman run KosManager.Api/api/postman_collection.json --env-var baseUrl=http://localhost:8090
```

## Secrets (repo publik, tanpa deploy)

Token nyata tidak pernah masuk git: `user-secrets` lokal + `.env.example` placeholder + fail-fast di `Program.cs` + CI secret dummy + mock sender. Lihat `.env.example`.

## Struktur

`Controllers/` (auth, rooms, tenants, bills, payments, dashboard, notify) · `Models/` · `Data/` (DbContext + EF migrations) · `Services/` (notify interface + Telegram/Fonnte/mock + ReminderService) · `seed/seed.sql` · `api/postman_collection.json`.
