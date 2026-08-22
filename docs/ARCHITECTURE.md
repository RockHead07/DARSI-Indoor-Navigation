# ARCHITECTURE.md — DARSI Indoor Navigation

> Status: LOCKED. Ini adalah source of truth arsitektur project. Perubahan besar harus dicatat di `DECISIONS.md` sebagai ADR baru, bukan langsung mengedit file ini tanpa jejak.

## Ringkasan satu paragraf

DARSI Indoor Navigation adalah fitur AR indoor navigation untuk RS Islam A. Yani Surabaya, dikembangkan sebagai bagian dari Kerja Praktik (pembimbing: Pak Amma, Pak Dhoto). Fitur ini akan di-embed ke dalam aplikasi **MyRSIy** (Flutter, developer: Farris Fardiansyah / KKSoft) sebagai satu widget menu, bukan aplikasi terpisah. Arsitektur menggabungkan tiga lapisan teknologi berbeda dalam satu APK: Flutter native (parent app + shell), WebView/Next.js (UI pre-AR), dan Unity (AR navigation via Unity as a Library).

## Diagram lapisan sistem

```
MyRSIy (Flutter, native — sudah di Play Store)
│
├── Home, Jadwal, Antrian, ... (screen native existing)
│
└── Menu "Navigasi Indoor"
        │
        ▼
    DarsiNavigationScreen (Flutter native)
        │  AppBar hijau custom — konsisten dgn screen "Layanan Unggulan"
        │  (back button, judul, subtitle, ornamen — BUKAN bagian WebView)
        │
        ▼
    WebView (body screen, load URL Next.js)
        │
        ├── Home           ─┐
        └── Cari Lokasi     ┘  browsing, TANPA jarak meter
        │
        │  tap "Mulai Navigasi AR"
        │  postMessage({action:'launchAR', ...})
        ▼
    Flutter menerima postMessage
        │
        ▼
    Unity as a Library (UaaL) — unityLibrary module (.aar)
        │
        ├── AR Canvas langsung aktif (tanpa splash/login internal)
        ├── MultiSet SDK localize (bagian dari AR session, bukan gate terpisah)
        ├── AR Navigation (arrow, path, drawer — jarak REAL setelah localize)
        ├── Cari Teman (render posisi teman dari friendlist, AR-only — lihat ADR-013)
        └── Voice input, Photon multiplayer (uGUI, existing)
        │
        │  selesai / back
        ▼
    Kembali ke Flutter → DarsiNavigationScreen → WebView (resume, tidak reload)
```

## Empat komponen yang terlibat

| Komponen | Tech stack | Peran | Kepemilikan |
|---|---|---|---|
| `My-eRSIy-CopyCat` (Flutter dummy, nanti prod di repo MyRSIy asli milik KKSoft) | Flutter | Parent app, shell, AppBar native, session/auth, launcher UaaL | KKSoft/Farris (prod) |
| `DARSI-WebView` | Next.js (atau HTML/CSS/JS statis) | UI pre-AR: Home, Cari Lokasi, friendlist | Bagus |
| `DARSI-Indoor-Navigation` | Unity 6000.3.14f1 + MultiSet SDK v1.11.5 + ARCore + Photon PUN 2 | AR Navigation, Cari Teman (render posisi), Voice input | Bagus |
| `DARSI-Indoor-Navigation-Backend` (repo terpisah, di GitHub; folder lokal `darsi-backend`; endpoint dikonsumsi WebView, lihat `API_CONTRACT.md`) | FastAPI (Python) + Postgres via `DATABASE_URL` (Supabase atau self-hosted) | Data POI, friendlist/friend-request, presence, business logic | Bagus |

## Backend & Infrastruktur Hosting (ADR-001, ADR-014, ADR-027)

- **FastAPI (Python):** Business logic, POI sync, dan RAG hybrid retrieval (`/api/assistant/query`).
- **PostgreSQL (`pgvector`):** Penyimpanan relasional POI, tabel chunk pengetahuan bervektor 384-dimensi, dan tabel jadwal dokter.
- **Portabilitas Penuh:** Menggunakan PostgreSQL standar via `DATABASE_URL` dengan `psycopg` murni (tanpa vendor-lockin).
- **Strategi Ingress & Hosting (ADR-027):**
  1. *Server Privat / On-Premise:* Daemon **Cloudflare Tunnel (`cloudflared`)** sebagai Zero Trust application connector, menyediakan endpoint HTTPS publik resmi tanpa membuka port inbound dan tanpa mewajibkan OpenVPN di HP klien.
  2. *Managed Serverless Cloud:* **Supabase** (Postgres + pgvector gratis) + **Koyeb / Render / Fly.io** (FastAPI compute gratis).
  3. *Lokal / Development:* **Docker Compose** (`pgvector/pgvector:pg16` + FastAPI) + `adb reverse` untuk testing kabel USB ke HP Android.

**Penting — tidak ada shared database dengan MyRSIy.** MyRSIy punya database sendiri (PostgreSQL + MySQL, dikonfirmasi Pak Farris/IT RSI A. Yani) yang sepenuhnya terpisah dan tidak diakses DARSI. Satu-satunya jalur komunikasi antara DARSI dan MyRSIy adalah lewat UaaL bridge (`postMessage` / `UnitySendMessage`), bukan lewat query database bersama. Lihat ADR-012 di `DECISIONS.md`.

## Prinsip desain yang mengikat semua keputusan turunan

1. **OTA update adalah requirement non-negotiable** dari pembimbing — semua yang BUKAN logika AR inti harus bisa diubah tanpa rilis ulang APK ke Play Store. Ini alasan utama kenapa WebView dipilih untuk pre-AR UI, bukan Unity UI Toolkit.
2. **Header adalah tanggung jawab Flutter native**, WebView tidak pernah menggambar header sendiri.
3. **Jarak/posisi presisi hanya valid setelah AR aktif** — MultiSet SDK adalah Visual Positioning System (VPS) yang butuh kamera aktif untuk localize. Tidak ada workaround yang jujur untuk menampilkan posisi real-time di luar sesi AR pada MVP ini.
4. **Tidak ada auto-discovery user lain** — semua fitur yang melibatkan lokasi user lain (Cari Teman) wajib consent eksplisit via friend-request + mutual accept (lihat ADR-013), tidak pernah radar/proximity otomatis atau direktori user terbuka.

Lihat `DECISIONS.md` untuk alasan detail di balik tiap prinsip ini.
