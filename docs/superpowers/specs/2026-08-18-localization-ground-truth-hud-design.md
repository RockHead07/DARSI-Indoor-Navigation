# Localization Ground-Truth HUD — Design

**Konteks:** Pak Amma (dosen pembimbing) minta bukti validasi akurasi VPS: bandingkan
posisi/jarak yang dihitung MultiSet vs pengukuran meteran asli di lapangan. Uji lapangan
di Lantai 10 HCM. Dibutuhkan tampilan admin-only di atas AR supaya angka mentahnya bisa
dibaca live saat testing.

## Metodologi (disepakati lewat brainstorming, 2026-08-18)

Gabungan dua uji terpisah — jangan dicampur jadi satu kesimpulan:

1. **Repeatability** — berdiri di POI 1, set jadi titik 0. Jalan ke POI lain, balik lagi
   ke POI 1 (titik fisik yang SAMA). Kalau HUD balik dekat ke (0,0), VPS konsisten.
2. **Akurasi** — jarak yang ditampilkan HUD antara dua titik dibandingkan ke jarak asli
   hasil ukur meteran. Selisihnya = offset akurasi.

**Bukan cuma satu kali jalan.** Ulangi loop yang sama minimal 3-5x di satu pasang POI
sebelum dirata-rata — satu kali jalan cuma satu titik data, bukan angka yang bisa
dipertanggungjawabkan. Kalau waktu di lapangan masih ada, tambah satu pasang POI lagi di
lokasi berbeda. Saat dilaporkan ke Pak Amma, sebutkan jumlah percobaan (mis. "rata-rata
dari N=5"), jangan angka tunggal telanjang.

## Fitur yang dibangun

**`LocalizationDebugHUD.cs`** (baru, `Assets/Scripts/`):

- Gerbang admin: **5x tap cepat (dalam ~2 detik) di logo DARSI** di HUD. Toggle status
  tersimpan di `PlayerPrefs` (persisten antar buka-tutup app selama sesi testing).
- Tombol **"Set Titik 0"** (cuma muncul saat mode admin aktif) — merekam posisi kamera
  AR saat ini (map-space X, Z) sebagai referensi.
- Teks HUD live (saat admin aktif): `Δx`, `Δz`, jarak dari titik 0 (magnitude), dan
  confidence localize saat ini.
- Posisi user diambil dari `Camera.main`/`arCamera` — pola yang sama dengan
  `FloorVisibilityManager.cs`, bukan cara baru.

**Sengaja TIDAK dibangun** (YAGNI untuk kebutuhan besok):
- Logging otomatis ke file — angka dibaca live, dicatat manual ke dokumen lapangan.
- Kalkulator offset di dalam app — perbandingan ke meteran dihitung manual di luar app.
- PIN/auth tambahan di gerbang admin — device testing tim sendiri, bukan aplikasi publik.

## Verifikasi

Build & jalan di device, aktifkan mode admin (5x tap logo), pastikan tombol "Set Titik 0"
dan teks Δx/Δz/jarak/confidence muncul dan update live saat jalan.
