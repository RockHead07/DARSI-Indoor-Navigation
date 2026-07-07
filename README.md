<div align="center">

<img width="240" alt="Image" src="https://github.com/user-attachments/assets/0bb9636b-0e53-4ba7-bfd9-85e1d6d59429" />

<br>

[![Unity](https://img.shields.io/badge/Unity-6000.3.14f1-000000?logo=unity&logoColor=white)](https://unity.com/)
[![MultiSet SDK](https://img.shields.io/badge/MultiSet%20SDK-v1.11.5-2E8B57)](https://www.multiset.ai/)
[![Platform](https://img.shields.io/badge/Platform-Android%20(ARCore)-3DDC84?logo=android&logoColor=white)](https://developers.google.com/ar)
[![License](https://img.shields.io/github/license/RockHead07/DARSI-Indoor-Navigation)](LICENSE)

<h1> DARSI Indoor Navigation </h1>

<p>
  <em>An Hospital Wayfinding Feature for RSI A. Yani.</em>
</p>

<p>
  <strong>Modul <em>AR Indoor Navigation</em> untuk RS Islam A. Yani Surabaya</strong> <br>
  <sub>di-embed sebagai
  satu menu di dalam aplikasi <strong>MyRSIy</strong> (Flutter) lewat <strong>Unity as a Library (UaaL)</strong>.
  Repo ini <strong>BUKAN aplikasi berdiri sendiri</strong>: ia hanya satu dari empat repo yang
  membentuk satu sistem. Lihat [Sistem & Repo Terkait](#sistem--repo-terkait) di bawah
  sebelum menganggap repo ini "<em>yang utama</em>".</sub>
</p>

</div>

## Overview

<div align="center">
	<code><img width="50" src="https://raw.githubusercontent.com/marwin1991/profile-technology-icons/refs/heads/main/icons/c%23.png" alt="C#" title="C#"/></code>
	<code><img width="50" src="https://raw.githubusercontent.com/marwin1991/profile-technology-icons/refs/heads/main/icons/unity.png" alt="Unity" title="Unity"/></code>
</div>

- Engine: Unity 6000.3.14f1
- SDK utama: MultiSet SDK v1.11.5 (VPS/localization) + ARCore/ARFoundation + Photon PUN 2
- Yang dikerjakan **di repo ini saja**: AR Navigation, Voice Input (Ollama), dan Cari Teman
  (render posisi teman dari friendlist — bukan direktori/radar, lihat ADR-013 di
  `docs/DECISIONS.md`)
- Yang **TIDAK** dikerjakan di sini: Home, Cari Lokasi, kelola friendlist — semua UI
  sebelum sesi AR aktif ada di repo WebView terpisah (lihat di bawah)

## Sistem & Repo Terkait

DARSI adalah satu sistem yang terdiri dari **empat repo**, tiga lapisan teknologi,
dalam satu APK Android. Diagram alurnya:

```
My eRSIy CopyCat 🐈‍⬛ (Flutter, native — sudah di Play Store)
│
└── Menu "Navigasi Indoor"
        │
        ▼
    WebView (Next.js) — Home, Cari Lokasi, kelola friendlist
        │
        │  tap "Mulai Navigasi AR"
        ▼
    Flutter meneruskan payload
        │
        ▼
    Unity as a Library (UaaL)  ◄── REPO INI
        │
        ├── AR Canvas aktif langsung (tanpa splash/login internal — ADR-003)
        ├── MultiSet SDK localize → AR Navigation (arrow, path, jarak real)
        ├── Cari Teman (render posisi teman, AR-only)
        └── Voice input (Ollama) · Photon multiplayer
        │
        │  selesai / back
        ▼
    Kembali ke WebView (resume, tidak reload)
```

>[!DISCLAIMER]
>**Ingat** penggunaan implementasi fitur **DARSI Indoor Navigation** masih diterapkan pada aplikasi tiruan My eRSIy!

| Repo | Tech stack | Peran | Kepemilikan |
|---|---|---|---|
| **[My-eRSIy-CopyCat-](https://github.com/RockHead07/My-eRSIy-CopyCat-)** (dummy; produksi nanti di repo MyRSIy asli) | Flutter | Parent app, shell/AppBar native, launcher UaaL, jembatan `postMessage` | KKSoft / Farris (prod) |
| **[DARSI-Indoor-Navigation-UI-WebView](https://github.com/RockHead07/DARSI-Indoor-Navigation-UI-WebView)** | Next.js | UI pre-AR: Home, Cari Lokasi, kelola friendlist (2D, bukan AR) | Bagus |
| **DARSI-Indoor-Navigation** (repo ini) | Unity 6000.3.14f1 + MultiSet SDK + ARCore + Photon PUN 2 | AR Navigation, Voice Input, Cari Teman (render posisi) | Bagus |
| **[DARSI-Indoor-Navigation-Backend](https://github.com/RockHead07/DARSI-Indoor-Navigation-Backend)** | FastAPI (Python) + Postgres (Supabase) | Data POI, friend-request/presence, business logic | Bagus |

**Tidak ada shared database dengan MyRSIy.** MyRSIy punya database sendiri yang
sepenuhnya terpisah; satu-satunya jalur komunikasi DARSI ↔ MyRSIy adalah lewat
jembatan UaaL (`postMessage` / intent extra), bukan query database bersama
(lihat ADR-012).

>[!CAUTION]
> **Baca dulu sebelum mengerjakan apapun di repo ini:** `docs/ARCHITECTURE.md` (source
> of truth arsitektur, LOCKED), `docs/DECISIONS.md` (semua ADR — cek dulu apakah
> keputusan yang mau kamu ubah sudah pernah dikunci), `docs/FLOWS.md`, `docs/ROADMAP.md`,
> dan `docs/INTEGRATION.md` (kontrak payload lintas repo — WAJIB identik dengan
> `API_CONTRACT.md` di repo WebView, ubah satu ubah dua-duanya).

## Struktur Penting

- `Assets/Scripts/` — `UaaLEntryPoint.cs` (entry point dari Flutter), `FloorVisibilityManager.cs`
- `Assets/VoiceInput/` — `POIData.cs`, `POIManager.cs`, `NavigationAdapter.cs`, `NavMeshObstacleHelper.cs`
- `Assets/Speech Recognition/` — `VoiceInputHandler.cs`, `OllamaConnector.cs`, `VoiceUIController.cs`
- `Assets/Scenes/` — scene aktif adalah **`WholePSDKU`** (di bawah folder sample MultiSet),
  bukan `SampleScene`
- `Packages/manifest.json` — daftar paket Unity
- `docs/` — dokumentasi arsitektur & kontrak (lihat kotak di atas)

## Cara Menjalankan

1. Buka Unity Hub → Add proyek ini dari folder repo → gunakan Unity **6000.3.14f1**.
2. Buka scene **`WholePSDKU`** (bukan `SampleScene` — itu tidak lagi dipakai).
3. Tekan Play. AR Canvas aktif langsung (tidak ada splash/login internal — lihat ADR-003
   di `docs/DECISIONS.md`).
4. Untuk pengujian penuh (voice input, sync POI ke backend), ikuti
   [Konfigurasi Sebelum Demo](#konfigurasi-sebelum-demo) di bawah.

>[!NOTE]
> Menjalankan repo ini sendirian hanya menunjukkan sisi AR. Untuk melihat alur penuh
> (Home → Cari Lokasi → AR), jalankan juga repo WebView + Backend + host Flutter — lihat
> tabel repo di atas.

## Catatan Kolaborasi

- Hindari commit folder `Library/` dan `Temp/` (sudah di-`.gitignore`).
- Jika menambah package, commit juga `Packages/manifest.json` dan `Packages/packages-lock.json`.
- Jika menambah scene/asset baru, pastikan file `.meta`-nya ikut ter-commit.
- Script-script yang sudah divalidasi jalan (jangan diubah tanpa alasan kuat — lihat
  `CLAUDE.md`): `VoiceInputHandler.cs`, `OllamaConnector.cs`, `POIManager.cs`, `POIData.cs`,
  `NavigationAdapter.cs`, `VoiceUIController.cs`, `VoiceUIConfig.cs`, `PhotonManager.cs`,
  `PlayerSync.cs`, `FriendListPanel.cs`, `FriendListEntry.cs`, `PlayerInfoPopup.cs`,
  `NavMeshObstacleHelper.cs`.
- Kalau menemukan kebutuhan yang memaksa keputusan arsitektur berubah, tandai eksplisit
  ke pemilik project — jangan diam-diam menyimpang dari `ARCHITECTURE.md`.

## Konfigurasi Sebelum Demo

Langkah yang **harus dilakukan sebelum menjalankan demo**, terutama kalau environment
berubah (misalnya pindah jaringan WiFi).

### 1. Mengubah IP Ollama

File: `Assets/Speech Recognition/OllamaConnector.cs`
Field: `ollamaHost` (bisa diubah via Inspector atau langsung di script)

Ollama berjalan di laptop/PC sebagai server lokal (model **qwen3:8b**). HP Android
berkomunikasi ke Ollama lewat WiFi. Karena kebanyakan jaringan pakai **DHCP** (IP
berubah-ubah), IP laptop bisa berubah tiap kali konek ulang ke WiFi.

**Langkah:**
1. Cek IP laptop saat ini: `ipconfig` (Windows) atau `ifconfig` (Mac/Linux)
2. Cari alamat IPv4 di adapter WiFi (contoh: `192.168.18.150`)
3. Di Unity Inspector, pilih GameObject yang punya komponen `OllamaConnector`
4. Ubah field **Ollama Host** ke IP terbaru
5. Pastikan HP dan laptop berada di **jaringan WiFi yang sama**

### 2. Menjalankan Server Ollama

```bash
# Jalankan model qwen3:8b (otomatis download kalau belum ada)
ollama run qwen3:8b

# Atau jalankan sebagai server background:
ollama serve
```

Pastikan port default `11434` tidak terblokir firewall. Testing cepat:
```bash
curl http://localhost:11434/api/generate -d '{"model":"qwen3:8b","prompt":"test","stream":false}'
```

### 3. Menggunakan Tool Auto Attach POIData

Unity Editor punya tool otomatis untuk menempelkan komponen `POIData` ke semua child
GameObject di bawah root POI.

**Langkah:**
1. Buka Unity Editor
2. Klik menu **Tools > POI > Auto Attach POIData**
3. Tool scan semua children di bawah root POI dan menambahkan `POIData` jika belum ada
4. Isi field `poiName`, `kategori`, dan `sinonim` di tiap `POIData` via Inspector

### 4. Menambahkan POI Baru ke Scene

1. Di Hierarchy, cari/buat GameObject parent **"POIs"** (sesuai `poiRoot` di `POIManager`)
2. Klik kanan "POIs" > **Create Empty** untuk child GameObject baru
3. Beri nama sesuai lokasi (contoh: "BAAK", "Toilet Lt2", "IGD")
4. Posisikan GameObject ke lokasi yang benar di scene
5. **Add Component** > `POIData`
6. Isi field:
   - **Poi Name** — nama resmi POI (kosong = pakai nama GameObject)
   - **Kategori** — harus salah satu dari daftar kanonik di `POI_CATEGORIES`
     (lihat ADR-016 di `docs/DECISIONS.md`) — kategori di luar daftar ditolak backend
     saat sync
   - **Sinonim** — klik **+** untuk alias/nama lain
7. Pastikan komponen **POI** dari MultiSet SDK juga terpasang di GameObject yang sama

Setelah POI ditambah/diubah, sync ke backend lewat **DARSI > Sync POIs to Backend**
(`Assets/Editor/POISyncWindow.cs`) — ini yang membuat data POI muncul di WebView.

### 5. Wiring NavigationAdapter di Inspector

| Field | Yang Harus Di-Assign | Keterangan |
|-------|---------------------|------------|
| **Navigation Controller** | Komponen `NavigationController` dari MultiSet SDK | Memanggil navigasi via SendMessage |
| **Set Poi Method Name** | `SetPOIForNavigation` (default) | Nama method di NavigationController |
| **Navigation UI Controller** | Komponen `NavigationUIController` | Menampilkan progress slider |
| **Start Navigation UI Method Name** | `ClickedStartNavigation` (default) | Nama method di NavigationUIController |
| **Destination Select UI** | Panel UI daftar destinasi (opsional) | Disembunyikan setelah navigasi mulai |
| **On Navigate To Transform/Position/Name** | Wire ke handler yang sesuai | Event alternatif jika tidak pakai SendMessage |
| **On Navigation Failed** | Wire ke UI error handler | Ditampilkan saat navigasi gagal |

>[!TIP]
>Klik kanan komponen `NavigationAdapter` di Inspector > **Validate Wiring** untuk
>mengecek semua referensi sudah benar.

### 6. NavMeshObstacleHelper (Obstacle Dinamis)

`Assets/VoiceInput/NavMeshObstacleHelper.cs` adalah pola untuk obstacle dinamis yang
akan diintegrasikan dengan deteksi kerumunan dari backend YOLO (`/api/human`).

1. Buat GameObject baru untuk merepresentasikan area kerumunan
2. Tambahkan komponen **NavMeshObstacleHelper** (NavMeshObstacle otomatis ditambahkan)
3. Script otomatis mengkonfigurasi carving pada NavMeshObstacle
4. Di masa depan, data YOLO backend memanggil `SetObstacleSize()` dan
   `SetObstacleActive()` untuk update real-time

>[!NOTE]
>Pendekatan NavMeshObstacle carving dipilih karena lebih ringan dibanding full NavMesh
>rebaking, dan cukup akurat untuk bounding box kerumunan.

## Troubleshooting

- Scene kosong/error → reimport project (klik kanan folder proyek > Reimport All).
- Package MultiSet tidak terdownload → cek koneksi dan re-open Unity.
- Payload dari WebView tidak terbaca / POI tidak ketemu → cek `docs/INTEGRATION.md`,
  pastikan field payload sama persis dengan `API_CONTRACT.md` di repo WebView.

## Kontak

Ada pertanyaan? Buat issue di repo ini atau diskusikan lewat chat tim.

#

<div align="center">
<sub>

Happy Coding 🎉

</sub>
