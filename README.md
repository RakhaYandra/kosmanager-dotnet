# kosmanager-dotnet

[![ci](https://github.com/RakhaYandra/kosmanager-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/RakhaYandra/kosmanager-dotnet/actions)

> Ekosistem: [api](https://github.com/RakhaYandra/kosmanager-dotnet) · [web](https://github.com/RakhaYandra/kosmanager-dotnet-web) · [docs](https://github.com/RakhaYandra/kosmanager-dotnet-docs/releases) · [qa](https://github.com/RakhaYandra/kosmanager-dotnet-qa) · [data](https://github.com/RakhaYandra/kosmanager-dotnet-data) · [ops](https://github.com/RakhaYandra/kosmanager-dotnet-ops)

Kos management REST API — **ASP.NET Core 8 + EF Core + MySQL + JWT + BackgroundService**. Single-kos MVP: rooms, tenants, auto-generate tagihan, verifikasi bayar, dashboard tunggakan, reminder Telegram H-3/H-1/H+1. Data contoh fiktif.

## Purpose, Output & Expectations

**Purpose.** Pemilik kos menagih manual via chat: jatuh tempo lupa, tunggakan tak tercatat. KosManager mengganti dengan satu API: kamar, tagihan otomatis, dan reminder terjadwal.

**Output.** API (`:8090`) + JWT 2 role (owner/penghuni), tagihan bulanan auto-generate (tenor tgl-10), reminder Telegram/Fonnte via `INotificationSender`, dashboard tunggakan + tren kas + struk PDF + Newman 25/25 (digate CI repo qa).

## Quickstart 5 menit

`JWT_SECRET` **wajib** diisi lewat env — `appsettings.json` sengaja dikosongkan
agar app gagal cepat (`Missing config: JWT_SECRET`) alih-alih jalan dengan
kunci tanda tangan yang ada di repo. Salin `.env.example` untuk nilai contoh.

```bash
# 1. MySQL
docker run -d --name kos-mysql -e MYSQL_ROOT_PASSWORD=rootpass -e MYSQL_DATABASE=kosmanager \
  -e MYSQL_USER=kos -e MYSQL_PASSWORD=kospass -p 3308:3306 mysql:8.4

# 2. Migrasi + seed
# `dotnet ef` memakai AppDbContextFactory, jadi tidak butuh JWT_SECRET —
# tapi tetap butuh DB_CONN di env.
export DB_CONN="server=localhost;port=3308;database=kosmanager;user=kos;password=kospass"
dotnet tool install -g dotnet-ef --version 8.0.13
dotnet ef database update --project KosManager.Infrastructure --startup-project KosManager.Api
docker exec -i kos-mysql mysql -ukos -pkospass kosmanager < KosManager.Api/seed/seed.sql

# 3. Jalankan (JWT_SECRET wajib, min 32 karakter)
export JWT_SECRET="$(openssl rand -hex 32)"
dotnet run --project KosManager.Api --urls http://localhost:8090

curl -s localhost:8090/healthz  # {"status":"ok"}
```

Alternatif tanpa SDK lokal — compose di repo `ops` menangani MySQL + API:
`cp ../ops/repo/.env.example ../ops/repo/.env` lalu `docker compose up -d`.

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
| Dashboard | Okupansi, kas bulan ini, tunggakan + hari telat, reminders terkirim; export CSV; tren kas 6 bulan. |
| Notify | Test-kirim + scheduler H-3/H-1/H+1 via `INotificationSender` (Telegram/Fonnte/mock). |
| Inbound | `TelegramPollingService` (getUpdates/10 dtk, offset file): `/start <email>` tautkan chat, `SUDAH` catat pending. Mode `TELEGRAM_POLLING=off|fake|live` (lihat ADR-005). |
| Struk | `GET /api/bills/{id}/receipt.pdf` (QuestPDF, stempel LUNAS/BELUM; owner atau miliknya). |

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
Newman (butuh API + DB + seed jalan): `npx newman run KosManager.Api/api/postman_collection.json --env-var baseUrl=http://localhost:8090` → 25/25 (gate CI: `qa.yml` assert total == 25 & failed == 0).

## Lisensi dependensi

QuestPDF dipakai di bawah **Community License** (gratis untuk individu + proyek open-source MIT seperti repo ini; syarat: `LicenseType.Community`, tanpa key). Lihat questpdf.com/license. Alternatif murni-MIT (PdfSharpCore) sengaja tidak dipilih karena API layout manual.

## Cache (baca tanpa ke DB tiap request)

- Lapis 0: `AsNoTracking()` di semua query read murni (tanpa risiko stale;
  query yang hasilnya dimodifikasi — ById update, scheduler sweep, verify —
  tetap tracked).
- Lapis 1: `IMemoryCache` + `CacheHelper` (registrasi key per prefix,
  invalidasi deterministik). TTL: dashboard 60 dtk, rooms/tenants 60 dtk,
  bills/queue 30 dtk. Key milik penghuni selalu bawa tenant id (anti-bocor).
  Semua endpoint tulis meng-invalidate prefix terkait (terverifikasi:
  create room → list langsung berubah).
- Observabilitas: MiniProfiler (`/profiler/results`) + log hit/miss (Debug).

## Dev & CI

Build/migrasi/seed: `dotnet build KosManager.Api` · `dotnet ef migrations add` di `KosManager.Infrastructure` · `seed/seed.sql`.
CI (`.github/workflows/ci.yml`): MySQL service → build → migrate → seed → boot → Newman.

Keputusan arsitektur: [ADR-002](docs/ADR-002-clean-architecture.md) (Clean Architecture-lite).

## Secrets (repo publik, tanpa deploy)

Token nyata tidak pernah masuk git: `user-secrets` lokal + `.env.example` placeholder + fail-fast di `Program.cs` + CI secret dummy + mock sender. Lihat `.env.example`.

## Struktur (Clean Architecture-lite, lihat `docs/ADR-002-clean-architecture.md`)

`KosManager.Domain/` (entitas murni) · `KosManager.Application/` (interface repo + use-case + notify) · `KosManager.Infrastructure/` (EF, repo impl, JWT, sender, scheduler) · `KosManager.Api/` (controller tipis, tanpa EF using) · `seed/seed.sql` · `api/postman_collection.json`.
