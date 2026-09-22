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
dotnet ef database update --project KosManager.Infrastructure --startup-project KosManager.Api
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

## Features

| Fitur | Deskripsi |
|---|---|
| Auth | Register (penghuni saja), login JWT 24h, `/me`. RBAC: owner vs penghuni. |
| Rooms | CRUD kamar + occupancy (kosong/isi + nama penghuni). |
| Tenants | CRUD + CSV import (maks 2MB, lapor imported/failed); ganti kamar → status isi. |
| Bills | Auto-generate bulanan (tenor tgl-10), idempoten per tenant+periode, filter status, daysLate. |
| Payments | Catat manual (tunai/transfer) → pending → verifikasi owner (paid/unpaid) + antrean. |
| Dashboard | Okupansi, kas bulan ini, tunggakan + hari telat, reminders terkirim; export CSV. |
| Notify | Test-kirim + scheduler H-3/H-1/H+1 via `INotificationSender` (Telegram/Fonnte/mock). |

## How It Works

```text
Client (Blazor) → POST /api/auth/login → JWT 24h
JWT → RBAC policy (owner/penghuni) → Use-case (Application) → Repository → MySQL
BillingService.generate → tenor tgl-10, skip bila (tenant,periode) ada
ReminderService (tiap jam) → H-3/H-1/H+1 sekali per tagihan (reminded_stage) → notification_logs
```

## RBAC

| Aksi | owner | penghuni | anon |
|---|---|---|---|
| auth register/login/me | ✅/✅/✅ | ✅/✅/✅ | ✅/✅/❌ |
| rooms CRUD | ✅ | baca saja | ❌ |
| tenants CRUD/import | ✅ | ❌ | ❌ |
| bills | semua | milik sendiri | ❌ |
| bills generate | ✅ | 403 | 401 |
| payments create | ✅ | milik sendiri | ❌ |
| payments verify/queue | ✅ | 403 | ❌ |
| dashboard/report | ✅ | 403 | ❌ |
| notify test | ✅ | 403 | ❌ |

## Coba via Swagger / Newman

Swagger UI: `http://localhost:8090/swagger` (Swashbuckle bawaan).
Newman (butuh API + DB + seed jalan): `npx newman run KosManager.Api/api/postman_collection.json --env-var baseUrl=http://localhost:8090` → 22/22.

## Dev & CI

`dotnet build KosManager.Api` · `dotnet ef migrations add` di `KosManager.Infrastructure` · seed via `seed/seed.sql`.
CI (`.github/workflows/ci.yml`): MySQL service → build → migrate → seed → boot → Newman.

Keputusan arsitektur: [ADR-002](docs/ADR-002-clean-architecture.md) (Clean Architecture-lite).

## Secrets (repo publik, tanpa deploy)

Token nyata tidak pernah masuk git: `user-secrets` lokal + `.env.example` placeholder + fail-fast di `Program.cs` + CI secret dummy + mock sender. Lihat `.env.example`.

## Struktur (Clean Architecture-lite, lihat `docs/ADR-002-clean-architecture.md`)

`KosManager.Domain/` (entitas murni) · `KosManager.Application/` (interface repo + use-case + notify) · `KosManager.Infrastructure/` (EF, repo impl, JWT, sender, scheduler) · `KosManager.Api/` (controller tipis, tanpa EF using) · `seed/seed.sql` · `api/postman_collection.json`.
