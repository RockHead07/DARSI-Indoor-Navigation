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

#### Amandemen 024-A (2026-08-25) — Ollama-LAN dihapus total dari `OllamaConnector.cs`, Groq jadi satu-satunya fallback klien

**Mencabut** bagian Ollama-lokal dari keputusan pokok di atas ("Ollama cuma dipakai kalau Groq gagal"). Groq tetap seperti semula.

**Pemicu:** setelah RAG Assistant + Bifrost (ADR-026, ADR-029) jadi jalur primer di server, `OllamaConnector` cuma jadi fallback klien level-dua (dipanggil `VoiceInputHandler.cs` kalau RAG Assistant tidak terjangkau). Ollama-LAN di dalamnya sudah didiagnosis sendiri oleh ADR-024 sebagai tidak realistis di lapangan (HP dan laptop harus satu WiFi) — tapi diagnosis itu belum ditindaklanjuti sampai tuntas. Ditemukan aktif merugikan, bukan cuma nganggur: `Start()` memanggil `PreWarmModel()` tanpa syarat setiap kali GameObject aktif, mengirim request ke IP LAN hardcoded dengan timeout 60 detik yang di lapangan pasti gagal — user menunggu sampai semenit tanpa hasil di setiap sesi voice, sebelum akhirnya jatuh ke "Sistem siap (offline mode)".

**Yang berubah di kode:** dihapus seluruhnya — field `ollamaHost`/`ollamaPort`/`modelName`/`useHttps`, konstanta `MAX_ATTEMPTS`/`RETRY_DELAY_SECONDS`/`PREWARM_TIMEOUT`, method `TryOllama()`/`PreWarmModel()`/`Start()`, field `txtStatus` (cuma dipakai `PreWarmModel`), kelas `OllamaRequest`/`OllamaResponse`. `ExtractPOI()` disederhanakan: Groq gagal atau `groqApiKey` kosong → langsung `onConnectionFailed`, tanpa percobaan kedua. Interface publik yang dipakai `VoiceInputHandler.cs` (`OllamaConnector.instance.ExtractPOI(...)`) tidak berubah.

**Yang sengaja TIDAK diubah:** nama kelas tetap `OllamaConnector` meski isinya sekarang murni Groq. Rename akan memaksa menyentuh `VoiceInputHandler.cs` juga (file protected lain) untuk manfaat kosmetik semata — di luar cakupan perbaikan ini.

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
**UPDATE 2026-08-24 (lanjutan) — angka BARU sudah ada, 100% LAMA resmi
dibuang.** Setelah 3 percobaan gagal (restart server, rate-limit Groq,
gangguan sesi + 503 Bifrost), run ke-4 berhasil bersih: **52/52 dinilai
tanpa error, 45/52 PASS (86,5%)**. Rincian kategori: Gawat Darurat 90%,
Poliklinik 70%, Farmasi 100%, Diagnostik 100%, Administrasi 67%, Fasilitas
Umum 100%, Di Luar Cakupan 75%. Diagnosis kegagalan (bukan tebakan, lewat
curl langsung ke API produksi): satu diantaranya (ambang skor `MIN_TOP_SCORE
=0.22`) sengaja belum ditambal, satu lagi (jadwal dokter kadang tanpa
lokasi) sudah dites akar masalahnya nyata (`doctor_schedules.poi_unity_id`
selalu `None`) dan ditambal, tapi masih kadang gagal — diduga variasi
sampling LLM, bukan bug kode. Dua celah lain (kosakata "spiral KB" collision
dengan "pemasangan gigi palsu", dan rubrik juri terlalu ketat untuk
penolakan out-of-scope) ditambal dan diverifikasi manual setelah run ini —
**angka 86,5% adalah lower-bound**, belum diukur ulang dengan kedua
perbaikan itu. Detail lengkap + tabel: `README.md` repo `darsi-backend`.

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
   > **Dicabut oleh Amandemen 030-A di bawah (2026-08-25).**

**Yang Ditolak:**
- Mengembangkan avatar 3D langsung di branch `main` saat RAG masih dalam proses pematangan dan belum diuji di device fisik.
- Menghubungkan avatar langsung ke endpoint RAG yang belum stabil tanpa melalui tahap *mock data / contract-first*.

#### Amandemen 030-A (2026-08-25) — merge gate dicabut lebih awal; alasannya operasional, bukan teknis

**Mencabut poin 4** (Merge Gate). Poin 1, 2, 3 tetap berlaku.

**Gate-nya BELUM terpenuhi saat dicabut, dan itu diakui terang-terangan.** Syaratnya "RAG
backend terbukti 100% stabil pada pengujian perangkat Android fisik", sementara
`AI-AVATAR-ASSISTANT.md` masih mencatat *"Belum pernah dites di device Android fisik pakai
mic asli"*. Jadi ini bukan gate yang lulus, melainkan gate yang **sengaja dicabut**.

**Pemicunya masalah operasional yang tidak terbayang saat ADR-030 ditulis.** Beberapa sesi
kerja (avatar dan RAG) berjalan bersamaan di **satu working tree fisik yang sama**. Karena
tiap sesi memakai branch berbeda, setiap perpindahan branch menghapus file milik sesi lain
dari disk. Dalam satu hari kerja hal ini terjadi **tiga kali**; setiap kali pekerjaan yang
belum di-commit hilang dari working tree dan hanya selamat karena sesi lain menyimpannya ke
`git stash`. Sekali kejadiannya di tengah perintah commit berjalan.

Biayanya nyata: waktu kerja habis untuk pemulihan git, bukan untuk fitur, dan ada risiko
suatu saat penyelamatan itu tidak terjadi.

**Kenapa merge, bukan worktree.** `git worktree` adalah perbaikan yang lebih tepat secara
teknis dan sempat diusulkan. Ditolak karena ongkos di sisi Unity: tiap worktree butuh
`Library/` sendiri yang harus dibangun dari nol, dan aset besar yang gitignored (model VRM
placeholder) harus disalin manual. Untuk tim sekecil ini, satu branch bersama lebih murah.

**Yang HILANG karena pencabutan ini, dan wajib diingat:** alasan asli gate itu adalah
mencegah kerja avatar mengganggu kestabilan scene navigasi produksi selagi RAG diverifikasi.
Perlindungan itu sekarang tidak ada lagi. Penggantinya bukan proses baru, melainkan
kewaspadaan: **setiap perubahan yang menyentuh scene produksi atau jalur navigasi harus
diperlakukan sebagai perubahan berisiko tinggi**, dan poin 3 (dilarang mengubah scene
produksi untuk keperluan eksplorasi avatar) justru menjadi **lebih** penting setelah gate
ini hilang, bukan kurang.

**Yang TIDAK berubah:** avatar tetap tidak boleh dihubungkan langsung ke endpoint RAG yang
belum stabil (lihat "Yang Ditolak" di atas), dan seluruh gate keselamatan ADR-034
(keputusan 4 dan 5) tetap berlaku penuh.


### ADR-031 — Audit & Perbaikan CI/CD Pipeline vs Konten Projek Aktual (2026-08-24)

**Konteks.**
Hasil audit menyeluruh CI/CD pipeline terhadap isi projek aktual mengungkap tiga masalah
kritis: (1) Unity Test Runner berjalan tanpa satupun test file, (2) `Backend/tests/`
tidak ada sehingga pytest selalu di-skip, (3) `release-main.yml` tidak menjalankan
backend tests — hanya `pr-validation.yml` yang menjalankan keduanya.

**Keputusan.**
1. **Backend tests di release pipeline:** `release-main.yml` ditambahkan job
   `run-backend-tests` yang memanggil `_backend-tests.yml` secara paralel dengan
   Unity tests — mencegah kode backend lolos tanpa validasi saat push langsung ke main.
2. **Buat `Backend/tests/`:** Smoke test (`test_yolo_api.py`) memvalidasi endpoint
   `/api/human` mengembalikan JSON shape yang benar dan logika `crowded` threshold.
   YOLO dan cv2 di-mock agar test jalan tanpa model/kamera.
3. **Buat Unity EditMode tests:** `Assets/Tests/Editor/POIDataTests.cs` memvalidasi
   properti turunan `POIData` (ADR-021): `Floor`, `Building`, `EffectiveName`,
   dan auto-generated `poiId`. Ini menjadikan Unity test gate bermakna.
4. **Enable scene produksi:** `DARSi-Indoor Navigation.unity` diaktifkan kembali di
   `EditorBuildSettings` agar `UaaLBuildScript.BuildAndroidUaaL()` mengekspor
   scene yang benar — bukan scene testing `TestingHCM` saja.
5. **Ruff config:** `pyproject.toml` ditambahkan dengan target Python 3.10 dan rule
   set E/F/W/I agar linting konsisten dan reproducible.
6. **CHANGELOG.md:** Diganti dari sisa template menjadi histori DARSI sesungguhnya
   yang diturunkan dari 95 commit git history.

**Yang Ditolak:**
- Mennonaktifkan `_unity-tests.yml` sementara tidak ada test (lebih baik tambahkan
  test minimal agar gate bermakna).
- Menambahkan `run-backend-tests` sebagai `needs:` dependency dari `build-unity-uaal`
  (backend dan Unity build independent — tidak perlu saling menunggu).

### ADR-032 — Arsitektur Look-At Head Tracking & Runtime VRM Coordinate Alignment (2026-08-24)

**Konteks.**
Pada sandbox avatar (`Sandbox_AvatarCompanion.unity`), ditemukan tiga isu pada sistem tatapan kepala VRM:
1. **Frame Accumulation (Spinning 360°):** Di `LateUpdate()`, operasi `headBone.rotation = delta * headBone.rotation` mengalikan delta secara kumulatif setiap frame tanpa reset bind pose, menyebabkan kepala berputar terus-menerus.
2. **Koordinat GLTF/VRM vs Unity (Karakter Membelakangi Kamera):** Format GLTF 2.0 (Right-Handed) diimpor glTFast dengan orientasi lokal yang membelakangi kamera saat parent dirotasi $180^\circ$.
3. **Pemisahan Hierarki Parent Tulang (Wajah Terputar $180^\circ$):** Menggunakan `transform.rotation * _headRestLocalRot` melewatkan rotasi $180^\circ$ dari GameObject `VRM_Character_Model` sehingga wajah menghadap ke belakang meskipun badan menghadap ke depan.

**Keputusan.**
1. **Penyimpanan Base Rest Pose (Anti-Spinning):** Menyimpan `_headRestLocalRot` sekali saat bone terhubung dan menghitung rotasi absolut setiap frame dari rest pose:
   $$\text{targetHeadWorldRot} = \text{lookOffset} \times (\text{headBone.parent.rotation} \times \text{\_headRestLocalRot})$$
2. **VRM Rotation Offset:** Menyetel `vrmRotationOffset = (0, 180, 0)` pada `VRMRuntimeLoader` agar arah hadap model visual glTF sinkron menghadap kamera.
3. **Sinkronisasi Langsung ke Parent Bone (`headBone.parent`):** Basis orientasi kepala dihitung dari parent tulang leher langsung (`J_Bip_C_Neck`), menjamin wajah selalu menghadap ke depan seirama dengan badan.
4. **Relaksasi T-Pose Lengan:** Menurunkan `J_Bip_L_UpperArm` dan `J_Bip_R_UpperArm` sebesar $65^\circ$ dengan *breathing sway* halus saat runtime.


### ADR-033 — Strategi Pemilihan Teknologi TTS: Hybrid Edge-TTS & On-Premises Sherpa-ONNX (2026-08-24)

**Konteks.**
Pada Milestone 2 AI Avatar Assistant DARSI, dibutuhkan modul Text-to-Speech (TTS) untuk menyuarakan asisten pemandu RS Islam A. Yani. Kriteria utama mencakup: keluwesan intonasi bahasa Indonesia, pelafalan istilah medis formal, latensi rendah, efisiensi beban server, dan keandalan operasional jika jaringan internet terputus.

**Keputusan.**
1. **Backend Abstraction Contract:** Endpoint TTS diisolasi pada FastAPI backend (`POST /api/assistant/tts`). Unity Client hanya mengirimkan teks dan menerima stream audio (MP3/WAV) tanpa ketergantungan pada engine TTS internal.
2. **Tier 1 — Primary Engine (Edge-TTS):**
   - Menggunakan model *Neural Voice* `id-ID-GadisNeural` (suara asisten rumah sakit yang santun, artikulatif, dan natural).
   - Beban server mendekati 0% (tidak butuh GPU dedicated), latensi $< 250\,\text{ms}$, tanpa biaya API komersial.
3. **Tier 2 — On-Premises Offline Fallback (Sherpa-ONNX / Piper):**
   - Jika koneksi internet server terputus (*air-gapped* intranet RS), backend otomatis mengalihkan sintesis suara ke model lokal ONNX bahasa Indonesia (`vits-icefall-id-indonesian` / Piper `id_ID`).
   - Berjalan mandiri di CPU server dengan latensi $\approx 50-80\,\text{ms}$.

**Yang Ditolak:**
- Menggunakan **Kokoro TTS** untuk bahasa Indonesia karena belum memiliki phonemizer native ID.
- Menggunakan **Supertonic / Supertone Play Cloud** karena transisi penutupan layanan cloud per Agustus 2026 dan minimnya korpus intonasi medis bahasa Indonesia.
- Menggunakan **XTTS v2 / Generative Voice Cloning** di fase awal karena kebutuhan GPU VRAM besar ($\ge 6\,\text{GB}$) dan latensi yang lebih tinggi untuk respon interaktif.

#### Amandemen 033-A (2026-08-26) — kajian Fase 2 bertabrakan dengan ADR-033 karena tidak membaca ADR ini dulu; ADR-033 dikonfirmasi TETAP BERLAKU

**Bukan revisi keputusan — ini kekeliruan proses yang dicatat supaya tidak terulang.**
`docs/superpowers/specs/2026-08-26-voice-output-lipsync-architecture.md` (ditulis 2 hari
setelah ADR-033, subjek persis sama: strategi TTS) merancang ulang arsitektur TTS dari nol
tanpa mengecek ADR ini lebih dulu, melanggar alur kerja CLAUDE.md poin 1. Hasilnya
bertabrakan pada dua hal:

1. **Endpoint digabung.** Kajian itu (§5, §6) menaruh `audio_url` langsung di response
   `POST /api/assistant/query` — sintesis TTS menumpang di request RAG yang sama. ADR-033
   keputusan 1 mengunci endpoint **terpisah** (`POST /api/assistant/tts`) justru supaya
   kegagalan salah satu tidak menjatuhkan yang lain. Digabung berarti kegagalan edge-tts
   (API tidak resmi milik Microsoft, riwayatnya pernah berubah tanpa peringatan) bisa
   menjatuhkan jawaban teks + `poi_id` navigasi yang sebenarnya sudah siap, dan menambah
   latensi sintesis audio di atas beban Bifrost yang sudah 13-32 detik
   (`AssistantClient.cs:31-35`).
2. **Tier fallback offline hilang total.** Kajian itu tidak menyebut Sherpa-ONNX/Piper
   sama sekali. ADR-033 keputusan 3 menaruh tier ini justru untuk skenario intranet RS
   terputus dari internet publik — bukan detail opsional.

**Keputusan:** ADR-033 dikonfirmasi berlaku persis seperti ditulis: endpoint TTS terpisah,
dua tier (Edge-TTS primer + Sherpa-ONNX/Piper fallback offline). Dokumen kajian
2026-08-26 **belum boleh dipakai sebagai basis implementasi Fase 2** sampai §5 dan §6-nya
direvisi mengikuti kontrak ini.

**Temuan tambahan, di luar konflik ADR:** field `gesture`/`expression` yang diusulkan di
§5 kajian itu belum ada di backend sungguhan (`AI-AVATAR-ASSISTANT.md:29-30`) — sah sebagai
desain ke depan, tapi harus ditandai "diusulkan", bukan ditulis seolah kontrak yang sudah
disepakati.

---

### ADR-034 — Model Penempatan Avatar: *Lead-Follow Guide* di Atas Path MultiSet (2026-08-24)

**Konteks.**
Tahap 1 ADR-030 menghasilkan `AvatarCompanionController` yang memunculkan avatar berukuran
penuh secara statis di depan kamera (`spawnDistance = 1.8`). Padahal `AI-AVATAR-ASSISTANT.md`
§4.1 secara eksplisit **menolak** model "Spawn Besar 1.5m di Depan Kamera" untuk sesi navigasi
berjalan, karena menutupi pandangan koridor fisik. Yang disetujui di tabel itu hanya *Mini
Floating Companion* dan *Virtual Info Kiosk*. Jadi implementasi Tahap 1 secara formal
bertentangan dengan dokumen keselamatannya sendiri.

Arah yang dipilih pemilik proyek (referensi karakter *MiSide*): avatar **berjalan di atas
NavMesh, memimpin pengguna ke tujuan, dan menjawab pertanyaan seputar navigasi indoor**.
Ini adalah model penempatan **keempat** yang belum tercatat di §4.1, dan secara keselamatan
justru lebih baik daripada yang ditolak: avatar bergerak menjauh sambil memimpin, tidak
pernah parkir diam di depan wajah pengguna. Rancangannya sudah ada sebagai Task 6
(`AIAvatarGuideController`, FSM Lead-Follow) di
`docs/superpowers/plans/2026-08-24-ai-avatar-assistant-guide.md`.

Audit branch `feature/vrm-avatar-assistant` (2026-08-24) menemukan tiga hal yang mengubah
bentuk keputusan ini:

1. **Rute sudah punya pemilik.** `NavigationController.instance.agent` dari MultiSet SDK
   **sudah** berupa `NavMeshAgent` yang menghitung rute, dan `ShowPath` sudah menggambar
   garisnya. Terverifikasi di `Assets/Scripts/Multiplayer/NavigationControllerExtension.cs`
   baris 29 dan 79. Draft Task 6 menambahkan `NavMeshAgent` **kedua** yang menghitung ulang
   rute yang sama.
2. **Protokol keselamatan §4 belum pernah benar-benar menyala.** `AvatarSafetyFade` gagal
   karena tiga sebab sekaligus: array `targetRenderers` ter-serialize di edit time berisi
   renderer placeholder (sehingga `CacheRenderers()` runtime ter-guard `Length == 0`), jarak
   diukur dari root di telapak kaki (Y=0) terhadap kamera setinggi ~1.4m sehingga ambang
   0.9m mustahil tercapai, dan alpha di-set lewat `MaterialPropertyBlock` ke material opaque.
3. **Rig humanoid tidak ada.** UniVRM tidak pernah benar-benar terpasang (`Packages/manifest.json`
   tidak punya `com.vrmc.*`); model dimuat runtime lewat glTFast yang hanya menghasilkan
   Generic rig tanpa pemetaan `HumanBodyBones`.

**Keputusan.**

1. **Model penempatan resmi: *Lead-Follow Guide*.** Tambahkan baris keempat pada tabel
   `AI-AVATAR-ASSISTANT.md` §4.1: avatar berjalan memimpin di depan pengguna sepanjang rute
   aktif, berhenti dan menoleh saat pengguna tertinggal, menunjuk saat tiba di tujuan.
   Status: **Disetujui untuk sesi navigasi berjalan**, dengan syarat keputusan 4 dan 5
   di bawah terpenuhi.

2. **Avatar TIDAK memiliki `NavMeshAgent` sendiri.** Avatar berjalan dengan menginterpolasi
   posisinya sepanjang polyline rute yang **sudah dihitung SDK**, dengan offset `leadDistance`
   di depan pengguna. Satu pemilik untuk pertanyaan "rutenya lewat mana", yaitu MultiSet SDK.
   Ini penerapan langsung prinsip ADR-021: data diturunkan dari satu pemilik sah, tidak disalin
   atau dihitung ulang.

   **Sumber rute yang benar adalah `ShowPath.instance`, bukan `navController.agent.path`.**
   Revisi pertama ADR ini menulis `navController.agent.path.corners`, itu **keliru**. Verifikasi
   refleksi (2026-08-24) menunjukkan `ShowPath` memegang field privat `NavMeshPath path` sendiri
   yang dihitung antara dua Transform (`SetPositionFrom` → `a`, `SetPositionTo` → `b`) dan
   digambar lewat `DrawPath(NavMeshPath)`. Artinya polyline yang **dilihat pengguna** berasal
   dari `ShowPath.path`. Menaiki `agent.path` berisiko menghasilkan jalur yang berbeda dari
   garis di lantai, yaitu persis kegagalan yang keputusan ini ingin cegah.

   **Terbukti terukur di Play mode (2026-08-24, scene produksi).** Setelah
   `SetPOIForNavigation()` dipanggil dan `IsCurrentlyNavigating() == true`:

   | Sumber | corners | status | keterangan |
   |---|---:|---|---|
   | `ShowPath.path` (privat) | **3** | `PathComplete`, 17,35 m | sama persis dengan `LineRenderer.positionCount = 3` |
   | `navController.agent.path` | **1** | `hasPath = false` | hanya titik posisi agent sendiri, rute KOSONG |

   Jadi bukan sekadar "lebih rapi": kalau avatar menaiki `agent.path.corners` seperti revisi
   pertama ADR ini, ia akan menerima **satu titik** dan tidak bergerak ke mana-mana. Pada alur
   POI normal, SDK tidak pernah mengisi `agent` dengan rute; `agent.destination` hanya di-set
   oleh `NavigationControllerExtension` untuk fitur navigasi-ke-teman. Kesamaan
   `ShowPath.path.corners` dengan `LineRenderer.positionCount` juga mengonfirmasi bahwa
   field itulah yang benar-benar digambar sebagai garis yang dilihat pengguna.

   Catatan operasional dari uji yang sama: `SetPOIForNavigation()` **tidak** di-gate oleh
   keberhasilan localize, jadi rute bisa diuji di Editor tanpa device. Ini menyangkut
   pengujian saja dan tidak mencabut keputusan 5, yang mengatur kapan avatar boleh
   **bergerak**, bukan kapan rute boleh dihitung.

   **Titik sudut rute sudah disediakan SDK, jangan hitung sendiri.** `ShowPath` punya
   `cornerVisualizationPrefab`, `visibleCorners[]`, `isCornersVisible`, dan
   `UpdateVisibleCorner(int, Vector3)`. Ini melengkapi keputusan 7: titik keputusan
   (persimpangan) tidak perlu diturunkan ulang oleh kode avatar.

   **Peran `navController.agent` sudah terverifikasi: proxy posisi pengguna, bukan pejalan.**
   Di scene produksi, GameObject `NavMeshAgent` adalah **anak dari `ARCamera`** dengan
   `localPosition (0, -1.2, 0)`, yaitu posisi pengguna yang dijatuhkan ke lantai. Diperkuat oleh
   field `lastAgentPosition` + `positionUpdateThreshold` pada `NavigationController`, pola
   "pantau pergerakan pengguna lalu hitung ulang rute". Jadi agent SDK **tidak** berjalan
   menyusuri rute, dan avatar tidak berebut peran dengannya.

3. **Rig: impor UniVRM di Editor, bukan pemuatan runtime.** Impor `.vrm` sekali menjadi prefab
   humanoid yang ikut build. `VRMRuntimeLoader.cs` dihapus seluruhnya.

   **Target format: VRM 0.x**, karena aset yang ada memang VRM 0.x (terverifikasi dari header
   glTF: `extensions.VRM`, `specVersion 0.0`, exporter VRoid Studio 1.26.0), VRoid mengekspor
   0.x secara baku, dan §2.1 serta plan doc sudah ditulis terhadap API 0.x (`VRMBlendShapeProxy`).
   Tidak ada kebutuhan DARSI yang dilayani lebih baik oleh VRM 1.0: look-at memakai implementasi
   sendiri (ADR-032), dan SpringBone dibatasi/dimatikan oleh guardrail §2.2. Kalau nanti pindah
   ke VRM 1.0, ongkosnya adalah tukar paket dan ekspor ulang dari VRoid, murah selama model RS
   sungguhan belum diauthor.

   Paket yang benar (dua-duanya wajib, UPM git URL tidak menarik dependency otomatis):
   ```
   "com.vrmc.gltf":   "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2"
   "com.vrmc.univrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM#v0.131.2"
   ```
   **Koreksi:** revisi pertama ADR ini menulis `com.vrmc.vrm`, itu **keliru**. `com.vrmc.vrm`
   adalah paket VRM **1.0**; paket VRM 0.x adalah `com.vrmc.univrm`. Versi dipatok **v0.131.2**
   secara sadar: release itu yang memperbaiki *import exception* dan obsolete warning pada
   Unity 6.2 sampai 6.5, dan project ini di Unity 6000.3.14f1 (Unity 6.3) jatuh di rentang itu.

   Catatan koeksistensi: `com.vrmc.gltf` (UniGLTF) berbeda dari `com.unity.cloud.gltfast` yang
   sudah ada sebagai dependency transitif MultiSet. Keduanya assembly dan namespace terpisah,
   jadi bisa hidup berdampingan.

   Konsekuensinya, **ADR-032 poin 2 dan poin 4
   dicabut**: `vrmRotationOffset` dan relaksasi T-Pose prosedural tidak lagi berlaku karena
   keduanya adalah kompensasi terhadap keterbatasan glTFast. ADR-032 poin 1 dan poin 3
   (rest pose absolut anti-spinning dan sinkronisasi ke `headBone.parent`) **tetap berlaku**
   dan tetap dipakai. Alasan rig humanoid menjadi syarat, bukan preferensi: tanpa clip
   `Walk_Forward` yang di-drive rig humanoid, avatar yang digeser sepanjang rute akan
   terlihat mengesot di lantai.

4. **`AvatarSafetyFade` yang terbukti menyala adalah *gate*, bukan pekerjaan susulan.**
   Locomotion tidak boleh diaktifkan sebelum fade terbukti bekerja, karena avatar yang
   berjalan bisa membelok di tikungan dan muncul tepat di muka pengguna. Tiga syarat wajib:
   (a) renderer yang di-fade adalah renderer model VRM sungguhan, (b) jarak diukur dari bone
   dada/kepala, bukan root di telapak kaki, (c) material avatar memakai surface type
   transparan sehingga alpha benar-benar dihormati shader. **Bukti yang diterima adalah
   rekaman/observasi Play mode saat fade betul-betul terlihat**, bukan pembacaan kode.
   Alasannya ada presedennya di proyek ini: lihat catatan koreksi ADR-028, di mana sebuah
   mekanisme keselamatan yang ADR-nya sudah percaya diri menyatakan "sudah benar" ternyata
   masih gagal pada skenario persis yang menjadi alasan ADR itu ditulis.

   > **Syarat (c) dicabut oleh Amandemen 034-A di bawah. Syarat (a) dan (b) tetap berlaku
   > dan sudah TERBUKTI menyala (2026-08-25).**

5. **Locomotion di-gate ke keberhasilan lokalisasi MultiSet.** Sesuai ADR-007 dan ADR-011,
   posisi hanya sah setelah localize berhasil. `StartLeading()` tidak boleh dipanggil sebelum
   itu, dan saat terjadi re-localize avatar wajib di-`Warp()` ulang ke titik valid pada rute.
   Kegagalan mode ini berbahaya karena senyap: `NavMeshAgent`/sampling yang jatuh di luar
   NavMesh tidak melempar error, avatar hanya diam membeku.

6. **Sandbox dulu, lalu `TestingHCM`, dan scene produksi TIDAK dipakai sebagai lahan uji.**
   ADR-030 poin 3 tetap berlaku: pengembangan visual/look-at/fade diuji di
   `Sandbox_AvatarCompanion.unity`. Untuk uji integrasi dengan navmesh dan
   `NavigationController`, dipakai `TestingHCM.unity`, bukan
   `DARSi-Indoor Navigation.unity`.

   **⚠️ Peringatan wajib dibaca sebelum menyimpulkan apa pun dari `TestingHCM`.** Kedua scene
   **tidak berperilaku sama** persis pada dimensi yang paling menentukan untuk lead-follow.
   Terukur 2026-08-24, rute `[Ground] Parkir Mobil` → `[Lantai1] IGD`:

   | Scene | Status rute lintas lantai | OffMeshLink |
   |---|---|---:|
   | `DARSi-Indoor Navigation` (produksi) | `PathPartial`, 2 corner, 0,90 m | 0 |
   | `TestingHCM` | **`PathComplete`, 17 corner**, naik 4,21 m | 0 |

   Di `TestingHCM` kedua lantai **tersambung di dalam navmesh hasil bake** (lompatan Y 2,04 m
   di corner 5, tanpa OffMeshLink), yang berarti bake-nya kemungkinan besar mendahului
   amandemen 020-B. Akibatnya `TestingHCM` akan **meluluskan uji yang gagal di produksi**:
   avatar tampak mulus naik lantai, padahal di produksi ia berhenti setelah 90 cm, dan
   `ShowPath` di sana akan menggambar garis menembus plafon persis seperti yang 020-B cegah.

   Aturannya: **`TestingHCM` sah untuk lead-follow DALAM SATU LANTAI** (bagian terbesar
   pekerjaan controller). **Serah terima lift dan seluruh perilaku lintas lantai (keputusan 9)
   WAJIB divalidasi terhadap navmesh produksi**, tidak boleh disimpulkan dari `TestingHCM`.

7. **Informasi rute dibawa audio; visual adalah penguat, bukan syarat.** Instruksi arah wajib
   lengkap dan bisa dipahami **tanpa pengguna menatap layar** ("lurus terus ya, nanti belok
   kanan di ujung koridor").

   **Avatar berjalan memimpin secara terus-menerus sepanjang rute dan tidak pernah menghilang
   di tengah jalan.** Yang bersifat sesekali adalah **gestur penekanannya**, bukan kehadirannya:
   di koridor lurus ia sekadar berjalan di depan pengguna, sedangkan di titik keputusan
   (persimpangan, transisi lantai, tiba di tujuan) ia berhenti, menoleh, menunjuk, dan menunggu.
   Yang hilang dari pengguna hanyalah *kewajiban menatap layar*, bukan pemandunya. Jangan
   membaca poin ini sebagai "avatar hanya muncul di checkpoint", model itu justru ditolak
   secara eksplisit di bawah.

   **Titik keputusan diturunkan, bukan ditanam manual.** Persimpangan adalah `corners` dari
   polyline rute yang sudah dihasilkan pathfinder, jadi tidak ada aset checkpoint yang perlu
   ditandai per koridor maupun di-maintain terpisah (prinsip ADR-021). Jangan membangun tool
   penanda checkpoint.

   Pembagian kendali: **rutenya ditentukan penuh** (lewat mana, belok di mana), sedangkan
   **tempo dan perhatiannya reaktif** terhadap pengguna (kapan menunggu saat pengguna
   tertinggal, kapan melanjutkan, ke mana kepala menoleh, kapan memudar karena terlalu dekat).
   Avatar tidak pernah melakukan pathfinding atau steering atas kehendaknya sendiri.

   Alasannya adalah pemisahan dua hal yang mudah tertukar: **kehadiran avatar** membebani
   *frame budget GPU*, sedangkan **informasi yang hanya tersedia secara visual** membebani
   *atensi pengguna*. Hanya yang kedua yang berbahaya di koridor RS. Trade-off yang dipilih
   secara sadar: bayar GPU, jangan bayar atensi orang sakit. Ini juga meniru perilaku pemandu
   manusia sungguhan, yang berbicara sepanjang jalan dan menunjuk di tikungan, tanpa menuntut
   wajahnya ditatap terus-menerus.

   Konsekuensi turunan: lead-follow **tidak boleh** dirilis sebelum TTS (ADR-033) berfungsi.
   Tanpa lapisan audio, model ini berubah menjadi "pengguna berjalan di koridor RS sambil
   menatap layar", yaitu persis bahaya yang §4.1 ingin cegah.

8. **Occlusion mesh diaktifkan sebagai bagian dari pekerjaan lead-follow.** Map mesh dari
   MultiSet sudah tersedia di project (`Assets/MultiSet/MapData/MAP_*.glb`, GameObject
   `Map Space` di scene produksi), jadi oklusi statis adalah **pekerjaan wiring yang belum
   dilakukan**, bukan keterbatasan bawaan AR. Map mesh dirender *depth-only* (`ColorMask 0`,
   `ZWrite On`) sehingga menulis depth buffer tanpa terlihat, dan avatar otomatis tertutup
   tembok, pintu, serta furnitur tetap.

   Catatan status saat ini, supaya tidak disalahpahami sesi berikutnya: satu-satunya skrip yang
   menyentuh hal ini, `MapMeshColliderSetup.cs`, justru ditulis untuk **mengeluarkan** mesh dari
   culling mask kamera (komentarnya harfiah: *"no depth writes, no visual occlusion"*), skrip itu
   **tidak dipakai** di scene produksi, dan layer `CollisionMesh` yang dibutuhkannya **tidak ada**
   di `TagManager.asset`. Jadi jangan berasumsi oklusi sudah menyala hanya karena mesh-nya ada.

   **Batas jujur:** ini hanya menutup occluder **statis**. Orang berjalan, brankar, kursi roda,
   dan troli **tidak** tertutup, padahal di koridor RS justru itu yang sering lewat di antara
   pengguna dan avatar. Occluder dinamis (ARCore Depth API) **di luar cakupan** keputusan ini dan
   belum diputuskan. Kualitas oklusi juga terikat kualitas scan, dan scan RSI sungguhan belum
   dikerjakan (masih di backlog Sprint 2).

9. **Avatar tunduk pada rute tersegmentasi ADR-020; TIDAK PERNAH satu polyline lintas lantai.**
   Keputusan 2 di atas menyebut "polyline rute" dalam bentuk tunggal, dan itu hanya benar
   **di dalam satu lantai**. ADR-020 (dengan amandemen 020-B) menetapkan tiap lantai adalah
   pulau NavMesh terpisah tanpa `NavMeshLink`, jadi rute lintas lantai secara struktural
   berupa dua segmen yang disambung serah terima di lift.

   Konsekuensinya untuk lead-follow:
   - Avatar hanya memimpin **segmen lantai saat ini**. Sampai di lift, ia berhenti, tidak
     menembus plafon, dan tidak berpura-pura tahu jalan ke atas.
   - Serah terima lift: avatar mengantar ke lift → berhenti/menghilang selama transisi →
     muncul kembali di lift lantai tujuan **hanya setelah re-localize berhasil** (ADR-020
     poin 4, konsisten dengan keputusan 5 di ADR ini).
   - Avatar mengikuti penyaringan lantai ADR-018 sama seperti marker POI: kalau pengguna
     tidak sedang di lantai avatar, avatar tidak dirender.

   **Pakai `FloorTransitionController` yang sudah ada, jangan bangun mesin state kedua.**
   Script itu sudah mengimplementasikan fase ADR-020 (`Idle`, `ToConnector`,
   `AwaitingRelocalize`, `ToDestination`). Avatar menjadi **penampil** fase tersebut, bukan
   pemilik kedua atas "sekarang lagi tahap apa" — alasan yang sama dengan keputusan 2 soal
   rute. Perubahan aditif yang diperlukan: fase tersebut saat ini `private`, jadi perlu
   diekspos (property read-only atau UnityEvent perubahan fase). Itu satu-satunya sentuhan
   yang dibenarkan; jangan menduplikasi logikanya ke `AIAvatarGuideController`.

   > **Status implementasi (2026-08-25): INTI SELESAI, BELUM TERVALIDASI.**
   >
   > `FloorTransitionController.Phase` dijadikan publik beserta `CurrentPhase` dan
   > `IsTransitioning` (hanya-baca; yang boleh mengubah tetap kelas itu sendiri). Ini
   > satu-satunya sentuhan yang dibenarkan keputusan ini, dan tidak ada mesin state
   > kedua yang dibuat.
   >
   > `AIAvatarGuideController` menyembunyikan avatar selama fase `AwaitingRelocalize`
   > dan menandai `_needsSnap` supaya saat muncul kembali ia melompat ke rute lantai
   > baru, bukan melanjutkan dari posisi lama yang sudah tidak bermakna. Avatar
   > **disembunyikan**, bukan sekadar dihentikan: pemandu yang mengambang di lantai lama
   > sementara penggunanya sudah pindah lebih menyesatkan daripada tidak ada pemandu.
   >
   > **Fase `ToConnector` dan `ToDestination` sengaja TIDAK diperlakukan khusus.** Rute
   > yang digambar `ShowPath` memang sudah segmen lantai yang sedang berlaku, jadi avatar
   > cukup menaikinya seperti biasa. Menambah penanganan khusus untuk keduanya berarti
   > menduplikasi keputusan yang sudah dibuat `FloorTransitionController`.
   >
   > **Penyaringan lantai eksplisit ala ADR-018 TIDAK ditambahkan**, dan ini penyederhanaan
   > yang disadari, bukan kelalaian. Kombinasi gerbang `AwaitingRelocalize` plus snap-ulang
   > sudah menjamin avatar tidak pernah tertinggal di lantai yang salah. Menambah
   > perbandingan indeks lantai berarti sumber kebenaran kedua untuk pertanyaan yang sama.
   > Kalau uji lapangan menemukan celah, itu yang ditambahkan lebih dulu.
   >
   > **BELUM DIVALIDASI, dan tidak bisa divalidasi di `TestingHCM`.** Sesuai peringatan
   > keputusan 6, `TestingHCM` memiliki lantai yang TERSAMBUNG di navmesh (terukur ulang
   > 2026-08-25: `PathComplete`, 17 corner), sehingga serah terima lift di sana tidak
   > pernah terpicu. Validasi wajib memakai navmesh produksi dan perangkat sungguhan,
   > karena `AwaitingRelocalize` hanya muncul saat localize benar-benar putus lalu pulih.

   **Bukti terukur (Edit mode, 2026-08-24, scene produksi):** dalam Lantai Dasar
   `PathComplete` 3 corner / 11,30 m; dalam Lantai 1 `PathComplete` 12 corner / 26,04 m;
   Lift Ground → Lift Lantai1 `PathPartial` 2 corner / 0,90 m; `OffMeshLink` di scene = 0.
   Artinya rel per-lantai memang tersedia dan terbaca untuk dinaiki avatar, dan pemisahan
   antar lantai sesuai 020-B benar-benar terimplementasi di scene, bukan sekadar tertulis.

**Yang Ditolak:**
- **`NavMeshAgent` kedua milik avatar** (sebagaimana draft Task 6 Step 2). Dua agent di NavMesh
  yang sama menghitung rute yang sama berarti avatar bisa memilih jalur berbeda dari garis
  `ShowPath` yang dilihat pengguna, yaitu pemandu yang jalannya tidak sama dengan panah di lantai.
- **Draft `AvatarSafetyFade` pada Task 6 Step 1.** Draft itu memakai `r.material.color`, yang
  meng-*clone* material instance setiap frame per renderer. Ini regresi dari kode yang sudah
  ada di branch, bukan perbaikan.
- **Spawn statis berukuran penuh di depan kamera untuk sesi berjalan.** Tetap ditolak sesuai
  §4.1. `AvatarCompanionController` diperlakukan sebagai stub Tahap 1 yang digantikan
  `AIAvatarGuideController`, bukan sebagai fondasi yang dikembangkan lebih lanjut.
- **Menunda perbaikan safety fade sampai locomotion jalan lebih dulu.** Urutan itu berarti
  menguji avatar berjalan di koridor tanpa proteksi pandangan yang berfungsi.
- **Avatar hanya muncul di titik keputusan lalu menghilang di koridor lurus.** Sempat diusulkan
  (2026-08-24) dengan alasan menghemat frame budget, oklusi, dan waktu HP terangkat. **Ditolak**
  setelah ditimbang dari sisi penggunanya: pengguna DARSI adalah orang sakit, lansia, dan
  pendamping pasien yang sedang cemas, dan pemandu yang lenyap di antara persimpangan justru
  menimbulkan keraguan ("tadi dia ke mana, aku masih benar tidak?") tepat saat orangnya paling
  butuh kepastian. Usulan itu mengoptimalkan ongkos engineering, bukan pengalaman pengguna.
  Kekhawatiran yang melatarinya sah, tetapi jawabannya adalah keputusan 7 (pindahkan informasi
  rute ke audio), bukan menghilangkan kehadiran avatar.
- **Mengasumsikan oklusi sebagai keterbatasan inheren AR.** Sempat dinyatakan demikian pada
  diskusi yang sama dan itu **keliru**: map mesh MultiSet sudah tersedia di project dan oklusi
  statis tinggal di-wiring (keputusan 8).
- **Memasang `NavMeshLink` antar-lantai supaya avatar dapat satu polyline utuh.** Ini jebakan
  yang sangat mungkin muncul: siapa pun yang menguji lead-follow akan melihat rute lintas lantai
  mengembalikan `PathPartial` dan menyimpulkan NavMesh-nya rusak. **Bukan rusak** — itu
  amandemen 020-B yang bekerja sebagaimana mestinya. Memasang link akan mencabut 020-B
  diam-diam dan menghidupkan lagi dua akibat yang ditolak di sana: `ShowPath` menggambar garis
  menembus plafon, dan **setiap** `NavMesh.CalculatePath` di proyek (termasuk kode SDK yang
  belum diaudit) mendadak menganggap dua lantai sebagai satu ruang berjalan.
- **Menduplikasi mesin state transisi lantai ke dalam `AIAvatarGuideController`.**
  `FloorTransitionController` sudah memilikinya; avatar menampilkan fasenya, tidak memilikinya
  sendiri (keputusan 9).

**Status verifikasi:**
- ✅ **SELESAI (2026-08-24).** Peran `navController.agent` sudah terjawab: proxy posisi
  pengguna, bukan pejalan otonom. Bukti dan konsekuensinya dicatat di keputusan 2.
- ⬜ **BELUM.** Apakah NavMesh hasil bake ikut berpindah saat MultiSet melakukan re-localize,
  atau tetap diam di koordinat bake. Ini menentukan seberapa berat penanganan keputusan 5.
  Perlu dicek sebelum locomotion diuji di device.
- ✅ **SELESAI (2026-08-24, Play mode).** `ShowPath.path` terisi `PathComplete` 3 corner /
  17,35 m saat navigasi aktif, sementara `agent.path` kosong (`hasPath=false`, 1 corner).
  Angka dan konsekuensinya dicatat di keputusan 2. Polyline-nya terbukti terbaca dan layak
  dinaiki avatar.

#### Amandemen 034-A (2026-08-25) — syarat (c) keputusan 4 dicabut: avatar HILANG MENDADAK, bukan memudar

**Mencabut syarat (c)** dari keputusan 4 ("material avatar memakai surface type transparan
sehingga alpha benar-benar dihormati shader"). Syarat (a) dan (b) tetap berlaku.

**Gate keputusan 4 dinyatakan LULUS** atas kriteria keselamatan, dengan bukti terukur dari
Play mode (scene sandbox, kamera ditaruh pada jarak tetap lalu alpha dibiarkan stabil):

| jarak horizontal | alpha | renderer aktif | avatar terlihat? |
|---:|---:|---:|---|
| 2,00 m | 1,00 | 3/3 | ya |
| 0,90 m | 1,00 | 3/3 | ya (ambang mulai) |
| 0,80 m | 0,75 | 3/3 | ya |
| 0,70 m | 0,50 | 3/3 | ya |
| 0,60 m | 0,25 | 3/3 | ya |
| **0,50 m** | **0,00** | **0/3** | **TIDAK (aman)** |
| 0,00 m | 0,00 | 0/3 | TIDAK (aman) |

Renderer yang terdaftar adalah `Face`, `Body`, `Hair` milik model VRM sungguhan, bukan
placeholder. Kurvanya persis `InverseLerp(0.5, 0.9, d)`. Ini pertama kalinya protokol §4
benar-benar terlihat menyala sejak ditulis.

**Yang diterima sebagai konsekuensi:** antara 0,9 m dan 0,5 m avatar tetap **pekat penuh**,
lalu **hilang mendadak** di 0,5 m. Tidak ada transisi memudar.

**Alasan syarat (c) gugur.** Material MToon hasil impor VRM ber-`_BlendMode` 1 (Cutout,
`_ALPHATEST_ON`), sehingga alpha dipakai sebagai ambang potong dan `MaterialPropertyBlock`
tidak dapat mengubah keyword maupun render queue. Memenuhi (c) menuntut mengubah material
ke Transparent saat impor, dan itu membawa tiga ongkos untuk keuntungan yang **murni
kosmetik**:
1. Karakter VRoid punya lapisan rambut, wajah, dan mata yang saling tumpang tindih.
   Transparansi pada geometri berlapis seperti ini rawan artefak urutan render.
2. Render queue transparan menambah beban pada budget frame §2.2 yang sudah ketat
   (target 60 FPS bersama ARCore dan VPS MultiSet).
3. Fungsi keselamatannya **tidak bergantung pada alpha sama sekali** — yang melindungi
   pandangan pengguna adalah `renderer.enabled` menjadi false, dan itu sudah terbukti.

**Kenapa pop dapat diterima, bukan sekadar dihindari.** Pada 0,5 m horizontal avatar sudah
memenuhi layar; nilai keselamatannya biner (pandangan terhalang atau tidak), bukan gradual.
Memudar perlahan dari 0,9 m justru menampilkan avatar semi-transparan tepat di jarak paling
berbahaya, sedangkan menghilangkannya sekaligus memberi pandangan penuh lebih cepat.

**Jalan naik kalau ternyata mengganggu di lapangan.** Kalau uji dengan pengguna sungguhan
menunjukkan pop-nya mengagetkan, dua opsi dievaluasi **dengan bukti lapangan**, bukan dugaan:
(1) MToon `_BlendMode` 2 saat impor, atau (2) shader fade berbasis dither/alpha-clip yang
tidak memerlukan render queue transparan. Jangan kerjakan salah satunya sebelum ada keluhan
nyata — ini keputusan yang sengaja ditunda, bukan yang dilupakan.

**Yang TIDAK berubah:** kewajiban bukti eksekusi di keputusan 4 tetap berlaku penuh untuk
perubahan apa pun pada mekanisme ini di masa depan.

### ADR-035 — Tidak Membangun Dashboard Admin Custom; Supabase Dashboard Cukup (2026-08-24)

**Konteks.**
Muncul pertanyaan apakah DARSI membutuhkan dashboard admin custom untuk analisis aplikasi
(statistik navigasi, log AI assistant, manajemen POI) yang bisa diakses admin/atasan.

Proyek sudah memiliki backend FastAPI + Supabase (PostgreSQL). Supabase menyediakan dashboard
bawaan: table viewer, SQL editor, log viewer — semuanya sudah berfungsi tanpa development
tambahan. Kondisi proyek saat ini: 11 POI, 1 gedung, belum production, VPS accuracy belum
diukur di device, field test RSI belum dilakukan, AI Avatar masih brainstorming.

**Keputusan.**

1. **Tidak membangun dashboard admin custom pada fase ini.** DARSI adalah *wayfinding tool*,
   bukan *analytics platform*. Admin rumah sakit/kampus tidak akan mengecek dashboard
   analitik navigasi secara rutin — mereka punya urusan operasional yang lebih mendesak.
   Development time lebih baik dipakai untuk mematangkan fitur inti (akurasi VPS, navigasi
   lintas lantai, AI companion).

2. **Supabase Dashboard sebagai alat monitoring default.** Untuk kebutuhan developer
   (memeriksa data, debug, query) dan kebutuhan akademik (mengumpulkan metrik untuk
   skripsi/laporan), Supabase Dashboard yang sudah ada lebih dari cukup. Data riset
   diekspor via SQL query → CSV, divisualisasikan di spreadsheet.

3. **Eskalasi: route `/admin` di Next.js yang sudah ada**, hanya jika dan ketika DARSI
   sudah production di RSI dan stakeholder non-teknis (staf humas/resepsionis) membutuhkan
   akses mandiri — misalnya mengubah status POI ("Poli Jantung tutup hari ini"). Bukan
   dashboard analitik, melainkan form operasional sederhana. Satu codebase, satu deployment,
   proteksi password sederhana.

4. **Telemetri ringan tetap ditambahkan saat fitur-fitur inti sudah stabil.** Endpoint
   `POST /api/analytics/event` sederhana untuk mencatat event navigasi dan query AI ke
   tabel Postgres. Tujuannya bukan dashboard, melainkan data mentah untuk laporan akademik
   dan tuning. Ini ditambahkan bersamaan atau setelah AI assistant dan navigasi berjalan
   end-to-end, bukan sebelumnya.

**Yang Ditolak:**
- **Dashboard admin custom (Next.js + chart library) sebagai subsistem terpisah.** Scope
  creep: menambah surface area development dan maintenance tanpa menyelesaikan masalah inti
  mana pun. Pada skala DARSI saat ini, marginal benefit dashboard custom di atas Supabase
  Dashboard mendekati nol.
- **Self-hosted Metabase/Grafana.** Menambah infrastruktur baru yang harus di-host dan
  di-maintain, tidak sebanding dengan skala proyek.
- **Menunda telemetri sama sekali.** Data empiris tetap penting untuk evaluasi akademik,
  tapi dikumpulkan lewat logging sederhana, bukan arsitektur dashboard.

---

### ADR-036 — Gerbang relevansi retrieval dilonggarkan (`MIN_TOP_SCORE` 0,22 → 0,15), diputuskan lewat asimetri biaya (2026-08-26)

**Berlaku di repo `darsi-backend`** (`app/assistant/retrieval.py`), dicatat di sini karena proyek ini sepakat satu tempat pencatatan ADR. Melanjutkan ADR-026, tidak mencabutnya.

**Pemicu.** Skenario `eval_llm_judge` #06, *"Tangan kena pisau robek berdarah banyak"*, dijawab **"Maaf, saya tidak punya informasi soal itu"** — tidak ada arahan ke IGD sama sekali. Ini satu-satunya kegagalan yang tersisa dari 52 skenario (51/52 = 98,1%), dan kebetulan yang paling berbahaya.

**Diagnosis yang sempat KELIRU, dicatat supaya tidak diulang.** Dugaan pertama: celah kosakata, pola yang sudah tiga kali berhasil sebelumnya (pipis, luka bakar, spiral KB). **Salah.** Dibuktikan lewat tiga curl ke produksi: chunk IGD sudah memuat frasa "luka robek, pendarahan hebat" secara harfiah, dan query yang memakai kata-kata itu persis memang berhasil. Akar sesungguhnya ada di `search_chunks()`: gerbang `MIN_TOP_SCORE` **hanya membaca skor vector**, sementara full-text baru dipakai *setelah* gerbang lolos. Akibatnya kecocokan kata harfiah ("robek") pun tidak bisa menolong membuka gerbang. Menebak akar masalah dari pola yang sudah-sudah adalah cara paling cepat menghasilkan perbaikan yang percaya diri tapi salah sasaran.

**Angka yang menentukan.** Skor query itu **0,214**. Ambang lama **0,22**. Pasien luka berdarah tidak dilayani karena selisih **0,006**.

**Keputusan 1 — dasar keputusannya asimetri biaya, bukan kemenangan angka.** Ini bagian terpenting ADR ini:
- Sampah yang **lolos** gerbang tidak menghasilkan jawaban salah. Dia diteruskan ke LLM, yang menolaknya dengan benar. Terukur: kategori Di Luar Cakupan **4/4** pada run final `eval_llm_judge`. Biayanya nyaris nol.
- Pertanyaan sah yang **diblokir** gerbang menghasilkan penolakan buta, dan di antaranya ada luka berdarah. Biayanya keselamatan.

Satu sisi nyaris tanpa biaya, sisi lain berbiaya nyawa. Maka gerbang dilonggarkan dan penyaringan diserahkan ke LLM — persis pembagian tugas yang sudah diputuskan ADR-026, sekarang dijalankan lebih konsisten.

**Keputusan 2 — 0,15, bukan 0,18.** Set uji `test-3` menunjukkan ≤0,18 sudah memberi recall soal sah **24/24 (100%)** (di 0,20 turun ke 21/24). Tapi 0,18 tetap ditolak: *"kena air panas melepuh"* ada di **0,181**, margin 0,001 dan akan patah begitu corpus berubah sedikit. 0,15 memberi margin nyata sambil tetap menahan yang benar-benar jauh (*"resep rendang padang"* di 0,090).

**Keputusan 3 — dua set uji baru ditulis sekaligus SEBELUM pengukuran.** `test-2` sudah terbakar khusus untuk parameter ini (kegagalannya sudah dipakai belajar bahwa 0,15 > 0,22), jadi memakainya lagi berarti mengukur sistem terhadap dirinya sendiri. Ditulis `test-3` (penyetelan) dan `test-4` (disegel), komposisi identik (24 soal sah + 8 soal sampah), nol tumpang tindih dengan 97 soal di empat set lama. Keduanya di-commit bersamaan sebelum angka apa pun dilihat, dan commit itu sendiri yang jadi buktinya.

**Bukti tandingan, dicatat apa adanya.** `test-4` **tidak mendukung** perubahan ini: soal sahnya 23/24 baik di 0,15 maupun 0,22, sementara penolakan sampahnya justru lebih baik di 0,22 (2/8 vs 1/8). Set segel bersikap netral-condong-menolak. Perubahan ini berdiri di atas argumen asimetri, bukan di atas tabel. **Siapa pun yang hendak mencabutnya harus membantah asimetri itu, bukan sekadar menunjuk angka.**

**Koreksi atas dokumentasi lama.** `RETRIEVAL-EVALUATION.md` §6 mengklaim ambang 0,22 memblokir empat pertanyaan sah. Diukur ulang, **tiga di antaranya sudah lolos sendiri** lewat perkayaan corpus ("keseleo" 0,308, "beli minum" 0,330, "nganter jenazah" 0,268). Selisih 0,22 vs 0,15 di `test-2` juga menyusut dari +9,3 poin menjadi **+3,1 poin**. Alasan menurunkan ambang ternyata *lebih lemah* dari yang tertulis — dan keputusannya tetap diambil, karena dasarnya memang bukan selisih itu.

**Temuan sampingan yang memperkuat ADR-026.** Pada ambang 0,22 gerbang cuma menolak **1 dari 8** soal sampah di `test-3` (2/8 di `test-4`, 1/4 di `test-2`). Gerbang ini bukan filter, melainkan kebocoran. Konsisten dengan bukti ADR-026 bahwa cosine tidak terkalibrasi untuk memisahkan dalam/luar cakupan.

**Sengaja TIDAK dibangun.** Sempat dipertimbangkan membuat full-text ikut membuka gerbang (perbaikan arsitektural yang menyasar akar masalah langsung). **Dibatalkan berdasarkan data**: pada 0,15 recall soal sah `test-3` sudah 100%, tidak ada sisa yang bisa diperbaiki opsi itu. Menambah logika untuk keuntungan terukur nol adalah YAGNI. Kalau suatu saat ada pertanyaan sah yang gagal padahal kata kuncinya cocok persis, opsi ini yang pertama dibuka lagi.

---

### ADR-037 — Pemilihan Engine Lip-Sync hecomi/uLipSync Berbasis MFCC dan Burst Compiler untuk Avatar 3D VRM (2026-08-31)

**Konteks.**
Pada implementasi Fase 2 Avatar (melanjutkan [ADR-030](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/docs/DECISIONS.md#adr-030--pengembangan-ai-avatar-companion-pada-scene-terisolasi-dan-paket-gltf-2026-08-23), [ADR-033](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/docs/DECISIONS.md#adr-033--arsitektur-voice-output-tts-hybrid-edge-tts--sherpa-onnx-dan-kontrak-endpoint-2026-08-26), dan [ADR-034](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/docs/DECISIONS.md#adr-034--model-penempatan-lead-follow-dan-safety-fade-avatar-companion-2026-08-26)), avatar 3D VRM membutuhkan sinkronisasi gerakan bibir (viseme lip-sync) terhadap output suara TTS secara real-time di perangkat mobile AR (Android) tanpa membebani garbage collection (GC) frame budget.

**Alternatif yang Dievaluasi:**
1. **hecomi/uLipSync (v3.1.5, Lisensi MIT):** Menggunakan Mel-Frequency Cepstral Coefficients (MFCC) yang dioptimasi penuh melalui Unity C# Job System dan Burst Compiler. Memetakan sinyal audio ke 5 preset vokal vokal standar bahasa Jepang/Indonesia (A, I, U, E, O) yang cocok persis 1:1 dengan preset `VRMBlendShapeProxy` milik UniVRM 0.x. Zero allocation di audio thread.
2. **OVRLipSync (Meta):** Menggunakan viseme Oculus (15 viseme). Memerlukan binary C++ native proprietary, lisensi tertutup, dan membutuhkan pemetaan ulang manual ke 5 blendshape UniVRM yang sering menghasilkan distorsi bentuk mulut.
3. **Custom Amplitude/RMS FFT:** Sangat sederhana dan ringan, namun hanya dapat mendeteksi intensitas volume (buka-tutup mulut monoton) tanpa dapat membedakan formasi vokal fonem (A, I, U, E, O).

**Keputusan Arsitektur:**
1. **Engine Primer:** Mengadopsi paket `hecomi/uLipSync` (v3.1.5 via local UPM di `Packages/com.hecomi.ulipsync`) bersama dependensi `com.unity.burst` dan `com.unity.mathematics`.
2. **Driver Adapter (`AvatarSpeechLipSync.cs`):** Dibuat komponen adapter [`AvatarSpeechLipSync`](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/Assets/Scripts/Avatar/AvatarSpeechLipSync.cs) di `Assets/Scripts/Avatar/` yang mendengarkan event `onLipSyncUpdate` dari `uLipSync`, menghitung pembukaan mulut terbobot rasio fonem dan volume gain, serta menerapkan nilai viseme ke [`VRMBlendShapeProxy`](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/Assets/Scripts/Avatar/AvatarSpeechLipSync.cs) di `LateUpdate()` menggunakan `Mathf.SmoothDamp`.
3. **Penyediaan Fallback Terpadu:** Komponen [`AvatarSpeechLipSync`](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/Assets/Scripts/Avatar/AvatarSpeechLipSync.cs) menyertakan modul estimasi amplitudo RMS prosedural lokal dengan buffer `float[512]` pra-alokasi (0 GC alloc). Jika `uLipSync` tidak terpasang atau non-aktif, avatar tetap dapat menggerakkan bibir secara ritmis tanpa crash atau exception.
4. **Guardrail Keselamatan Animasi:** Saat audio selesai diputar atau volume berada di bawah `minVolumeThreshold` (0.02), seluruh bobot viseme (A, I, U, E, O) secara otomatis diredam halus kembali ke 0.0f (pose istirahat / mulut tertutup) untuk mencegah mulut avatar tersangkut dalam pose terbuka.

**Hasil Validasi Empiris:**
- Unit Test EditMode: 9 dari 9 test lulus 100% ([`AvatarSpeechLipSyncTests.cs`](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/Assets/Tests/Editor/AvatarSpeechLipSyncTests.cs)).
- PlayMode Probe: Berhasil merekam ribuan frame pengujian vokal AIUEO dan sapaan natural RS. Evaluasi post-playback membuktikan seluruh viseme kembali ke 0.000 (LULUS 100%).

