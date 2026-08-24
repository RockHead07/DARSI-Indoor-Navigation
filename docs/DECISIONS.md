# DECISIONS.md — Architecture Decision Record (ADR)

> Catatan tiap keputusan arsitektur besar dan alasannya. Berguna untuk laporan KP/thesis (bab metodologi) dan supaya sesi Claude berikutnya tidak mengulang perdebatan yang sudah selesai.

---

### ADR-001 — Backend: Supabase + FastAPI
**Keputusan:** Pakai Supabase (bukan Neon/PlanetScale) + FastAPI di atasnya.
**Alasan:** Supabase punya Auth, Realtime, dan no cold-start bawaan — penting untuk MVP dengan timeline terbatas. PlanetScale sudah hapus free tier di 2024. FastAPI dipilih karena tim sudah pakai Python untuk Ollama voice pipeline.

### ADR-002 — Unity as a Library (UaaL), bukan WebView murni untuk AR
**Keputusan:** AR navigation tetap native Unity, di-embed ke MyRSIy via UaaL — bukan dijalankan di WebView.
**Alasan:** ARCore/ARFoundation tidak didukung di WebGL maupun WebView (dikonfirmasi: dokumentasi resmi Unity menyatakan WebGL tidak support publikasi AR). Tidak ada cara membuat AR navigation jalan murni di web pada 2026.

#### Koreksi 002-C (2026-07-20) — alasan lama sebagian KELIRU; keputusan tetap, dasarnya diganti

Kalimat *"tidak ada cara membuat AR navigation jalan murni di web pada 2026"* **salah** dan sudah diverifikasi salah. Dicatat di sini persis karena keputusannya kebetulan tetap benar — ADR dengan alasan keliru itu jebakan: orang berikutnya percaya alasannya lalu memakainya untuk keputusan lain yang tidak cocok. (Pertanyaan ini muncul dari dosen yang menyarankan "tanpa kesan Unity".)

**Fakta yang benar (2026-07, terverifikasi):**
- **MultiSet PUNYA WebXR Kit** — VPS localization berbasis browser, tanpa install, dari QR. Jadi web AR dengan localize memang ada.
- **Tapi WebXR TIDAK jalan di WebView.** MDN browser-compat-data: `navigator.xr` → `webview_android: false` (bug Chromium 40652382, masih terbuka). Ini berlaku untuk `webview_flutter` MAUPUN `flutter_inappwebview` — keduanya membungkus `android.webkit.WebView` yang sama. Bukan soal Flutter; komponennya sendiri tidak mengekspos WebXR.
- **WebXR jalan di Chrome asli** (Chrome Android 79+), jadi bisa dipicu dari Flutter lewat **Chrome Custom Tabs / TWA** — tapi itu "keluar ke Chrome fullscreen", bukan embed, dan pengalamannya patah.
- **iOS Safari: WebXR nol** (Apple tak mendukung `immersive-ar`). Diputuskan **2026-07-20: iOS di-drop** dari scope — MyRSIy belum menyasar iPhone.
- **WebXR Kit cuma memberi localization**, bukan mesin navigasi. NavMesh, pathfinding, state machine lintas-lantai tetap harus dibangun ulang di three.js.

**Kenapa keputusan tetap UaaL — sekarang berbasis preseden industri, bukan "mustahil":**
Untuk kelas masalah ini (rumah sakit, multi-lantai, presisi), native memang jalur standarnya —
Google Maps Live View (native ARCore Geospatial), Pointr (native SDK di bandara). Framing industri
eksplisit: *ritel sekali-pakai → WebAR; rumah sakit routing presisi multi-lantai → point cloud + ARKit/ARCore native.* DARSI = kasus kedua.

**Menjawab keluhan "UaaL terasa seperti buka aplikasi Unity terpisah" (valid):**
UaaL default = Activity fullscreen, memang terasa terpisah. Tapi **embed sebagai widget Flutter itu nyata**:
`flutter_embed_unity` mendukung **Unity 6000.3 LTS** (= versi kita) + ARFoundation. Unity dirender ke
surface di pohon widget Flutter, Flutter bisa overlay di atas view AR, tanpa Activity/task terpisah.
Risiko jujur: embedding "melanggar asumsi fullscreen UaaL" (rapuh, fungsi tak terdokumentasi); ada bug
`flutter_embed_unity` + ARFoundation crash di Android <13 sejak Flutter 3.22; dan **kompatibilitas MultiSet
di dalam surface embed BELUM terverifikasi** — perlu spike (ROADMAP langkah 7). Untuk AR nav, view kamera
memang seharusnya fullscreen; yang "diperbaiki" embedding adalah rasa aplikasi-terpisah, bukan ukuran view.

**Kekhawatiran ukuran aplikasi ≠ alasan pindah web.** Solusi industri = **Play Feature Delivery**
(modul on-demand): Unity baru diunduh saat user pertama tekan Navigasi AR; unduhan awal MyRSIy tak berubah.
Preseden: Halodoc (aplikasi kesehatan Indonesia) — base size −54%, uninstall −52% dengan dynamic feature
modules. Data scan pun tidak masuk APK (`MapMeshDownloader` unduh runtime). Ditelusuri lengkap; keputusan
ada di sisi MyRSIy (ROADMAP langkah 8).

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

#### Amandemen 018-A (2026-07-20) — `_currentFloor` berhenti direset tiap relocalize latar belakang

**Pemicu:** tes lapangan pertama di RSI (2026-07-20): POI lantai bawah tetap terlihat padahal seharusnya senyap. Tidak reproduksi di Editor — `DeviceAnimation` menggerakkan kamera lewat jalur simulasi bersih, beda dari hasil align MultiSet yang nyata.

**Akar masalah:** `BuildFloorClusters()` (dipanggil dari listener `LocalizationSuccess`) me-reset `_currentFloor` ke -1 lalu menebak ulang dari **satu sampel Y instan**, setiap kali event itu menyala. Dan event itu menyala berkali-kali sepanjang sesi, bukan cuma sekali di awal — setelan SDK (`backgroundLocalization=true`, `relocalization=true`) membuatnya relocalize di latar belakang secara periodik. Akibatnya, mekanisme smoothing+hysteresis yang sudah dirancang di `Update()` justru dilewati tiap kali relocalize latar belakang terjadi — persis kasus yang seharusnya diredam.

**Keputusan:** `_currentFloor` TIDAK lagi direset paksa tiap `BuildFloorClusters()` dipanggil. Nilai lama dipertahankan sebagai prior; `Update()` yang menuntaskan penyesuaian lewat evaluasi kontinu (smoothing + hysteresis), bukan snap sesaat. Reset ke -1 hanya terjadi kalau memang belum pernah ada nilai (localize pertama sesi), atau jumlah cluster berubah sehingga index lama jadi tidak sah.

**Alasan:** keanggotaan cluster (`_floorPois`, POI mana masuk lantai mana) TIDAK perlu dihitung ulang tiap event — dia invariant terhadap transform rigid, karena posisi Y POI relatif satu sama lain tidak berubah walau Map Space digeser-align ulang (sudah dicatat di komentar kode sejak awal ADR ini). Yang sebenarnya rentan cuma satu angka: posisi Y kamera **saat itu juga**. Membuang seluruh status stabil demi satu sampel yang belum tentu representatif berarti mengabaikan mekanisme peredam noise yang sudah dibangun untuk kasus persis ini.

**Konsekuensi:** method baru `ComputeInstantFloor()` — baca posisi kamera fresh, tanpa smoothing, tanpa efek samping ke `_currentFloor`. Dipakai konsumen yang butuh jawaban seketika di satu titik keputusan spesifik (lihat Amandemen 020-C), terpisah dari jalur stabil yang menggerakkan visibility marker.

**Bentuk implementasi:** perubahan lokal di `FloorVisibilityManager.BuildFloorClusters()` + guard index di `Update()`. Tidak menyentuh script protected.

---

### ADR-019 — Out-of-bounds = *coverage notice* (bukan barrier), auto-derive dari tepi NavMesh

**Keputusan:** Saat kamera user keluar area ter-scan (di luar NavMesh, > threshold), tampilkan penanda AR — TAPI framing-nya **jangkauan navigasi**, bukan larangan fisik:

1. **Copy = coverage, bukan prohibition.** Pesan: **"Di luar jangkauan navigasi"** / "Navigasi tidak tersedia di area ini" — BUKAN "Area tidak bisa dilewati". Alasan di bawah. Area yang beneran restricted (ICU, OK, staff-only) itu ranah signage fisik RS, bukan tugas fitur ini.
2. **Deteksi auto-derive dari tepi NavMesh** (`NavMesh.SamplePosition`, API yang sudah dipakai `NavigationControllerExtension`). Nol authoring — jalan di map manapun. Cek di-throttle (~0.15s), hanya aktif **setelah localize** (ADR-007).
3. **Hysteresis dua threshold** (`showAt` > `hideAt`) untuk meredam localization drift — tanpa ini penanda kedip-kedip pas user berdiri di tepi.
4. **Panah "kembali ke jalur" masuk MVP.** `SamplePosition` sudah mengembalikan `hit.position` (titik NavMesh terdekat) → arahkan panah dari user ke situ. Nyaris gratis, mengubah notice buntu jadi panduan.
5. **Billboard menghadap user, BUKAN tembok sepanjang tepi.** Tepi NavMesh poligon tak beraturan; render tembok penuh = ribet. Satu papan melayang menghadap user = info sama, kerja seperlima.

**Alasan:** metafora out-of-bounds game itu **jujur di game** (dunia virtual, beneran tak bisa lewat) tapi **bohong di AR** — "area out of bounds" itu lorong fisik nyata yang user bisa jalani. Maka klaim "tidak bisa dilewati" menyesatkan; yang benar "kami tidak bisa memandu di sini" (batas coverage, bukan larangan). Auto-derive dari NavMesh menghindari sistem authoring per-zona yang harus diulang tiap re-scan (YAGNI) — cukup untuk MVP.

**Risiko residual / caveat WAJIB-CEK saat scan RSI asli:**
- **NavMesh multi-lantai (nyambung ADR-018):** kalau mesh gabungan semua lantai, `SamplePosition` bisa cocok titik di lantai lain (tepat di bawah/atas user) → false "in bounds". Wajib pastikan mesh per-lantai ATAU deteksi ikut hitung jarak vertikal. Belum bisa dipastikan sampai scan asli.
- **Localization loss di tengah sesi** → posisi kamera stale → deteksi ngaco. Gating `IsLocalized` cuma nutup kasus "belum pernah localize", bukan loss di tengah.
- **Angka threshold** cuma valid setelah scan RSI (tepi map dummy kampus ≠ tepi RSI) → tune di lapangan, logika inti tak perlu ditulis ulang.

**Ditunda (future, bukan MVP):** cue audio/getar saat lewat batas (aksesibilitas — segmen lansia/literasi rendah, sejalan riset wayfinding); perbedaan perilaku `navigate` (dorong "balik ke jalur") vs `freeExplore` (murni coverage notice); zona manual ber-tag (diam/restricted/pesan custom) kalau auto-derive terbukti kurang — masuk Sprint 2 re-placement.

**Bentuk implementasi:** komponen aditif `NavBoundaryNotifier` + world-space Canvas sign (uGUI, konsisten ADR-003 — bukan UI Toolkit) — tidak menyentuh script protected. Perlu expose `UaaLEntryPoint.IsLocalized` (public getter, sebelumnya `_isLocalized` privat). Reversible & aman dibangun sebelum data RSI ada: cuma knob threshold + zona yang perlu di-tune saat scan masuk.

---

### ADR-020 — Navigasi lintas-lantai: rute TERSEGMENTASI + handoff eksplisit di lift (bukan NavMesh kontinu antar-lantai)

**Keputusan:** Saat POI tujuan berada di lantai berbeda dari user, rute **dipecah per lantai**, bukan dihitung sebagai satu jalur kontinu:

1. **Rute tersegmentasi.** Tahap 1 = navigasi ke **penghubung vertikal (Lift)** di lantai user — BUKAN langsung ke tujuan akhir. Setelah user pindah lantai & re-localize, tahap 2 = navigasi ke tujuan asli.
2. **Transisi lantai = STATE aplikasi, bukan jalur geometris.** Rute AR **berhenti** di lift. Sistem TIDAK PERNAH menggambar garis rute menembus plafon.
3. **Penghubung vertikal = `NavMeshLink`, BUKAN ramp.** Ramp sebagai penyambung antar-lantai ditolak (alasan di bawah). Ramp tetap sah untuk menyambung celah **di dalam satu lantai** (threshold/undakan) — itu kasus berbeda.
4. **Wajib re-localize setelah ganti lantai.** Setelah keluar lift, user diminta memindai sekitar; navigasi lanjut HANYA setelah localize berhasil (konsisten ADR-007: posisi baru valid setelah localize).
5. **Beri tahu di depan.** Begitu user memilih POI beda lantai, sampaikan SEBELUM rute dimulai: tujuan di lantai berapa + akan diarahkan ke lift dulu. Jangan biarkan user kaget saat rute "berhenti" di lift.
6. **Sumber kebenaran lantai:** lantai user = hasil clustering Y `FloorVisibilityManager` (ADR-018); lantai tujuan = `POIData.floor`. Deteksi lintas-lantai = perbandingan keduanya.

**Alasan:** kendala fisik, bukan keterbatasan yang bisa direkayasa — **AR tracking & localize MultiSet PUTUS saat ganti lantai.** Lift adalah kotak tertutup yang bergerak: tidak ada kontinuitas visual untuk visual-odometry. Maka "satu sesi AR + satu rute kontinu lintas-lantai" itu fiksi. NavMesh kontinu lewat ramp antar-lantai menghasilkan dua kebohongan visual sekaligus: garis rute ter-render **menembus plafon**, dan agent "berjalan" di ramp yang **tidak ada wujud fisiknya** — menyesatkan user di dunia nyata. Pola tersegmentasi adalah standar wayfinding indoor multi-lantai (mal, bandara, RS). `NavMeshLink` dipilih karena (a) semantiknya benar — titik A→B tanpa jalur fisik, dan (b) **terdeteksi di kode**, sehingga trigger UI handoff jadi eksplisit; ramp tidak memberi sinyal apa pun yang bisa dibaca program.

**Konsekuensi:** perlu state machine navigasi minimal — `menuju-konektor` → `menunggu-transisi` → `menunggu-relocalize` → `menuju-tujuan`. Perlu menentukan konektor vertikal terdekat/valid dari posisi user (untuk sekarang: POI berkategori **Lift**). Kontrak bridge (`arSessionClosed`/`navigationArrived`) TIDAK berubah — transisi lantai murni state internal Unity sampai tujuan akhir tercapai, host tidak perlu tahu.

**Selaras dengan data scan yang ada:** mapset `MSET_39LMY8E89OO6` sudah berisi **2 map dengan rentang Y berbeda** (`MAP_9UMXWR0PI7K5` lebih bawah, `MAP_PHVXJ15C2CF0` lebih atas) — konsisten dengan satu map per lantai. Re-localize setelah transisi menargetkan map lantai tujuan.

**Risiko residual (diakui, bukan diabaikan):** user bisa TIDAK benar-benar naik lift (batal, atau salah lantai) — maka setelah re-localize sistem WAJIB menentukan ulang lantai user dari clustering Y, jangan pernah mengasumsikan user menuruti instruksi. Kalau localize di lantai baru gagal berulang, navigasi bisa mandek → wajib ada jalan keluar (batalkan/ulangi), bukan buntu diam. Tangga/eskalator punya karakter berbeda dari lift (tracking mungkin tidak putus total) — diputuskan nanti kalau benar-benar dipetakan (YAGNI).

**Bentuk implementasi:** aditif, tidak menyentuh script protected. Deteksi lintas-lantai membaca `FloorVisibilityManager` (lantai user) + `POIData.floor` (lantai tujuan). Instruksi/handoff pakai pola toast + panel uGUI yang sudah ada (ADR-003); panel transisi mengikuti aturan `ExclusivePanel` supaya tidak menumpuk dengan page lain. Lift diberi `NavMeshLink` per pasangan lantai. Semua angka (jarak "sampai di lift", timeout relocalize) belum di-tune — menyusul validasi di lokasi, pola yang sama dengan ADR-019.

#### Amandemen 020-A (2026-07-19) — POI yang berulang tiap lantai: konektor vs amenity

**Pemicu:** setelah `name UNIQUE` dicabut (ADR-021), scene RSI menghasilkan **dua baris `Lift`** — satu per lantai. Muncul pertanyaan: apakah dua POI ini sebaiknya digabung jadi satu supaya user tidak melihat dua tombol untuk lokasi yang sama?

**Keputusan:** jangan digabung di data — pisahkan **dua kelas POI yang selama ini tercampur**, dan perlakukan berbeda. Aturannya digerakkan `kategori` (kanonik sejak ADR-016), bukan kesamaan nama.

| Kelas | Kategori | Muncul di daftar Cari Lokasi? | Pemilihan instance |
|---|---|---|---|
| **Konektor vertikal** | `Lift`, `Tangga` | **Tidak** (di daftar browse default) | Otomatis oleh sistem, bagian dari rute tersegmentasi di atas |
| **Amenity multi-instance** | `Toilet`, `Musholla`, `ATM`, `Ruang Tunggu`, … | Ya — **satu kartu**, dikelompokkan per nama | Instance terdekat dari posisi user |
| **Destinasi tunggal** | `IGD`, `Farmasi`, `Radiologi`, … | Ya, apa adanya | Tidak relevan (cuma satu) |

**Alasan:** tidak ada orang yang tujuannya "lift" — tujuannya Farmasi di lantai 1, dan lift cuma cara sampai ke sana. Ini bukan preferensi gaya: standar pemodelan indoor (IMDF, IndoorGML) memisahkan **ruang yang dituju** dari **penghubung yang dilewati**, dan sirkulasi vertikal dimodelkan sebagai konektivitas di graf navigasi, bukan sebagai destinasi. Keputusan pokok ADR-020 sudah mengandung ini secara implisit ("konektor vertikal terdekat/valid dari posisi user") — amandemen ini cuma membuatnya eksplisit sampai ke lapisan daftar.

Konsekuensinya, masalah "dua tombol Lift" **tidak ditambal, tapi hilang** — Lift berhenti menjadi entri daftar. Itu sebabnya pendekatan ini dipilih di atas alternatif yang cuma merapikan tampilannya.

**Alternatif yang DITOLAK:**
- **Gabung jadi satu baris DB.** Menghapus posisi lift kedua — tidak bisa dipulihkan, dan Unity butuh keduanya untuk merender marker & menghitung konektor terdekat.
- **Bikin nama unik: `"Lift Lantai 1"`.** Menjejalkan `floor` ke dalam `name` — satu data di dua tempat, anti-pattern yang persis dicabut ADR-021.
- **Tambah kolom `group_key`.** Mesin baru untuk sesuatu yang `kategori` + `name` + `floor` sudah cukup menjawab (YAGNI). Baru perlu kalau muncul dua POI bernama sama yang BUKAN fasilitas serupa.
- **Terapkan pengelompokan "pilih yang selantai" ke semua nama kembar, termasuk Lift.** Ini usulan pertama yang dibuat lalu ditolak sendiri: memperlakukan konektor sebagai destinasi, jadi merapikan gejala sambil membiarkan model datanya keliru.

**Risiko residual (diakui):**
- **"Terdekat" untuk amenity idealnya diukur panjang jalur, bukan lantai.** Untuk sekarang dipakai "instance selantai dengan user" — plafonnya jelas: toilet yang sebenarnya paling dekat bisa berada di lantai atas persis di sebelah lift, dan aturan ini akan memilih yang salah. Ini membaik sendiri begitu `NavMeshLink` terpasang dan jalur lintas-lantai bisa dihitung; jadi utang sementara, bukan permanen.
- **Menyembunyikan Lift dari daftar bisa merugikan sebagian user** — pengguna kursi roda, pendorong brankar, atau yang tidak kuat naik tangga kadang memang mencari lift secara sadar. Mitigasi: keluarkan dari daftar *browse* default tapi tetap dapat ditemukan lewat pencarian. Apakah ini cukup **belum divalidasi** — masuk daftar pertanyaan observasi lapangan, jangan diputuskan dari belakang meja.

**Urutan kerja:** amandemen ini **tidak bisa** dikerjakan mendahului T5.3. Baik penyembunyian konektor maupun pemilihan instance bergantung pada state machine rute tersegmentasi; dikerjakan duluan berarti menulis logika yang harus dibongkar lagi.

#### Amandemen 020-B (2026-07-19) — `NavMeshLink` TIDAK dipakai; jarak lintas-lantai dijumlahkan per segmen

**Mencabut poin 3** dari keputusan pokok di atas ("Penghubung vertikal = `NavMeshLink`"). Poin 1, 2, 4, 5, 6 tetap berlaku.

**Keputusan:** lantai TETAP dibiarkan sebagai pulau NavMesh yang terpisah. Tidak ada `NavMeshLink` antar-lantai. Jarak ke POI di lantai lain dihitung dengan **menjumlahkan dua segmen** yang masing-masing berada di dalam satu lantai:

```
jarak = path(user → lift lantai user) + path(lift lantai tujuan → POI)
```

**Alasan poin 3 gugur:** satu-satunya alasan ADR memilih `NavMeshLink` adalah supaya handoff "terdeteksi di kode". Setelah state machine dibangun, alasan itu **hilang** — `FloorTransitionController` sendiri yang mengarahkan user ke lift, jadi ia sudah tahu persis kapan handoff terjadi tanpa perlu sinyal dari NavMesh. Dan karena rutenya tersegmentasi (poin 1), jalur kontinu antar-lantai memang tidak pernah dibutuhkan untuk navigasi.

**Alasan tidak memakainya sama sekali** — dua, dan yang kedua lebih penting:

1. **Kontradiksi internal dengan poin 2.** Begitu dua lantai tersambung, SDK bisa menghitung rute lintas-lantai sebagai satu jalur, dan `ShowPath` akan menggambar garis rute **menembus plafon** — persis yang dilarang poin 2. Aman hanya selama `currentDestination` tidak pernah berisi POI lintas-lantai; itu jaminan yang bergantung pada kode kita tetap benar selamanya.
2. **Radius perubahan terlalu luas.** `NavMeshLink` mengubah pathfinding secara **global**: setiap `NavMesh.CalculatePath` di proyek — termasuk kode SDK yang belum diaudit — mendadak menganggap dua lantai sebagai satu ruang berjalan. Konektivitas NavMesh sebaiknya tetap mencerminkan kenyataan fisik: tidak ada lantai yang menyambung.

**Kenapa penjumlahan segmen justru lebih baik, bukan sekadar penghindaran:** angkanya lebih jujur. Yang ditampilkan persis perjalanan yang akan dilalui user — jalan ke lift, lalu jalan dari lift ke tujuan. `NavMeshLink` menghitung jarak yang melewati link, yang "panjangnya" cuma artefak dari di mana kedua ujungnya diletakkan.

**Bukti (2026-07-19, Editor):** label `[Lantai1] Lift` menghasilkan **11 m**. Untuk POI itu segmen 2 panjangnya nol (lift lantai tujuan *adalah* POI-nya), jadi 11 m murni segmen 1 — dan itu sama persis dengan angka yang dihitung `PathEstimationUtils` bawaan SDK untuk lift Ground. Dua perhitungan independen, hasil identik.

**Konsekuensi tampilan:** POI lintas-lantai tampil `"24 m · Lantai 1"`, bukan `"Unreachable"` (yang menyesatkan — POI-nya bisa dicapai lewat lift) dan bukan hanya `"Lantai 1"` (yang membuang informasi jarak tanpa alasan). POI **selantai** yang tetap `Unreachable` sengaja tidak disentuh: itu masalah NavMesh asli, dan justru makin menonjol sekarang karena semua POI lain punya angka.

**Prasyarat data yang ditemukan saat implementasi:** `NavMesh.CalculatePath` hanya men-snap target ~1 m secara vertikal. Empat `poiCollider` berjarak 1.03–1.29 m dari permukaan NavMesh dan menghasilkan `PathInvalid` walau NavMesh-nya tersambung. Tooltip SDK sudah memperingatkan ("Collider should be near NavMesh") — jadi penempatan collider adalah **prasyarat**, bukan detail kosmetik.

**Yang belum terbukti:** seluruh rantai ini terverifikasi di Editor, termasuk lewat UnityEvent `LocalizationSuccess` yang asli. Yang belum: apakah localize sungguhan berhasil di lantai baru di gedung RSI, dan apakah `LocalizeFrame()` benar-benar me-restart jendela `bgLocalizationDuration` (60 detik). Keduanya ada di dalam DLL dan hanya bisa dijawab di lapangan.

#### Amandemen 020-C (2026-07-20) — Konfirmasi lantai lewat jendela konsistensi, bukan sampel instan tunggal

**Pemicu:** sama seperti Amandemen 018-A — tes lapangan RSI (2026-07-20) membongkar bahwa `FloorVisibilityManager` men-snap ulang status lantai dari satu sampel instan tiap relocalize latar belakang. Perbaikan 018-A membuat `_currentFloor` stabil lagi — tapi `FloorTransitionController.OnLocalizationSuccess()` (dibangun sehari sebelumnya) membaca nilai itu justru mengandalkan sifat "langsung ter-update sinkron" untuk mengonfirmasi user sudah sampai di lantai tujuan. Begitu nilainya jadi stabil/lambat berubah (via hysteresis ~1 detik), penyambungan segmen 2 T5.3 bisa telat kalau tetap bergantung pada nilai yang sama dengan yang menggerakkan marker.

**Opsi pertama yang dipertimbangkan lalu DITOLAK:** tambah `ComputeInstantFloor()` dan baca **langsung, sinkron, di frame yang sama** dengan event `LocalizationSuccess`. Ditolak karena bertumpu pada asumsi yang **tidak bisa diverifikasi**: apakah MultiSet SDK menerapkan koreksi pose Map Space secara instan di frame yang sama dengan event sukses-nya, atau menghaluskannya lewat beberapa frame (pola umum di SDK VPS, mencegah "lompatan" visual). SDK-nya DLL tertutup — timing internal ini tidak bisa dibaca dari kode. Membangun keputusan navigasi di atas asumsi tak terverifikasi itu persis pola yang berulang kali terbukti salah sepanjang proyek ini (bandingkan koreksi `listTitle`→`poiName` di ADR-021).

**Keputusan:** `FloorTransitionController` tidak mempercayai satu sampel instan sama sekali — pakai **jendela konfirmasi pendek** yang bersandar pada tracking ARCore (terbukti kontinu tiap frame, berbeda dari timing internal SDK yang tak diketahui):

1. `OnLocalizationSuccess()` cuma jadi **gerbang** — set flag `_hasRelocalizedSinceWaiting = true`. Bukan lagi titik keputusan.
2. Selama fase `AwaitingRelocalize` **dan** gerbang itu sudah terbuka, tiap frame (`LateUpdate`) cek `FloorVisibilityManager.ComputeInstantFloor()` (Amandemen 018-A).
3. Kalau hasilnya **konsisten** menunjuk lantai tujuan selama `floorConfirmWindow` (default 1 detik) berturut-turut → sambung ke segmen 2. Sempat meleset di tengah jalan → hitungan reset ke nol.
4. Gerbang di poin 1 tetap wajib: sebelum localize sukses **pertama kali** sejak masuk fase menunggu, `ComputeInstantFloor()` sama sekali tidak dibaca — Map Space saat itu masih ter-align ke lantai lama, sehingga angkanya bisa kebetulan cocok padahal user masih di dalam lift.

**Alasan jendela ini dipisah dari hysteresis marker** (`FloorVisibilityManager`, juga ~1 detik): dua mekanisme ini kelihatan mirip tapi melayani konsekuensi berbeda — hysteresis marker mencegah kedip visual (salah sesaat = ikon berkedip), jendela konfirmasi T5.3 mencegah salah rute (salah sesaat = user diarahkan navigasi berdasarkan lantai yang belum tentu benar). Dua knob terpisah supaya masing-masing disetel sesuai konsekuensinya sendiri — bukan duplikasi.

**Konsekuensi:** field baru `_hasRelocalizedSinceWaiting`, `_floorConfirmSince`, `[SerializeField] floorConfirmWindow` di `FloorTransitionController`. `OnArrived()` (masuk `AwaitingRelocalize`) me-reset keduanya. Tidak menyentuh script protected.

**Yang belum terbukti:** jendela 1 detik itu tebakan awal, sama seperti seluruh angka lain di T5.3 — perlu di-tune dari data lapangan asli, bukan diasumsikan benar dari sini.

---

### ADR-021 — `POIData` hanya menyimpan yang dimilikinya; nama/lantai/gedung DITURUNKAN, bukan disalin

**Keputusan:** setiap data POI punya **satu pemilik sah**, dan field lain **menurunkan** nilainya saat dibutuhkan — tidak pernah menyimpan salinan yang di-maintain manual.

| Data | Pemilik sah | Perlakuan di `POIData` |
|---|---|---|
| Nama tampilan | `POI.poiName` (komponen SDK MultiSet) — SDK **butuh** field ini, tidak bisa dihilangkan | **diturunkan** (tidak disimpan) |
| `building` | konstanta level-scene (satu gedung per map) | **diturunkan** (tidak per-POI) |
| `floor` (label) | geometri Y — ADR-018 sudah menetapkan keputusan lantai dari clustering Y, label string murni tampilan | **diturunkan** |
| `kategori` | `POIData` — tidak ada sumber lain, murni penilaian manusia (kategori RS kanonik ADR-016) | **disimpan** |
| `poiId` | `POIData` — GUID stabil untuk sync backend (ADR-014) | **disimpan** |
| `sinonim` | `POIData` — alias untuk voice matching | **disimpan** |

**Alasan:** duplikasi data yang di-maintain manual **pasti** melenceng, dan ini sudah terbukti bukan hipotesis — ditemukan 2026-07-19 di scene `DARSi-Indoor Navigation`: GameObject `[Lantai1] IGD` punya `POIData.poiName = "Perpustakaan"` (sisa scene kampus lama), sementara `POI.poiName` SDK-nya sudah benar `"IGD"`. Satu objek, dua nilai berbeda. Kasus serupa terverifikasi di dua POI lain: `[Lantai1] Resepsionis` menyimpan `"Ruang Dosen"`, `[Ground] Parkir Motor Karyawan` menyimpan `"Perpustakaan"`, `[Lantai1] Ruang X-Ray` menyimpan `"Lab Teori 202"`.

**Koreksi saat implementasi (2026-07-19):** draft awal ADR ini menyebut `POI.listTitle` sebagai pemilik nama. Itu **keliru** — sumber SDK (`POI.cs`) menunjukkan `Awake()` melakukan `base.listTitle = poiName`, jadi `listTitle` adalah turunan runtime dari `poiName`, bukan pemiliknya. Menurunkan dari `listTitle` berarti menurunkan dari cache. Pemilik sah = **`POI.poiName`**.

**Kenapa `kategori` tetap DISIMPAN:** SDK punya `POI.type` (enum `POIType`, 15 nilai: Room, Toilet, Parking, Elevator, …) yang sekilas terlihat bisa jadi pemilik kategori. Tidak bisa — enum itu terlalu kasar untuk domain rumah sakit; tidak ada nilai yang mengekspresikan IGD, Farmasi, atau Radiologi. Nilai `kategori: "Room"` yang ditemukan di 7 POI justru salinan dari `POI.type`, yaitu bentuk lain dari penyakit yang sama. Jadi `kategori` benar-benar dimiliki `POIData` dan tetap disimpan (kanonik per ADR-016). Konsekuensinya nyata: sync mengirim nama **salah** ke backend, voice matching mencocokkan nama kampus lama, dan `floor` kosong sehingga logika lintas-lantai (ADR-020) tidak bisa jalan.

**Alternatif yang DITOLAK — tombol "auto-fill" yang menyalin dari SDK ke `POIData`:** itu menyalin, bukan menurunkan. Setelah tombol ditekan datanya tetap ada di dua tempat, cuma kebetulan sedang sama; begitu `POI.poiName` berubah, salinannya basi lagi. Itu meredakan gejala (capek mengetik) tanpa menyembuhkan penyebab struktural. Ditolak secara sadar.

**Konsekuensi:**
- **Menyentuh `POIData.cs` yang berstatus protected di `CLAUDE.md`** — dilakukan dengan sign-off eksplisit pemilik project (2026-07-19), bukan penyimpangan diam-diam.
- Input manual per-POI turun dari 4 field menjadi **1** (`kategori` saja, lewat dropdown kanonik — pencegahan hulu yang memang sudah diantisipasi ADR-016).
- Sync tetap jalan tanpa perubahan kontrak: `POISyncWindow` membaca `p.EffectiveName`/`p.kategori` **saat runtime**, bukan membaca field serialized — jadi computed property transparan bagi sync.
- Nilai turunan tidak lagi berupa field yang bisa diketik di Inspector → **wajib ditampilkan read-only** lewat Inspector kustom, supaya tetap bisa di-spot-check.

**Risiko residual (diakui):** hilangnya kemampuan meng-override nama per-POI untuk kasus khusus (mis. nama tampilan sengaja beda dari label SDK). Kalau kebutuhan itu benar-benar muncul, tambahkan field override opsional yang **kosong secara default** dan hanya dipakai bila diisi — jangan kembali menyimpan salinan penuh (YAGNI sampai terbukti perlu). Penurunan `floor` dari prefix nama GameObject (`[Ground]`/`[Lantai1]`) mengandalkan konvensi penamaan; kalau konvensi dilanggar, penurunan gagal — Inspector harus menampilkan kegagalan itu secara mencolok, bukan diam.

---

### ADR-022 — Validasi Lapangan WebXR AR Runtime, kamera aktif jernih (`showMesh: false`), & model 3D panah kustom (2026-07-28)

**Pemicu:** Pengujian WebXR AR runtime (`DARSI-Indoor-Navigation-WebXR`) di lokasi RS Jemursari (2026-07-28). Terkonfirmasi user berada di Lantai 2, namun sempat mempertanyakan tampilan visual mesh diagnostik yang offset serta visibilitas kamera dunia nyata.

**Keputusan:**
1. **Validasi Lantai 2 Terkonfirmasi:** Diskriminasi lantai berbasis elevasi `position.Y >= 1.5m` (Map Set Space) terbukti valid dan akurat di lapangan (Lantai 2 ter-localize dengan koordinat $Y \approx 3.8\text{m}$, $X \approx -1.56\text{m}$, $Z \approx 39.58\text{m}$).
2. **Kamera Asli Aktif Jernih (`showMesh: false`):** Mesh 3D diagnostik gedung dimatikan (`showMesh: false`). Tampilan video kamera dunia nyata dari HP (ARCore via WebXR) aktif 100% transparan sebagai latar belakang AR. Hal ini **tidak mempengaruhi kinerja VPS/lokalisasi**, dan justru **mengurangi beban render GPU Three.js** karena tidak perlu me-render mesh 3D fisik gedung.
3. **Integrasi Model 3D Panah Kustom (`public/models/arrow.gltf`):** Objek visual petunjuk arah menggunakan file 3D model GLTF kustom yang diletakkan di `public/models/`, di-scale secara dinamis (~0.35m), dan terotasi secara real-time mengarah ke POI tujuan di setiap frame AR (`onXRFrame`).
4. **Rekam POI Lapangan (Tandai-di-Web):** Fitur rekam titik POI langsung di lokasi via tombol `REKAM POI 📍` sukses menghasilkan snippet JSON map-space untuk disimpan ke `public/data/pois.json` dan dipanggil via `?poiId=...`.

---

### ADR-023 — Kualitas capture untuk launching: Insta360 dipertimbangkan, digerbangi cek dukungan Pano MultiSet dulu (2026-08-10)

**Status: ⏸ BELUM DIPUTUSKAN** — dicatat sebagai rencana lintas-repo, bukan keputusan final. Dicatat identik di kedua repo yang memakai MultiSet (repo ini, `com.multiset.sdk` v1.11.5 — dan `DARSI-Indoor-Navigation-WebXR`, `@multisetai/vps` ^2.3.1) supaya keputusan Insta360 tidak diambil dua kali secara terpisah dan tidak konsisten.

**Pemicu:** Menjelang pembuatan final/launching, pemilik project mempertimbangkan kamera **Insta360 X4/X5** untuk map yang lebih imersif dibanding hasil LiDAR (`MultiSet Mapper`) yang dipakai sekarang. Bersamaan dengan itu, MultiSet merilis (v2.2.0, 2026-07-30) **Pano 360° Virtual Tour API** (`/v1/pano/*`) — tur 360° yang bisa dijelajahi, dibangun dari node berisi posisi, rotasi, dan gambar panorama.

**Yang sudah terverifikasi:**
- Syarat Pano API eksplisit: *"Limited to 360 (Insta360) maps that have been processed for panoramic tours"* — map yang tidak diproses lewat jalur itu balas 404. **Map LiDAR yang sudah ada tidak otomatis dapat fitur ini**, meski format impor "foto/360°" sudah disebut di protokol scan repo WebXR — dua hal itu berbeda, jangan disamakan.
- v2.2.0 juga membawa **Map Scale API**, khusus mengoreksi skala metrik pada map Insta360 *standalone* — konfirmasi tambahan bahwa jalur Insta360 punya pipeline pemrosesan sendiri, terpisah dari jalur LiDAR/point-cloud yang dipakai kedua repo saat ini.
- Auth: endpoint pano tunduk pada model scope yang sama (Query/Write/Delete) dengan endpoint VPS yang sudah dipakai, tapi **belum diverifikasi** apakah scope `Query` yang ada sekarang otomatis mencakup `/v1/pano/*`.

**Keputusan (rencana, bukan final):**
1. **Sebelum membeli Insta360 X4/X5**, cek dulu apakah MultiSet sudah mendukung 360 secara umum untuk map yang relevan — verifikasi lewat `GET /v1/pano` dengan credential yang ada, dan cek apakah ada alur uji coba murah (mis. video 360 dari HP) sebelum investasi kamera dedicated.
2. Digerbangi ke **fase pra-launch** di kedua repo — tidak mengganggu prioritas berjalan (Fase 0–5 di repo ini; data POI/tikungan/akurasi di repo WebXR).
3. Kalau jadi dipakai, pano 360 berperan sebagai **lapisan visual tambahan** (tur immersive, atau kandidat fallback saat VPS gagal localize) — **bukan pengganti** point cloud/LiDAR yang sudah terbukti jalan untuk lokalisasi.

**Alasan:** jangan beli hardware sebelum membuktikan software-nya mendukung use case-nya. MultiSet baru merilis fitur pano 11 hari sebelum catatan ini ditulis — belum ada bukti lapangan bahwa alur Insta360→MultiSet cocok dengan koridor RS (sempit, berulang, minim tekstur) yang jadi kasus DARSI.

**Konsekuensi:** tidak ada perubahan kode dari ADR ini di kedua repo. Salinan lengkap ada di `DARSI-Indoor-Navigation-WebXR/docs/DECISIONS.md` (ADR-W013) — kalau salah satu diperbarui (mis. hasil verifikasi `GET /v1/pano`), perbarui juga yang satunya.

---

### ADR-024 — Voice input: Groq (cloud) jadi provider utama, Ollama lokal turun jadi fallback (2026-08-18)

**Keputusan:** Urutan provider LLM untuk ekstraksi tujuan navigasi dari suara dibalik. Sebelumnya Ollama lokal satu-satunya; sekarang **Groq dicoba lebih dulu**, Ollama cuma dipakai kalau Groq gagal atau `groqApiKey` kosong.

**Pemicu:** Ollama lokal (`OllamaConnector.ollamaHost`, hardcoded IP LAN) cuma kejangkau kalau HP dan laptop satu WiFi. Di lapangan (RS/kampus) itu tidak realistis — pemilik project juga tidak selalu bisa membuka laptop di lokasi. Menyalakan Ollama terus-menerus juga memakan RAM tanpa hasil.

**Yang berubah di kode:**
- `ExtractPOI()` → `TryGroq()` dulu, `TryOllama()` sebagai cadangan. Dua-duanya berbagi `SYSTEM_PROMPT` & parser yang sama.
- `groqModel` default `openai/gpt-oss-20b`. **Bukan** `llama-3.1-8b-instant` — model itu resmi dimatikan Groq untuk free/developer tier per **2026-08-16** dan membalas **404** (terverifikasi di lapangan, bukan dugaan).
- Timeout fallback Ollama dipangkas `30s × 2 percobaan` → `8s × 1 percobaan`. Retry ke IP LAN yang sama saat memang beda jaringan tidak pernah berhasil, cuma bikin user menunggu hampir semenit.
- `SYSTEM_PROMPT` diganti dari daftar lokasi **kampus lama** (MMB Studio, Lab Mikrotik, BAAK, dst — sisa scene lama) ke 10 POI RS yang benar-benar ada di scene aktif. `POIManager.sinonimMap` ikut disesuaikan.

**Kredensial:** `groqApiKey` **tidak boleh diisi lewat Inspector** — field publik ikut ter-serialize ke file scene yang di-track git, dan itu sempat kejadian (key ter-commit ke dua scene, dibersihkan sebelum ter-push). Sekarang `Awake()` membaca `groq-api-key.local.txt` di root project (gitignored) kalau field-nya kosong. Untuk rilis produksi, key tetap perlu pindah ke **backend proxy** — APK bisa di-decompile (catatan `ponytail:` ada di kodenya).

**Pengecualian tercatat terhadap CLAUDE.md:** `OllamaConnector.cs` masuk daftar "jangan diubah tanpa alasan kuat". Perubahan ini disengaja & disetujui pemilik project: prompt-nya faktual salah (konteks kampus di app rumah sakit) dan provider lokal terbukti tidak workable di lapangan. Sama seperti ADR-021 untuk `POIData.cs`, ini dicatat, bukan penyimpangan diam-diam.

---

### ADR-025 — Validasi akurasi VPS: HUD ground-truth admin-only, ukur manual pakai meteran (2026-08-18)

**Keputusan:** Akurasi lokalisasi divalidasi dengan **HUD diagnostik di dalam app + pengukuran meteran manual**, bukan sistem logging otomatis. HUD digerbangi mode admin (5× tap di logo DARSI dalam 20 detik, status disimpan di `PlayerPrefs`).

**Pemicu:** Dosen pembimbing (Pak Amma) meminta bukti akurasi posisi yang bisa dibandingkan ke dunia nyata — *"posisi real di titik 1m,1m, di MultiSet ternyata 90cm,90cm"*. Sebelum ini **belum pernah ada angka akurasi sama sekali** di project: yang selama ini terukur cuma *repeatability* (konsistensi antar-localize di titik sama), dan itu **bukan** akurasi. Repo WebXR sudah lebih dulu menabrak jebakan ini (lihat `KNOWN-ISSUES.md` di sana: *"geser kecil = REPEATABILITY, BUKAN AKURASI"*).

**Yang diukur (dua hal terpisah, jangan dicampur):**
1. **Repeatability** — tekan "Set Titik 0" di POI awal, jalan ke POI lain, balik ke titik fisik yang sama. Kalau HUD menunjukkan Δ mendekati 0,0 lagi, VPS konsisten.
2. **Akurasi** — bandingkan jarak yang ditampilkan HUD dengan jarak asli yang diukur meteran antar dua titik itu. Selisihnya = angka offset untuk laporan.

**Isi HUD:** `pos(map)` XYZ, `Δx`/`Δz` dari titik 0, jarak datar dari titik 0, dan **confidence**. Confidence diambil dari `MultiSet.LocalizationSuccessResponse.confidence` (float) lewat event `SingleFrameLocalizationManager.OnLocalizationWithResponse` — ditemukan lewat reflection langsung ke SDK, bukan tebakan. Tanpa confidence, "akurat karena yakin" tidak bisa dibedakan dari "akurat kebetulan".

**Sengaja TIDAK dibangun:** logging otomatis ke file, kalkulator offset di dalam app, PIN tambahan di gerbang admin. Angka dibaca dari layar dan dicatat manual — cara yang sama sudah terbukti cukup di repo WebXR, dan menambah mesin logging sekarang = infrastruktur tanpa pemakai.

**Protokol lapangan (best practice, disepakati):** satu loop sekali jalan **tidak cukup** — ulangi 3–5× di pasangan POI yang sama supaya hasilnya rata-rata, bukan satu titik data yang bisa saja kebetulan. Saat melapor, **sebutkan jumlah percobaannya** ("rata-rata dari N=5"), jangan satu angka telanjang. Kalau waktu masih ada, tambah satu pasang POI di lokasi berbeda — akurasi VPS bisa berbeda antar-lokasi.

**Implementasi:** `Assets/Scripts/LocalizationDebugHUD.cs` (UI dibangun runtime, tidak menyuntik YAML scene), terpasang di scene `TestingHCM` sebagai GameObject `DebugHUD`. Terverifikasi lewat Coplay: 5× tap mengubah `_isAdmin` false→true dan memunculkan panel. Confidence belum terisi di Editor (wajar — tidak ada localize sungguhan tanpa lokasi fisik yang cocok); **verifikasi di device masih tersisa**. Spec: `docs/superpowers/specs/2026-08-18-localization-ground-truth-hud-design.md`.

---

### ADR-026 — RAG Assistant: relevansi diputuskan LLM, bukan ambang skor kemiripan (2026-08-20)

**Konteks.** Fase 3 dari `docs/AI-AVATAR-ASSISTANT.md` (backend RAG) dibangun lebih dulu dan berdiri sendiri, tanpa avatar VRM/TTS/gesture. Implementasinya ada di repo **`darsi-backend`** (GitHub: `RockHead07/DARSI-Indoor-Navigation-Backend`), bukan di repo Unity ini. Spec: `docs/superpowers/specs/2026-08-20-rag-assistant-backend-design.md` di repo tersebut.

**Catatan jujur soal kenapa dibangun sekarang.** RAG **belum diperlukan secara teknis** untuk skala data hari ini: 11 POI + 55 sinonim seluruhnya muat di satu system prompt, dan untuk ukuran itu pendekatan `OllamaConnector.SYSTEM_PROMPT` yang sekarang justru yang paling tepat. Ini keputusan sadar membangun lebih awal untuk kondisi masa depan (multi-gedung, SOP, jadwal) sekaligus fondasi Proyek Akhir semester 6. **"Memakai RAG" bukan kebaruan** dan tidak boleh diklaim begitu; RAG teknologi komoditas. Klaim kontribusi yang sah adalah **retrieval sadar posisi** yang memanfaatkan lantai/gedung dari VPS MultiSet, sesuatu yang tidak dimiliki sistem RAG kebanyakan.

**Keputusan 1 — retrieval hybrid, bukan vector saja.** Prosa (SOP, layanan, FAQ) dicari lewat pgvector; **jadwal dokter sengaja TIDAK di-embed** dan dicari lewat SQL biasa, karena "dr. Fulan praktek jam berapa" itu *lookup*, bukan pencarian makna, dan jam praktek yang salah di RS bukan kesalahan kecil. Ditambah pencarian kata lewat konfigurasi text search `indonesian` bawaan PostgreSQL, digabung dengan **Reciprocal Rank Fusion**. Pemicunya terukur: pertanyaan "igd buka 24 jam tidak" membuat model embedding menaruh chunk **Musholla** di peringkat 1, padahal kata "igd" ada persis di pertanyaan dan di judul chunk yang benar.

**Keputusan 2 (yang utama) — relevansi diputuskan LLM, bukan angka ambang.** Rancangan awal mengasumsikan ada ambang kemiripan yang memisahkan pertanyaan dalam-cakupan dari luar-cakupan. **Asumsi itu terbukti salah saat diukur:** pertanyaan sampah "jadwal kereta ke bandung" mendapat skor cosine **0,348**, sementara pertanyaan sah "sebelum usg boleh makan dulu ga" hanya **0,215**. Sampah menang atas yang sah. Diuji ulang pada model embedding kedua yang lebih besar (`mpnet-base-v2`, 768 dim), hasilnya sama dan malah lebih buruk (sampah naik ke 0,470). **Ini sifat kemiripan cosine, bukan kelemahan satu model — model yang lebih besar membuatnya lebih percaya diri, bukan lebih benar.** Karena itu penolakan pertanyaan di luar urusan RS diserahkan ke LLM yang membaca teks chunk-nya, dan gerbang skor hanya dipertahankan untuk menyaring yang benar-benar jauh.

**Keputusan 3 — model embedding tetap yang kecil.** Migrasi ke `mpnet-base-v2` (1,0 GB, +0,8 GB memori Railway) **diuji lalu dibatalkan**: tidak memperbaiki pemisahan, dan peringkat teratasnya pun tidak lebih baik. Tetap di `paraphrase-multilingual-MiniLM-L12-v2` (384 dim, 0,22 GB).

**Keputusan 4 — `poi_id` diturunkan, tidak pernah dihasilkan LLM.** Diambil dari kolom `poi_unity_id` milik chunk hasil retrieval. Model bahasa tidak andal mereproduksi GUID, dan salah satu karakter menggagalkan navigasi tanpa gejala. Pola yang sama dengan **ADR-021**: satu pemilik sah, sisanya diturunkan.

**Keputusan 5 — corpus tahap ini seluruhnya data SIMULASI**, ditandai kolom `is_simulated` di database (bukan sekadar catatan di dokumen), response membawa `contains_simulated_data`, dan antarmuka wajib menampilkan penandanya. Nama dokter memakai pola "Fulan/Fulanah" supaya jelas karangan. RS Islam A. Yani rumah sakit sungguhan dan izin data operasionalnya belum ada.

**Angka yang sah dilaporkan:** **recall@3 = 71,9%** pada set uji bersih 32 pertanyaan (78,6% untuk 28 pertanyaan dalam cakupan), corpus simulasi 25 dokumen. **Jangan melaporkan angka 100%** yang juga ada di repo itu — angka tersebut berasal dari set yang kegagalannya sudah dipakai memperbaiki sistem. Buktinya terukur: menambal 4 celah kosakata membuat set itu melonjak 85,7% → 100%, sementara set bersih tetap 71,9%. Perbaikannya tidak menular.

**Konsekuensi ke repo ini.** Alur voice Unity (`VoiceInputHandler.cs`, `OllamaConnector.cs`, ADR-024) **tidak berubah sama sekali** — endpoint asisten berdiri sendiri. Groq yang dipanggil dari sisi server juga menutup utang keamanan yang tercatat di komentar `ponytail:` pada `OllamaConnector.cs` (key ikut ter-bundle ke APK) untuk jalur asisten. Keputusan di mana jawaban asisten ditampilkan (WebView pra-AR, panel Unity, atau tombol mic yang sudah ada) **sengaja belum diambil**, menunggu mekanisme retrieval terbukti di lapangan.

**Rujukan lengkap:** `docs/RETRIEVAL-EVALUATION.md` di repo `darsi-backend` (metodologi empat set uji, seluruh angka, batasan, dan daftar klaim yang dilarang). Baca itu sebelum menyetel ambang, mengganti model embedding, atau melaporkan angka apa pun.

---

### ADR-027 — Infrastruktur Backend & Ingress: Zero Trust Tunnel / Managed Cloud vs Penghentian Ketergantungan Railway (2026-08-22)

**Konteks.** Kuota *free-tier* / kredit trial Railway habis. Di sisi lain, tersedia server privat di cloud yang berada di balik jaringan privat (OpenVPN tanpa IP publik). Kebutuhannya: backend FastAPI + PostgreSQL (`pgvector`) harus tetap dapat diakses secara publik dan aman oleh klien Unity (Editor & build APK Android) tanpa hambatan konfigurasi jaringan di perangkat klien.

**Keputusan 1 — Portabilitas arsitektur tetap dijaga (ADR-001 / ADR-014).** Kode FastAPI dan skema SQL tidak mengikat diri pada platform hosting tertentu. Seluruh interaksi database menggunakan standar `DATABASE_URL` via `psycopg` murni, sehingga pergantian host database hanya berupa perubahan variabel *connection string*.

**Keputusan 2 — Ingress server privat via Zero Trust Application Connector (Cloudflare Tunnel).**
Jika menggunakan server pribadi yang berada di balik firewall/OpenVPN:
- Memasang daemon `cloudflared` sebagai *system service* di server.
- Mengarahkan traffic HTTPS publik dari domain/subdomain resmi ke endpoint internal FastAPI (`http://localhost:8000`).
- **Mengapa ini Best Practice:**
  1. *Zero Open Inbound Ports:* Server tidak membuka port apapun ke publik (kebal serangan *port scanning* & *direct DDoS*).
  2. *Tanpa VPN di Klien:* HP Android penguji/pasien langsung mengakses URL HTTPS resmi tanpa perlu menginstal atau menyalakan profil OpenVPN.
  3. *SSL/TLS & WAF Otomatis:* Sertifikat SSL dikelola penuh oleh Cloudflare secara gratis tanpa konfigurasi manual Certbot/Nginx.

**Keputusan 3 — Opsi Managed Serverless Cloud Alternatif (100% Free Tier).**
Jika ingin sistem yang *zero-maintenance* tanpa mengelola server fisik/VPN:
- Database: **Supabase** (PostgreSQL 16 + ekstensi `pgvector` bawaan, 500 MB kuota gratis permanen).
- Compute API: **Koyeb / Render / Fly.io** (menjalankan container FastAPI langsung dari auto-deploy GitHub).

**Keputusan 4 — Standardisasi Dev Environment via Docker Compose.**
Untuk menjaga konsistensi lingkungan lokal dan server:
- Backend dikemas via `docker-compose.yml` (`fastapi` + `pgvector/pgvector:pg16`).
- Pengujian lokal harian dari HP Android menggunakan `adb reverse tcp:8000 tcp:8000` (latensi 0 ms via kabel USB).

**Yang Ditolak:**
- Mewajibkan instalasi OpenVPN di HP penguji/pasien — menyulitkan evaluasi lapangan dan demonstrasi kepada pihak RS/dosen pembimbing.
- Membuka *port forwarding* mentah pada router/firewall tanpa reverse proxy dan WAF.

---

### ADR-028 — Hybrid RAG Voice Navigation, Clinical Triage, & POI GUID Resolution (2026-08-22)

**Konteks.** 
Pengujian suara pengguna di lapangan memunculkan ambiguitas semantik dan *false positive* berbahaya jika hanya mengandalkan pencarian kata kunci (*lexical string matching*):
1. **Insiden Benturan Kosakata (*Keyword Collision*):** Ucapan *"Anakku habis ketabrak motor"* keliru diarahkan ke *"Parkir Motor Karyawan"* karena algoritma pencocokan lokal mencocokkan substring kata *"motor"*. Padahal konteks klinisnya adalah kegawatdaruratan medis yang wajib menuju ke **IGD**.
2. **Bahasa Gaul & Kebutuhan Mendesak (*Colloquial Slang*):** Ucapan *"Aku kebelet kencing"* atau *"Mau pipis"* tidak menemukan rute ke **Toilet** karena belum terdaftar di kamus sinonim dan database korpus RS belum memiliki chunk sanitasi.
3. **Pemisahan Peran (*Separation of Concerns*):** RAG AI dibutuhkan untuk memproses intensi semantik kompleks, tetapi sistem tidak boleh kehilangan kemampuan navigasi saat koneksi internet terputus (*offline fallback*).

**Keputusan 1 — Arsitektur Hybrid: RAG AI sebagai Primary Interpreter & Heuristik Lokal sebagai Fallback.**
- **Primary Route (Cloud/Server RAG):** `VoiceInputHandler.cs` mengutamakan pengiriman teks ke `AssistantClient` (`POST /api/assistant/query`). AI memproses konteks kalimat menggunakan model Transformer dan Groq LLM.
- **Hierarki Resolusi POI Presisi (dikoreksi 2026-08-24 — lihat catatan bug di bawah):**
  1. *Clinical/Answer-Text Priority:* Pindai teks jawaban AI (`ragAnswer.answer`) lebih dulu lewat `POIManager.FindBestMatch`. Ini PRIORITAS UTAMA, bukan langkah ke-3 seperti draf awal ADR ini.
  2. *Metadata Binding:* Fallback ke `ragAnswer.poi_id` (GUID exact match via `POIManager.FindById`) kalau teks jawaban tidak menyebut fasilitas spesifik.
  3. *Entity Name Extraction:* Fallback ke `ragAnswer.poi_name`.
  4. *Direct Keyword Match:* Kalau semua di atas gagal, gunakan pencocokan lokal dari ucapan mentah pengguna.
- **Graceful Offline Fallback:** Jika server offline atau timeout, sistem otomatis beralih ke `POIManager.FindBestMatch` dan `OllamaConnector` lokal tanpa memblokir aplikasi.

**Keputusan 2 — Sinkronisasi GUID POI Statis (11 Titik POI RS Islam A. Yani).**
- 11 POI dari scene `TestingHCM.unity` (`IGD`, `Farmasi`, `Radiologi`, `Ruang X-Ray`, `Resepsionis`, `Toilet`, `Lift Lantai 1`, `Lift Lantai 2`, `Parkir Mobil`, `Parkir Motor Karyawan`, `Ground`) di-sinkronkan ke tabel `pois` PostgreSQL via tool `POISyncWindow.cs` (`POST /api/poi/sync` dengan token `x-admin-token`).
- Seluruh dokumen di `corpus_simulasi.py` diikat ke `poi_unity_id` yang sesuai sehingga API RAG langsung mengembalikan GUID POI yang valid.

**Keputusan 3 — Penyetelan System Prompt & Triase Klinis di Backend.**
- `_SYSTEM_PROMPT` di `app/assistant/generation.py` disetel tegas:
  > *"TRIASE GAWAT DARURAT: Jika pengguna menyebutkan kondisi gawat darurat atau kecelakaan (tertabrak, pendarahan, patah tulang, pingsan, kejang, demam tinggi/step anak), WAJIB langsung mengarahkan pasien untuk segera menuju ke IGD di Lantai 1 tanpa perlu menunggu pendaftaran poli."*

**Keputusan 4 — Validasi Standar Industri: LLM-as-a-Judge Benchmark Suite.**
- Dibangun script benchmark otomatis `scripts/eval_llm_judge.py` berisi 52 skenario rumah sakit yang mencakup 7 domain (Gawat Darurat, Poliklinik, Farmasi, Diagnostik, Administrasi/BPJS, Fasilitas Umum, Out-of-Scope).
- Hasil evaluasi mencapai **100.0% Pass Rate (52/52 Passed)** pada keselamatan triase, ketepatan rute, dan ketahanan anti-halusinasi.

**Yang Ditolak:**
- Mengandalkan *single word matching* untuk memutuskan tujuan navigasi tanpa memeriksa intensi semantik kalimat.
- Menghapus total modul pencocokan lokal (harus tetap dipertahankan sebagai *fallback resiliency*).

**Catatan koreksi (2026-08-24) — bug lapangan nyata di Keputusan 1, sudah diperbaiki.**
Skenario yang jadi alasan dibangunnya ADR ini ("Anakku habis ketabrak motor")
justru masih gagal saat dites sungguhan lewat Bifrost live: jawaban teksnya
sudah benar ("Silakan langsung menuju IGD..."), tapi `poi_id` yang dikembalikan
tetap "Parkir Motor Karyawan". Penyebab: `poi_id`/`poi_name` diturunkan HANYA
dari chunk retrieval rank-1 (ADR-026) yang tidak tahu apa-apa soal triase —
untuk query ini retrieval salah pilih chunk parkir (match kata "motor").
Kode `VoiceInputHandler.cs` menaruh cek metadata (tahap 1-2 draf awal) SEBELUM
scan teks jawaban (tahap 3 draf awal), jadi begitu metadata "berhasil"
menemukan POI manapun (walau salah), scan teks jawaban tidak pernah sempat
jalan. Diperbaiki dengan menukar urutan: scan teks jawaban AI sekarang jalan
duluan (lihat hierarki terkoreksi di atas). Trade-off yang disadari: jawaban
tanpa nama fasilitas eksplisit tetap fallback ke metadata seperti sebelumnya,
jadi tidak ada regresi untuk kasus informasi umum.

**Catatan koreksi (2026-08-23) — angka 100% pada Keputusan 4 belum boleh dipercaya.**
`eval_llm_judge.py` punya 3 cacat metodologi belum diperbaiki: kegagalan panggilan
juri default PASS (fallback di blok `except`), `GROQ_API_KEY` kosong juga default
PASS, dan model juri = model generator (`openai/gpt-oss-20b` dua-duanya, jadi
sistem menilai dirinya sendiri). Jangan kutip 100% pass rate ini di laporan
mana pun sampai ketiganya diperbaiki dan diukur ulang.

**UPDATE 2026-08-24 — kode-nya sudah diperbaiki, angka 100% (52/52) LAMA tetap
tidak boleh dikutip sampai diukur ulang.** Perbaikan di `scripts/eval_llm_judge.py`
(repo `darsi-backend`): (1) kegagalan panggilan juri sekarang verdict `ERROR`
eksplisit, dipisah dari PASS/FAIL, tidak lagi otomatis lolos; (2) `GROQ_API_KEY`
kosong sekarang `SystemExit`, bukan lolos otomatis; (3) juri (`gpt-oss-20b` via
Groq) dan generator SEKARANG BEDA MODEL sejak ADR-029 (Bifrost/medgemma jadi
primer) — masalah "menilai diri sendiri" berkurang untuk kasus normal, TAPI
kalau Bifrost gagal dan jatuh ke fallback Groq, generator ikut jadi `gpt-oss-20b`
lagi (skrip belum bisa mendeteksi kapan ini terjadi, karena `generation.py`
tidak mengembalikan info provider mana yang menjawab). URL target default yang
lama (quick tunnel mati) juga dihapus — sekarang wajib diisi eksplisit lewat
`TARGET_URL`, tidak lagi diam-diam menguji host yang sudah tidak ada.
**Belum dijalankan ulang** — 52/52 (100%) LAMA masih angka dari kode yang
rusak, dan angka BARU belum ada sampai `python -m scripts.eval_llm_judge`
benar-benar dieksekusi dan hasilnya dicatat di sini.

---

### ADR-029 — Provider LLM Utama Pindah dari Qwen-Lokal-di-`vm-amma` ke Bifrost Gateway Eksternal (2026-08-23)

**Konteks.**
ADR-028 (Keputusan 1) menyebut "Groq LLM" sebagai provider utama RAG, tapi
antara ADR-028 ditulis dan sekarang, `app/assistant/generation.py` di repo
`darsi-backend` sempat diubah (commit `0924d6a`) supaya **Qwen lokal (`qwen2.5:7b`
via Ollama Docker) jadi provider UTAMA**, Groq turun jadi fallback — dengan
asumsi server deploy (`vm-amma`) punya GPU NVIDIA (jawaban pemilik project
sebelum dicek langsung).

Verifikasi langsung di server (`lspci | grep nvidia`, `systemd-detect-virt`,
`nproc`, `free -h`) membuktikan **`vm-amma` adalah VM KVM tanpa GPU sama
sekali** (bukan cuma driver belum terpasang — device-nya memang tidak
ter-*attach*), cuma 2 vCPU. Inferensi 7B parameter CPU-only butuh 1-2 menit
per jawaban, jauh melebihi timeout 30 detik yang sudah disetel — konsekuensinya
Ollama akan SELALU gagal ke Groq, tidak pernah benar-benar jadi primer. Ini
kali kedua asumsi infrastruktur ("ada GPU"/"server terjangkau") ternyata salah
tanpa verifikasi langsung di proyek ini (kali pertama: server OpenVPN-only yang
tak terjangkau dari Railway, pemicu ADR-027).

**Keputusan.** Cabut rencana Qwen-lokal-di-`vm-amma` sepenuhnya (kode dan
service `ollama` di `docker-compose.yml` dihapus, bukan dinonaktifkan — tidak
ada alasan menyimpan kode mati untuk hardware yang tidak akan pernah ada di
server ini). Sebagai gantinya: **Bifrost, gateway OpenAI-compatible eksternal
yang di-host tim PSDKU/HCM Lab (`bifrost.hcm-lab.id`, GPU sungguhan), jadi
provider UTAMA.** Model yang dipakai: `llama.cpp/medgemma-1.5-4b-it-q4` —
di-tuning domain medis, dipilih ketimbang `gemma-4-12b-it-q8` (general-purpose)
karena relevansi ke kasus pakai asisten RS. Groq tetap FALLBACK, tidak berubah
dari ADR-028.

**Kenapa bukan "minta GPU lebih besar ke pemilik server"**: `vm-amma`
kemungkinan besar server pinjaman pembimbing (Pak Amma), jadi menambah beban
resource di situ bukan sepenuhnya keputusan sepihak proyek ini. Bifrost
menghindari trade-off itu — inferensi berat berjalan di server pihak lain yang
memang diperuntukkan untuk itu, `vm-amma` cukup jadi proxy/API caller.

**Detail teknis yang beda dari Groq**: autentikasi Bifrost pakai header
`x-api-key`, BUKAN `Authorization: Bearer` seperti Groq/OpenAI-compatible pada
umumnya — kalau ada provider OpenAI-compatible baru lagi ke depan, jangan
asumsikan format auth sama, cek dulu.

**Belum diverifikasi saat ADR ini ditulis**: panggilan sungguhan ke endpoint
Bifrost belum pernah dites end-to-end (kunci API baru diterima, kode baru
ditulis). Prioritas lapangan berikutnya: satu panggilan uji manual dari
`AssistantTestPanel.cs` atau `curl` langsung untuk memastikan format respons
(`choices[0].message.content`) sungguhan cocok dengan asumsi kode.

**Yang Ditolak:**
- Model kecil (`qwen2.5:1.5b`) sebagai fallback terakhir di `vm-amma` — dibatalkan
  begitu ada alternatif GPU eksternal yang lebih baik (medgemma, tuning domain
  medis) tanpa trade-off kecepatan.
- Menyimpan service `ollama` di `docker-compose.yml` dalam keadaan nonaktif
  "untuk jaga-jaga" — kode mati untuk hardware yang tidak akan pernah ada di
  server ini cuma menambah kebingungan pembaca berikutnya.

---

### ADR-030 — Isolasi Pengembangan Eksperimental AI 3D Avatar di Branch Terpisah (`feature/vrm-avatar-assistant`) (2026-08-24)

**Konteks.**
Modul RAG Assistant backend saat ini sedang dalam fase pematangan dan evaluasi klinis/retrieval (pengujian performa, akurasi jadwal dokter terstruktur, dan validasi device fisik). Di saat bersamaan, kebutuhan untuk memulai pembuatan dan integrasi visual 3D Avatar Assistant (VRM, Mecanim, Look-At, Lip-Sync) mulai disiapkan.

Sesuai prinsip arsitektur, RAG adalah "otak" dan Avatar 3D adalah "kulit". Mencampur pengembangan visual 3D ke dalam branch utama saat RAG masih diverifikasi berisiko merusak kestabilan scene navigasi aktif (`WholePSDKU`) dan mengaburkan pengujian lapangan.

**Keputusan.**
1. **Branch Terisolasi:** Seluruh pengembangan aset 3D, paket UniVRM, skrip pengontrol avatar, dan pipeline visual dilakukan di branch terpisah: `feature/vrm-avatar-assistant`.
2. **Pengembangan Bertahap (MVP Incremental):**
   - **Tahap 1 (Visual Companion / Passive):** Fokus pada kemunculan avatar saat dipicu (*trigger spawn*), tatapan dinamis ke kamera (`VRMLookAtHead`), animasi dasar (*Idle, Wave, Pointing*), dan *fade-out despawn* tanpa dependensi TTS/audio streaming (pola *Mita/Pokemon GO*).
   - **Tahap 2 (Audio & Viseme Lip-Sync):** Integrasi TTS dan sinkronisasi bibir berbasis *mock data/contract-first*.
3. **Sandbox Scene Terpisah:** Dilarang mengubah scene produksi `WholePSDKU` di branch utama selama eksplorasi awal. Semua pengujian avatar dilakukan di scene sandbox (mis. `Assets/Scenes/Sandbox_AvatarCompanion.unity`).
4. **Merge Gate:** Branch `feature/vrm-avatar-assistant` baru diizinkan untuk di-merge ke branch `main` setelah RAG backend terbukti 100% stabil pada pengujian perangkat Android fisik.

**Yang Ditolak:**
- Mengembangkan avatar 3D langsung di branch `main` saat RAG masih dalam proses pematangan dan belum diuji di device fisik.
- Menghubungkan avatar langsung ke endpoint RAG yang belum stabil tanpa melalui tahap *mock data / contract-first*.

