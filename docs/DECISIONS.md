# DECISIONS.md — Architecture Decision Record (ADR)

> Catatan tiap keputusan arsitektur besar dan alasannya. Berguna untuk laporan KP/thesis (bab metodologi) dan supaya sesi Claude berikutnya tidak mengulang perdebatan yang sudah selesai.

---

### ADR-001 — Backend: Supabase + FastAPI
**Keputusan:** Pakai Supabase (bukan Neon/PlanetScale) + FastAPI di atasnya.
**Alasan:** Supabase punya Auth, Realtime, dan no cold-start bawaan — penting untuk MVP dengan timeline terbatas. PlanetScale sudah hapus free tier di 2024. FastAPI dipilih karena tim sudah pakai Python untuk Ollama voice pipeline.

### ADR-002 — Unity as a Library (UaaL), bukan WebView murni untuk AR
**Keputusan:** AR navigation tetap native Unity, di-embed ke MyRSIy via UaaL — bukan dijalankan di WebView.
**Alasan:** ARCore/ARFoundation tidak didukung di WebGL maupun WebView (dikonfirmasi: dokumentasi resmi Unity menyatakan WebGL tidak support publikasi AR). Tidak ada cara membuat AR navigation jalan murni di web pada 2026.

### ADR-003 — WebView untuk UI pre-AR, bukan Unity UI Toolkit
**Keputusan:** Screen Home dan Cari Lokasi dibangun sebagai WebView (Next.js), bukan Unity UI Toolkit (UXML/USS).
**Alasan:** Pembimbing meminta kemampuan update tanpa rilis ulang APK, debugging lebih mudah, dan verifikasi Play Store lebih straightforward untuk app yang sebagian besar native + WebView dibanding app WebView murni. Implementasi UI Toolkit yang sempat dibuat (Splash/Auth/Login/Register/Home — 5 UXML, 5 USS, ScreenManager, dll) sudah **dihapus sepenuhnya** dari project Unity karena scope-nya sudah tidak relevan (auth sekarang di-handle MyRSIy).
**Dipertimbangkan ulang:** Sempat muncul opsi balik ke UI Toolkit demi transisi antar-screen yang lebih mulus (seperti Google Maps, satu engine). Ditolak karena mengorbankan requirement OTA update yang jadi alasan utama pivot ke WebView. Solusinya: perbaiki UX transisi (loading state konsisten warna) alih-alih ganti arsitektur.

### ADR-004 — Header adalah Flutter native, bukan bagian WebView
**Keputusan:** AppBar hijau (back button, judul, subtitle, ornamen) digambar oleh `DarsiNavigationScreen` di Flutter. WebView DARSI mulai dari area konten saja.
**Alasan:** Konsistensi visual dengan MyRSIy (dikonfirmasi dari screenshot screen "Layanan Unggulan"). Menghindari header dobel saat WebView di-embed.

### ADR-005 — Color palette DARSI independen dari MyRSIy
**Keputusan:** DARSI punya palette resmi sendiri (Sensational Green #035030, Lime Peel #93BB24, dkk — lihat `DESIGN_SYSTEM.md`) dan font Roboto — bukan hex/font hasil extract dari source code Flutter MyRSIy (#0B4D32, Poppins).
**Alasan:** Keputusan sadar dari pemilik project untuk memberi DARSI identitas visual sendiri yang related tapi tidak identik dengan MyRSIy.

### ADR-006 — Tidak ada screen Peta 2D
**Keputusan:** Screen floor plan 2D dengan dot "posisi kamu sekarang" dihapus dari scope.
**Alasan:** Dot posisi user tidak bisa akurat tanpa lokalisasi AR aktif. Menampilkannya di WebView (pre-AR) berarti berbohong ke user atau memakai data basi ("posisi terakhir"), yang menyesatkan secara UX.

### ADR-007 — Tidak ada jarak (meter) di WebView browsing
**Keputusan:** Home dan Cari Lokasi tidak menampilkan angka jarak seperti "120m". Info yang ditampilkan hanya lantai/gedung. Jarak REAL baru muncul setelah user masuk AR dan berhasil localize.
**Alasan:** Sama seperti Peta 2D — jarak akurat butuh posisi user valid yang hanya ada saat AR aktif. Divalidasi dengan pattern industri: Google Maps Live View juga baru mengaktifkan kamera/AR setelah user commit ke arah tujuan (browsing & lihat rute pakai GPS dulu, AR baru aktif di tahap akhir) — prinsip "progressive disclosure".

### ADR-008 — Tidak ada gate "Deteksi Lokasi" wajib sebelum Home
**Keputusan:** Sempat dirancang screen wajib "Deteksi Lokasi" (scan Unity localization-only) sebelum user bisa akses Home. **Dibatalkan.**
**Alasan:** Terlalu banyak friction di awal — user dipaksa scan sebelum tahu ada value apa di app. Kembali ke pattern progressive disclosure: localization terjadi sebagai bagian dari transisi masuk AR (saat tap "Mulai Navigasi AR"), bukan gate terpisah.

### ADR-009 — Istilah "POI" diganti "Lokasi" di semua UI-facing text
**Keputusan:** Kata "POI" / "Point of Interest" tidak pernah muncul di teks yang dilihat user. Istilah teknis di kode/API (`poiId`, `/api/poi/search`) tetap boleh dipakai karena itu konvensi developer, bukan yang dilihat user.
**Alasan:** "POI" tidak dikenali pasien/pengunjung awam, bisa terdengar seperti singkatan medis yang membingungkan.

### ADR-010 — Cari Teman: pairing-code, bukan auto-detect proximity
**Keputusan:** Fitur cari teman/player TIDAK menampilkan daftar siapa saja yang sedang online di sekitar user (radar). Sebagai gantinya, satu pihak generate kode singkat, bagikan lewat channel eksternal (WhatsApp dll), pihak lain masukkan kode untuk pairing.
**Alasan:** Auto-detect proximity punya risiko stalking nyata — dikonfirmasi lewat riset: app location-sharing kredibel (Life360, Apple Find My, sistem paten location-sharing "closed system") selalu mensyaratkan consent eksplisit dan hubungan yang sudah terjalin SEBELUM masuk app, tidak pernah discovery otomatis terhadap orang asing. Konteks RS memperbesar risiko karena populasi rentan (pasien sendirian, korban KDRT, lansia).

### ADR-011 — Cari Teman hanya berfungsi saat kedua pihak aktif di sesi AR
**Keputusan:** Pairing hanya berhasil kalau KEDUA user sedang localized di sesi AR aktif. Tidak ada tracking posisi di luar sesi AR.
**Alasan:** Sama dengan ADR-007 — keterbatasan fundamental VPS. Sempat dipertimbangkan hybrid positioning (WiFi fingerprinting + PDR/pedestrian dead reckoning) untuk bisa track posisi tanpa AR aktif terus-menerus, tapi **ditolak untuk MVP** karena kompleksitas implementasi (butuh survey kalibrasi WiFi di gedung RS, drift error PDR) tidak sepadan dengan timeline thesis. Dicatat sebagai future work.

---

### ADR-012 — Konfirmasi resmi tech stack MyRSIy (Pak Farris, IT RSI A. Yani, 30 Jun 2026)
**Jawaban resmi:**
1. Framework: **Flutter** — sesuai dengan asumsi yang sudah dipakai di seluruh dokumen ini. Tidak ada rework arsitektur yang diperlukan.
2. Database MyRSIy: **PostgreSQL + MySQL** (dua database, bukan satu)
3. Dokumentasi arsitektur sistem MyRSIy: **belum ada** ("Blm terdokumentasi")

**Implikasi:**
- Database MyRSIy (Postgres+MySQL) adalah **milik dan urusan internal MyRSIy sepenuhnya** — DARSI TIDAK mengakses database itu secara langsung. Backend DARSI (Supabase — juga Postgres, tapi instance terpisah) berdiri sendiri. Satu-satunya jalur komunikasi antara DARSI dan MyRSIy adalah lewat UaaL bridge (`postMessage`/`UnitySendMessage`) yang sudah didefinisikan di `INTEGRATION.md` / `API_CONTRACT.md` — bukan lewat shared database.
- Karena MyRSIy tidak punya dokumentasi arsitektur resmi, semua asumsi integrasi (menu item, `DarsiNavigationScreen`, `webview_flutter`) harus terus divalidasi empiris lewat project dummy `My-eRSIy-CopyCat`, bukan dari referensi resmi KKSoft. Kalau ada perbedaan besar saat integrasi ke repo MyRSIy asli nanti, catat sebagai ADR baru.

## Status tech stack MyRSIy — RESMI & TERKUNCI

- Framework: **Flutter** ✅ confirmed
- Database: **PostgreSQL + MySQL** (internal MyRSIy, terpisah dari Supabase milik DARSI) ✅ confirmed
- Dokumentasi arsitektur dari pihak MyRSIy: tidak tersedia — semua integrasi berbasis observasi dari project dummy
- Sudah ada `webview_flutter: ^4.10.0` dan generic `WebViewScreen` di project dummy slicing

---

### ADR-013 — Cari Teman: friendlist persisten via friend-request (menggantikan pairing-code ephemeral)

**Keputusan:** Model Cari Teman diubah dari pairing-code sekali-pakai (ADR-010/011 versi awal) menjadi **friendlist persisten berbasis friend-request**. User menambah teman lewat **add-by-exact-identifier** (username/ID/QR code) — **BUKAN** direktori/search user yang bisa di-browse. Request masuk berstatus `pending`, baru jadi koneksi permanen setelah penerima **accept** (mutual consent, dua arah). Manajemen (kirim/accept/reject/hapus koneksi) sepenuhnya di **WebView** (2D, tanpa kamera). Posisi live + jarak + navigasi ke teman tetap **AR-only** (lihat ADR-011 — tidak dicabut, cuma di-refine: sekarang syaratnya "koneksi accepted DAN AR aktif", bukan lagi "kode valid DAN AR aktif").

**Presence:** `GET /api/friends` mengembalikan status **kehadiran saja** — `online` / `ar-active` / `offline`. **Tidak pernah** menyertakan gedung, lantai, atau posisi di luar sesi AR. Presence hanya terlihat oleh koneksi yang sudah `accepted` (bukan publik). User bisa **opt-out** (tampil offline ke semua orang), setara toggle "share my location" di Find My/Life360.

**Guardrail keamanan (non-negotiable, berlaku semua tahap):**
- Add-friend **hanya** by exact identifier — tidak ada endpoint/UI untuk browse/cari user secara terbuka.
- Rate-limit pengiriman request (cegah spam-request ke banyak identifier).
- Block/report tersedia di sisi penerima.
- Presence dan posisi tidak pernah bocor ke pihak yang bukan koneksi accepted.

**Alasan:** Owner project (Bagus) ingin user bisa kelola/cari koneksi teman dari WebView tanpa harus masuk AR dulu — pairing-code ephemeral terlalu terbatas untuk itu (nggak ada "daftar teman" yang bisa dilihat sewaktu-waktu). Friend-request memenuhi kebutuhan itu **tanpa** mengulang risiko yang membuat ADR-010 menolak auto-discovery: satu-satunya hal yang bikin auto-discovery berbahaya adalah *discoverability publik* (siapa saja bisa nemu siapa saja). Selama add-friend tetap exact-identifier + mutual accept (bukan direktori terbuka), model ini se-aman pairing-code tapi jauh lebih berguna — konsisten dengan pola app kredibel yang sama yang dikutip ADR-010 (Life360, Apple Find My: keduanya JUGA pakai friendlist persisten, bukan cuma pairing sekali pakai, tapi tetap tanpa direktori publik).

**Hard blocker (lihat `ROADMAP.md` T0.8):** friend-request butuh identitas user yang stabil (user ID + handle) dari MyRSIy lewat bridge. Kalau MyRSIy tidak bisa menyediakan itu, fitur ini tidak bisa dibangun sama sekali dan harus mundur ke model pairing-code (ADR-010/011 versi asli). Menunggu konfirmasi Pak Farris.

**Dampak ke dokumen lain:** `FLOWS.md` bagian 5 ditulis ulang mengikuti model ini. `INTEGRATION.md`/`API_CONTRACT.md` payload `launchAR` pakai field `connectionId` (bukan lagi `pairingSessionId`). Endpoint backend jadi `/api/friends/request|respond|{id}` + `GET /api/friends` (bukan lagi `/api/pairing/create|join|confirm`).

---

### ADR-014 — Kepemilikan field POI: gedung/lantai di Unity (POIData), status di backend
**Keputusan:** Data per-POI dibagi berdasarkan **volatilitas & kepemilikan**:
- **Statis-struktural** (`building`, `floor`) → milik **`POIData` di Unity**. Ikut ke-sync ke backend, Unity jadi sumber kebenaran. `POIData.cs` ditambah 2 field string `building` + `floor` (aditif, tidak menyentuh logika existing — ditandai eksplisit karena `POIData.cs` ada di daftar "jangan diubah tanpa alasan kuat"; ini alasan kuatnya).
- **Operasional-volatile** (`status`: Buka/Antre/Penuh) → milik **backend**. Berubah sering, idealnya dari sistem antrean RS, dan bisa diedit orang non-teknis lewat admin panel — jadi tidak masuk akal di Unity.

**Alasan:** POI secara fisik memang diauthoring di Unity (harus, biar bisa dinavigasi), jadi gedung/lantai wajar ditag di situ juga — sekali kerja, satu sumber kebenaran, tidak ada risiko baris yatim akibat matching nama. Menaruh gedung/lantai di backend = 2 sumber kebenaran untuk 1 POI + logika partial-upsert + risiko drift. Kunci prinsip: **"sering berubah & diedit non-teknis?" → backend. "nyaris tak berubah & melekat ke fisik POI?" → Unity.** Catatan OTA: ngedit gedung/lantai di Unity Editor tetap OTA (sync ke backend → WebView fetch), TIDAK perlu reupload APK — Unity di sini alat authoring, bukan penyimpan data runtime.

**Fasing implementasi:** untuk sekarang (T3.4) backend di-**seed manual** (gedung/lantai/status diketik langsung di SQL). Model Unity-sumber-kebenaran baru aktif penuh saat Unity Editor sync tool dibangun (ROADMAP T3.4-L2). Jadi ADR ini mengunci *keputusan*-nya sekarang; implementasi field `POIData` + sync menyusul agar tidak memblok WebView.

**Prinsip portability (turunan ADR-001):** WebView → FastAPI → Postgres SQL standar; jangan cantol dalam ke fitur proprietary Supabase (Auth/Realtime/PostgREST langsung) supaya migrasi ke Postgres RS-hosted tetap mulus (`pg_dump`/`pg_restore` + repoint connection string).

---

> Catatan: ADR-015 (Cari Teman "request-to-meet") dicatat di `DECISIONS.md` repo WebView — murni keputusan flow UI 2D, tidak menyentuh kerja Unity. Repo Unity lompat ke ADR-016 yang memang menyentuh authoring POI di sini.

### ADR-016 — Kategori POI kanonik = satu sumber kebenaran, divalidasi di boundary sync

**Keputusan:** Kategori POI adalah **himpunan tertutup kanonik**, bukan string bebas. Di Unity, `POIData.kategori` yang diketik saat authoring POI HARUS salah satu dari daftar kanonik. Alur penegakan:
1. **SSOT di backend:** konstanta `POI_CATEGORIES` (`darsi-backend/app/main.py`) = daftar kategori kanonik.
2. **Validasi fail-loud saat sync:** window `DARSI > Sync POIs to Backend` push ke `POST /api/poi/sync`; kalau ada POI dengan `kategori` di luar daftar → **seluruh sync ditolak (HTTP 422)** dengan pesan menyebut kategori salah + daftar valid. Muncul langsung di window POI Sync ("Gagal (422): kategori tidak dikenal: ...").
3. **WebView:** `categoryIcon()` (`app/lib/api.ts`) memetakan kategori → ikon (`/icons/<segmen>/<nama>.svg`); key-nya mirror `POI_CATEGORIES` (case-sensitive). Kategori tak dikenal jatuh ke ikon `pin` (jaring pengaman, bukan pengganti validasi).

**Alasan:** `category` menentukan ikon POI di WebView (turunan ADR-014: category itu field milik Unity). String bebas yang diketik di Unity gampang typo/beda kapital, dan gejalanya diam-diam (ikon default). Validasi di endpoint sync = chokepoint natural → typo ketahuan **saat klik Sync**, bukan berminggu kemudian. Tabel `categories` + API sendiri sengaja tidak dibuat (over-engineering untuk skala ~20 POI).

**Konsekuensi:** menambah/rename kategori = edit **dua tempat** (`POI_CATEGORIES` backend + `categoryIcon()` WebView) + taruh file ikon di `public/icons/<segmen>/`. Pencegahan di hulu (jadikan `POIData.kategori` dropdown/enum, bukan string bebas) menyusul saat daftar kategori RS fix — sekarang masih `string` + guardrail validasi di boundary. Daftar kategori final RS wajib divalidasi ke IT/manajemen RSI.

---

### ADR-017 — Gating login MyRSIy + identitas via injeksi host + DARSI mint handle sendiri

**Keputusan:** Identitas user (Fase 2 friendlist) = **seam** yang dibangun sekarang tanpa nunggu MyRSIy:
1. **Gating ikut login MyRSIy:** navigasi lokasi = boleh **tamu**; Cari Teman = **login-only**.
2. **Identitas disuntik host→WebView** (`window.__DARSI_USER__`), **BUKAN lewat Unity dan bukan lewat `launchAR`**. Unity tak menyentuh identitas — cuma terima `connectionId` yang sudah `accepted` seperti biasa. Yang wajib dari MyRSIy cuma `userId` stabil + tak didaur ulang (UUID/PK).
3. **DARSI mint handle sendiri** kalau MyRSIy tak kasih — tak perlu MyRSIy expose PII.

**Relevansi ke repo Unity:** ADR ini sebagian besar keputusan WebView/host, tapi dicatat di sini karena (a) menegaskan Unity **tetap tidak punya konsep login/identitas** (konsisten ADR-003) dan tidak boleh menerima/menyimpan `userId`, dan (b) menurunkan T0.8 dari blocker keras jadi wiring terakhir — Fase 2 (termasuk T2.6/T2.7 render posisi teman di Unity) bisa dibangun di atas identitas suntikan (dev/copycat). Detail kontrak `window.__DARSI_USER__` ada di `INTEGRATION.md` + `API_CONTRACT.md` (WebView). Refine ADR-013 (identifier friend-request = handle buatan DARSI); posisi tetap AR-only (ADR-011/015).

---

### ADR-018 — Visibilitas POI per-lantai: cluster-derived, bukan band ketinggian hardcode

**Keputusan:** POI hanya tampil di AR kalau berada di **lantai yang sama dengan posisi kamera user saat ini** (mengurangi clutter marker lintas-lantai pada map multi-lantai bertumpuk vertikal). Lantai ditentukan lewat **clustering otomatis dari posisi Y POI**, BUKAN band ketinggian hardcode (mis. "0-3m = lantai 1, 3-6m = lantai 2"):

1. **Derive lantai dari data:** saat localize, kelompokkan semua `POIData` aktif berdasarkan Y — klaster yang terbentuk = lantai-lantai yang ada. Tidak ada angka ketinggian di-hardcode di kode.
2. **Lantai aktif = klaster terdekat** dari posisi kamera (pose MultiSet yang sudah ter-localize, BUKAN Y ARCore mentah yang drift), di-smooth ~0.5 detik untuk meredam jitter.
3. **Hysteresis:** pindah lantai aktif hanya setelah kamera melewati titik tengah antar-klaster + margin, dan stabil beberapa frame — mencegah POI berkedip nyala-mati di dekat tangga/batas lantai.
4. **Target navigasi aktif selalu tampil**, walau berbeda lantai dari lantai user sekarang — filter ini murni untuk decluttering marker, BUKAN untuk memutus rute. NavMesh tetap menyambungkan lintas lantai lewat node tangga/lift seperti biasa; POI tujuan baru "senyap" visual sampai user tiba di lantainya.
5. **`POIData.floor` (string "Lantai 1/2", ADR-014) tetap murni label tampilan** — keputusan visibilitas AR sepenuhnya dari geometri Y, supaya tidak ada risiko mismatch antara label string dan posisi fisik asli.

**Alasan:** band hardcode mengharuskan menebak tinggi lantai gedung RSI sebelum data/scan asli ada — tebakan yang salah (lantai lebih tinggi/rendah dari asumsi, lobi RS yang biasanya lebih tinggi) bikin POI kepotong salah lantai secara diam-diam. Clustering menghindari ini sepenuhnya: ia menemukan tinggi lantai dari **scan fisik asli** (posisi Y hasil authoring POI di scene MultiSet), jadi otomatis benar untuk gedung RSI berapa pun tinggi lantainya nanti — tidak perlu tahu angkanya sekarang. Keputusan ini reversible & aman diambil sebelum data RSI ada: cuma 2 knob (jendela smoothing, margin hysteresis) yang perlu di-tune ulang saat scan asli masuk, logika inti tidak perlu ditulis ulang.

**Risiko residual (diakui, bukan diabaikan):** lantai yang berdekatan tipis (mis. mezzanine ~1.5-2m di atas lantai dasar) bisa ambigu kalau jaraknya kurang jauh dari tinggi HP dipegang (~1.4m); POI per lantai yang sangat sedikit (1-2 titik) bikin klaster kurang stabil secara statistik. Keduanya spesifik-gedung RSI, belum diketahui sampai scan asli ada — ditangani nanti kalau benar-benar muncul (YAGNI), bukan diantisipasi sekarang dengan kompleksitas tambahan.

**Bentuk implementasi:** komponen aditif baru `FloorVisibilityManager` (belum dibuat) — reuse list POI dari `POIManager` yang sudah ada, tidak menyentuh script protected. Validasi awal pakai data kampus (11 POI, 2 lantai) sebagai testbed sebelum data RSI asli tersedia.
