# ADR-005: Telegram inbound via polling, bukan webhook

Tanggal: 2026-09-23. Status: diterima.

## Konteks

Butuh perintah masuk (`/start` tautkan chat, `SUDAH` catat bayar) tanpa server
publik/VPS ($0, repo publik, demo lokal).

## Keputusan

`TelegramPollingService` (BackgroundService, tiap 10 dtk) memanggil
`getUpdates` dengan offset persisten di file lokal (gitignore).
Alasan polling atas webhook: tanpa URL publik/HTTPS, tanpa langganan.

## Konsekuensi & pelajaran nyata

- Semantik offset meniru API asli: pesan gagal diproses HARUS dikirim ulang.
  Bug ditemukan saat verifikasi: fake awal menelan antrean saat gagal
  (dequeue destruktif) → diperbaiki jadi filter-by-offset. Pelajaran:
  test double harus meniru semantik kegagalan, bukan hanya sukses.
- `AsNoTracking` (Lapis 0 cache) membuat update chat_id jadi no-op diam-diam;
  write path wajib entity tracked (`ByIdAsync`, bukan list). Pelajaran:
  optimasi read bisa meracuni write — dipisahkan eksplisit.
- Offset basi (file lama) membuat proses sehat terlihat mati total.
  Runbook: selalu hapus offset saat ganti skenario uji.
- [`/start <email>`, `SUDAH`] → pending payment; verifikasi tetap di owner.
  Tanpa token asli: `TELEGRAM_POLLING=fake` + file JSON; 1 slot `verified-real`
  disiapkan untuk token produksi.
