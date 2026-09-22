# ADR-002: Migrasi ke Clean Architecture-lite

Tanggal: 2026-09-22. Status: diterima.

## Konteks

MVP KosManager dibangun 1 hari dengan arsitektur lapis sederhana
(`Controllers/` langsung memakai EF `DbContext`, logika bisnis di controller).
Pola ini sama seperti Shiftbase/LifeOS dan benar untuk kecepatan MVP,
tetapi tidak mencerminkan pengalaman freelance (Service-COP, .NET enterprise
7 proyek: Base/Domain/Shared/Application/Infrastructure/WebApi/gRPC).

## Keputusan

Migrasi ke 4 proyek (lite, tanpa gRPC dan tanpa DTO terpisah):

- `KosManager.Domain` — entitas murni + konstanta status/role. Nol dependensi.
- `KosManager.Application` — interface repository, use-case
  (Auth/Billing/Payment/Dashboard/Room/Tenant services),
  `INotificationSender` + template. Hanya BCL + `System.Net.Http.Json`.
- `KosManager.Infrastructure` — `AppDbContext`, implementasi repository,
  `JwtIssuer`, `BCryptPasswordHasher`, sender Telegram/Fonnte/mock,
  `ReminderService`, `AddInfrastructure()`.
- `KosManager.Api` — controller tipis (tanpa `using` EntityFramework —
  diverifikasi via grep), `Program.cs` hanya wiring, exception mapper.

Aturan: dependensi menunjuk ke dalam (Api → Infrastructure → Application → Domain).
Kontrak JSON tidak berubah (FE nol perubahan, Newman 22/22 hijau sebelum/sesudah).

## Konsekuensi

- Riwayat migrasi EF lama dibuang (DEV only); migrasi regen di Infrastructure.
- Entitas dipakai sebagai kontrak API (tanpa DTO) — tech debt tercatat,
 acceptable untuk MVP; DTO menyusul bila konsumen bertambah.
- `FrameworkReference Microsoft.AspNetCore.App` di classlib sengaja dihindari;
  sebagai gantinya `IJwtIssuer`/`IPasswordHasher` (injeksi abstraksi, bukan framework).
