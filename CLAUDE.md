# CLAUDE.md — DARSI Indoor Navigation (Unity repo)

Baca `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/FLOWS.md`, dan `docs/ROADMAP.md` dulu sebelum mengerjakan apapun di repo ini — jangan asumsikan konteks dari training data, semua keputusan arsitektur project ini sudah dikunci di file-file tersebut.

## Ringkasan super singkat

DARSI adalah fitur AR indoor navigation untuk RS Islam A. Yani, di-embed ke app **MyRSIy** (Flutter) lewat **Unity as a Library (UaaL)**. Repo ini HANYA menangani AR Navigation, Voice Input, dan Cari Teman (render posisi teman dari friendlist) — semua UI sebelum AR (Home, Cari Lokasi, kelola friendlist) ada di repo terpisah (`DARSI-WebView`, Next.js) dan TIDAK dikerjakan di sini.

## Yang WAJIB diingat

- **Unity TIDAK punya splash/login/home screen internal lagi.** Sudah dihapus (lihat ADR-003 di `DECISIONS.md`). AR Canvas aktif langsung saat Unity di-launch dari Flutter.
- **UI Toolkit sudah tidak dipakai sama sekali.** Semua UI di repo ini pakai uGUI. Jangan tambah UIDocument/UXML/USS baru kecuali ada keputusan arsitektur baru yang mencabut ADR-003.
- **Jarak/posisi hanya valid setelah MultiSet localize berhasil.** Jangan buat fitur apapun yang menampilkan posisi user di luar sesi AR aktif — itu keputusan sadar (ADR-007, ADR-011), bukan kelalaian.
- **Cari Teman TIDAK BOLEH jadi auto-discovery/radar.** Wajib friend-request + mutual accept, dikelola dari WebView (bukan pairing-code lagi). Ini keputusan privasi/keamanan yang sudah divalidasi riset (ADR-010, ADR-013) — jangan disederhanakan jadi "tampilkan semua yang online" atau bikin direktori user terbuka meski itu tampak lebih mudah diimplementasikan.
- **Belum ada entry point UaaL.** `UaaLEntryPoint.cs` (nama sementara) yang menerima data dari Flutter belum dibuat — ini prioritas kerja berikutnya (lihat `docs/ROADMAP.md` Fase 1). Lihat `docs/INTEGRATION.md` untuk kontrak payload-nya.

## Script existing yang JANGAN diubah tanpa alasan kuat

`VoiceInputHandler.cs`, `OllamaConnector.cs`, `POIManager.cs`, `POIData.cs`, `NavigationAdapter.cs`, `VoiceUIController.cs`, `VoiceUIConfig.cs`, `PhotonManager.cs`, `PlayerSync.cs`, `FriendListPanel.cs`, `FriendListEntry.cs`, `PlayerInfoPopup.cs`, `NavMeshObstacleHelper.cs` — semua sudah jalan dan sudah divalidasi tidak ada compile error pasca cleanup UI Toolkit.

## Alur kerja yang diharapkan

1. Setiap task besar: baca ulang `docs/DECISIONS.md` dulu — cek apakah sudah ada ADR yang relevan sebelum mengusulkan pendekatan baru.
2. Jangan commit tanpa review dari pemilik project (Bagus).
3. Kalau menemukan kebutuhan yang memaksa keputusan arsitektur berubah, tandai eksplisit ke pemilik project — jangan diam-diam menyimpang dari `ARCHITECTURE.md`.

## Tech stack

Unity 6000.3.14f1 · MultiSet SDK v1.11.5 · ARCore/ARFoundation · Photon PUN 2 · Ollama + qwen3:8b (voice) · target Android.
