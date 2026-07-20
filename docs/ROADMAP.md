# ROADMAP.md — DARSI Indoor Navigation (whole-project)

> Task breakdown lintas semua layer DARSI (Unity · WebView · Backend · Flutter bridge).
> Format tiap task: **Status · Repo · Depends** lalu **Done when**.
> Status: `TODO` / `WIP` / `BLOCKED` / `DONE`. Perubahan arsitektur → catat ADR baru di `DECISIONS.md` dulu.
>
> Urutan wajib: **Fase 0 → Fase 1**. Fase 2/3/4 bisa jalan lebih paralel setelah Fase 0 clear.
> `⚠️ NEEDS DECISION` = nunggu jawaban eksternal (Pak Farris / IT RSI), bukan bisa diputus sendiri.

---

## STATUS TERKINI (2026-07-20) — baca ini dulu

**Di mana kita:** T5.3 (navigasi lintas-lantai) selesai & terverifikasi sejauh yang bisa di Editor.
Prinsip yang menata semuanya: **hampir seluruh logika navigasi berstatus "terbukti di Editor,
belum terbukti di perangkat".** Sampai tes lapangan RSI, jangan bangun apa pun baru di atasnya.

**Rencana pekan-pekan depan (urut dependensi, bukan kemenarikan):**

1. **🔴 Railway trial hampir habis — cek/perpanjang DULUAN.** Eksternal, tak bisa diburu mendadak.
   Kalau backend mati saat di RSI, tes batal & kunjungan (butuh izin) terbuang.
2. **Beresin yang menggantung:** commit scene + `NavPathMaterial.mat`; commit+push+deploy
   `app/page.tsx` (WebView "Layanan Utama"); perbaiki `BuildingName` (masih tebakan, sudah
   nyasar ke 11 baris DB) + kategori Resepsionis; build APK, tes buka-tutup.
3. **Tes lapangan RSI** — [FIELD-TEST-T5.3.md](FIELD-TEST-T5.3.md). Angka penentu: detik dari
   keluar lift → navigasi menyambung (menyetel `relocalizeTimeout`, membuktikan asumsi DLL).
4. **Setelah tes:** setel angka dari data nyata; perbaiki temuan lapangan.
5. **Bayar utang tes otomatis** — logika clustering & keputusan lintas-lantai bisa diuji tanpa AR.
   Sengaja SETELAH lapangan (hasil bisa mengubah logikanya dulu).
6. **Dokumen keputusan untuk dosen** — UaaL+Feature Delivery vs WebXR vs embedding (lihat ADR-002 koreksi).
7. **Spike `flutter_embed_unity`** — jawab: MultiSet bisa localize di dalam Unity yang di-embed?
   Paling akhir: mengganti fondasi integrasi, jangan saat masih membuktikan navigasi.
8. **Play Feature Delivery** (MyRSIy) — jawaban sebenarnya untuk kekhawatiran ukuran (bukan pindah web).

**Utang teknis yang sudah diakui (jangan lupa):** belum ada satu pun tes otomatis; `DestinationFloorLabel`
menempel ke internal SDK (`ListItemUI.distance`) — mati diam kalau SDK rename; asumsi `LocalizeFrame()`
me-restart jendela 60 dtk belum terbukti. Lihat juga Backlog di bawah (PERF-1, dll).

---

## Fase 0 — Foundation / Kunci Kontrak  *(BLOCKER semua fase lain)*

Nggak boleh mulai coding fitur sebelum fase ini clear — kontrak yang masih bertabrakan bikin Unity & WebView di-code dari spec yang beda.

### T0.1 — Hapus blok pairing lama yang kontradiktif di INTEGRATION.md
- `TODO` · Docs(Unity+WebView) · Depends: —
- **Done when:** section "Message: Cari Teman (pairing-code)" lama (Unity A generate / Unity B input) dihapus/ditulis ulang; `INTEGRATION.md` konsisten bahwa Unity TIDAK panggil `/api/pairing/*` langsung dan TIDAK punya UI kode.

### T0.2 — Kunci payload `arSessionClosed`
- `TODO` · Docs(Unity+WebView) · Depends: —
- **Done when:** `INTEGRATION.md` & `API_CONTRACT.md` sepakat field `{ arrived, poiId }` dan tertulis siapa yang mengisi (Flutter merge dari `navigationArrived`, atau Unity kirim payload non-empty). Tidak ada lagi `{}` vs `{arrived,poiId}` mismatch.

### T0.3 — Samakan bridge API ke `webview_flutter`
- `TODO` · Docs(WebView) · Depends: —
- **Done when:** contoh kode di `API_CONTRACT.md` pakai `JavaScriptChannel` (`webview_flutter`), bukan `window.flutter_inappwebview.callHandler`. Konsisten dgn ADR-012.

### T0.4 — Lengkapi contoh `launchAR` di API_CONTRACT.md
- `TODO` · Docs(WebView) · Depends: T0.7
- **Done when:** contoh JS menyertakan `mode: 'findFriend'` + field koneksi (`connectionId`), cocok konsep byte-for-byte dgn `INTEGRATION.md`.

### T0.5 — Putuskan nasib `app/peta-lantai/` (WebView)
- `TODO` · WebView · Depends: —
- **Done when:** folder `app/peta-lantai/` dihapus (patuh ADR-006), ATAU ADR baru ditulis untuk mencabut ADR-006. Tidak boleh ada screen Peta 2D nyangkut tanpa keputusan.

### T0.6 — Daftarkan layer Backend di ARCHITECTURE.md
- `TODO` · Docs(all) · Depends: —
- **Done when:** tabel arsitektur mencantumkan Backend (Supabase + FastAPI) sebagai komponen ke-4 + peran + kepemilikan (Bagus). "3 repo" → "4 komponen".

### T0.7 — Tulis ADR-013 (friendlist persisten berbasis friend-request)
- `TODO` · Docs(all) · Depends: —
- **Done when:** ADR-013 ditulis di `DECISIONS.md` + `FLOWS.md §5` di-update. Isi: friendlist persisten via **friend-request add-by-exact-identifier (bukan direktori terbuka) + mutual accept**; presence **status-only** (online/AR-active/offline, tanpa lokasi); posisi live tetap **AR-only**; user bisa **opt-out (tampil offline)**; rate-limit + block. ADR-013 me-refine ADR-011 (bukan mencabut — posisi tetap AR-only).

### T0.8 — identitas user stabil dari MyRSIy  *(diturunkan dari blocker keras → wiring terakhir, 2026-07-06)*
- `WIP` (seam DONE, final wiring pending) · Backend/Flutter · Depends: T0.7
- **Update 2026-07-06 (ADR-017):** dibangun **seam identitas** supaya T0.8 TIDAK lagi nge-block pembangunan Fase 2. WebView `app/lib/user.ts` (`getCurrentUser()`) + kontrak `window.__DARSI_USER__` (host→WebView, lihat `INTEGRATION.md`/`API_CONTRACT.md`) + guest-gate Cari Teman (login-only). Di copycat kita pegang launch sendiri → `userId` bisa disuntik nilai dev sekarang, jadi seluruh Fase 2 bisa dibangun & didemoin tanpa MyRSIy. `handle` di-mint DARSI sendiri (tak perlu MyRSIy expose PII).
- **Done when (yang tersisa):** dev MyRSIy konfirmasi bisa oper **satu field `userId`** (UUID/PK, TIDAK didaur ulang) saat launch modul DARSI. Cuma itu. Bisa dijawab async, tak harus Pak Farris.

**Pertanyaan yang sudah menyempit (kirim ke dev MyRSIy — bukan questionnaire):**
> "Saat MyRSIy nge-launch modul DARSI (UaaL/WebView), bisa nggak oper **1 field: `userId`** user yang lagi login — berupa UUID atau primary key yang **tidak pernah didaur ulang**? Cuma itu yang DARSI butuh; nama/handle DARSI bikin sendiri."
- Handle & lifecycle hapus-akun sudah kejawab dari sisi desain DARSI (ADR-017), tidak perlu ditanyakan lagi.
- Poin lama #4/#5 (endpoint API MyRSIy, data POI dari RS) tetap relevan jangka panjang tapi tidak nge-block Fase 2.

**Catatan penting (2026-07-02):** Jawaban Pak Farris "Postgresql + Mysql" itu menjawab *"database apa yang dipakai **My eRSIy**"* — itu info stack internal MyRSIy, BUKAN arahan agar DARSI pakai/akses DB itu. Tidak ada arahan mengubah backend DARSI. Keputusan tetap: backend DARSI = Supabase sendiri, integrasi ke MyRSIy lewat bridge (bukan shared DB). Lihat ADR-001/ADR-012. Best-practice: Supabase = managed Postgres, jadi keputusan reversible (bisa migrasi ke Postgres self-hosted kalau produksi RS mewajibkan) — aman diambil sekarang.

**Draft pertanyaan lanjutan buat Pak Farris (kirim nanti — arah: dapat identitas user tanpa akses DB):**
1. Apakah MyRSIy bisa **mengoper ID user + nama tampilan** yang sedang login ke modul DARSI saat di-launch (lewat bridge/session)? Ini yang paling menentukan — kalau "bisa", friendlist (Fase 2) langsung ter-unblock tanpa DARSI menyentuh DB MyRSIy.
2. Format ID user itu apa (mis. UUID, integer, string)? Apakah stabil/permanen per user?
3. Apakah tersedia nama tampilan/handle yang aman ditunjukkan ke user lain (bukan data sensitif)?
4. Apakah MyRSIy punya endpoint/API yang bisa DARSI panggil, atau semua data harus lewat bridge saat launch?
5. (Opsional, jangka panjang) Kalau nanti produksi butuh data POI/lokasi dari sisi RS, apakah ada API-nya, atau DARSI kelola sendiri?

---

## Fase 1 — UaaL Entry Point (Unity)  *(prioritas #1)*

Satu-satunya pintu masuk data dari Flutter. Semua fitur AR hilir dari sini.

### T1.1 — `UaaLEntryPoint.cs` skeleton + GameObject di scene
- `DONE` · Unity · Depends: Fase 0
- **Done when:** GameObject `UaaLEntryPoint` (persistent) ada di scene WholePSDKU, script attached, `ReceiveLaunchPayload(string json)` bisa dipanggil & nge-log payload.
- Implemented: `Assets/Scripts/UaaLEntryPoint.cs`, GameObject wired in `WholePSDKU.unity`, `DontDestroyOnLoad` singleton.

### T1.2 — Struct payload + parse JSON
- `DONE` · Unity · Depends: T1.1
- **Done when:** JSON valid (`action, mode, poiId, poiName, floor, building, connectionId?`) → struct; JSON invalid → log error, tidak crash.
- Verified live in Play Mode: malformed JSON logs error and does not throw.

### T1.3 — Buffer payload sampai `localizationSuccess`
- `DONE` · Unity · Depends: T1.2, T0.2
- **Done when:** payload yang masuk sebelum localize disimpan, lalu diterapkan otomatis begitu localize sukses (tujuan hanya valid pasca-localize — ADR-007).
- Wired `MapLocalizationManager`'s `LocalizationSuccess` UnityEvent → `UaaLEntryPoint.OnLocalizationSuccess` (4th persistent listener, alongside existing `PhotonManager.OnLocalizationSuccess`). Verified: payload sent before localize does not route until `OnLocalizationSuccess` fires.

### T1.4 — Route `mode: navigate` → NavigationAdapter
- `DONE` · Unity · Depends: T1.3
- **Done when:** `poiId` valid → `NavigationAdapter` mulai navigasi; `poiId` invalid → toast (ID), tidak crash.
- Verified live: exact `poiId` match → `POIManager.FindBestMatchWithContext` → `NavigationAdapter.NavigateToPOI` fires real navigation. Invalid `poiId` → `ToastManager.Instance.ShowAlert(...)` + warning log, no crash.
- **Gap ditutup (2026-07-07):** stable-ID kini end-to-end. Backend expose `unity_id` sebagai `id` di response POI → WebView kirim balik sebagai `launchAR.poiId` → `UaaLEntryPoint.ResolvePoi` resolve by GUID dulu (`POIData.poiId`), fallback fuzzy-match `poiName` untuk POI legacy tanpa GUID. Rename nama tampilan tak lagi mematahkan navigasi. `arSessionClosed` juga bawa `poiName` untuk banner WebView. (Sebelumnya `poiId` = nama, rapuh terhadap rename.)

### T1.5 — Route `mode: freeExplore` → UI pilih tujuan di AR
- `DONE` · Unity · Depends: T1.3
- **Done when:** payload tanpa `poiId` → panel pilih tujuan di dalam AR muncul.
- **Correction:** panel pilih tujuan TERNYATA sudah ada bawaan MultiSet SDK sample (`NavigationUIController.DestinationSelectUI`, toggle via `ToggleDestinationSelectUI()`, list POI searchable lewat `SelectList`) — bukan perlu dibangun baru seperti dugaan awal. `RouteFreeExplore()` di `UaaLEntryPoint.cs` sekarang memanggil `navigationUIController.ToggleDestinationSelectUI()`. Diverifikasi live di Play Mode: panel `False → True` setelah payload `mode:freeExplore` dikirim pasca-localize.

### T1.6 — Emit event Unity → Flutter (`UnitySendMessage`)
- `DONE` (with one flagged gap) · Unity · Depends: T1.1, T0.2
- **Done when:** `arSessionReady`, `localizationSuccess` (reuse `PhotonManager.NotifyLocalizationSucceeded`), `navigationArrived`, `arSessionClosed` terkirim di titik yang benar.
- All 4 events implemented and firing at correct points (verified `arSessionReady` + `localizationSuccess` live). `navigationArrived`/`arSessionClosed` exposed as public methods (`NotifyNavigationArrived`, `CloseArSession`) ready to be called once arrival-detection and a back-button hook exist elsewhere.
- **⚠️ NEEDS DECISION (flag to Bagus):** the actual outbound Android bridge call uses a placeholder native method name (`activity.Call("onUnityMessage", ...)` via `AndroidJavaObject` on `UnityPlayer.currentActivity`). This must match whatever host Activity method `My-eRSIy-CopyCat` implements to receive Unity callbacks — confirm when building ROADMAP T4.5 (Flutter bridge), update both sides together.

### T1.7 — Debug harness (tes tanpa Flutter)
- `DONE` · Unity · Depends: T1.2
- **Done when:** ada tombol/inspector yang manggil `ReceiveLaunchPayload` dgn payload contoh — bisa tes `navigate` & `freeExplore` di Editor tanpa Flutter.
- 4 `[ContextMenu]` entries on the `UaaLEntryPoint` component (right-click header in Inspector): simulate navigate (valid/invalid poiId), freeExplore, localizationSuccess.

---

## Fase 2 — Friendlist + Cari Teman  *(bisa dibangun di atas identity seam — ADR-017; final wiring MyRSIy = T0.8)*

Model final (ADR-013): friend-request persisten. Data-entry & manajemen teman di **WebView**; posisi live cuma di **AR**.

**Update 2026-07-06 (ADR-017):** tidak lagi "BLOCKED keras". Identity seam (`app/lib/user.ts` + `window.__DARSI_USER__`) sudah ada → T2.1–T2.7 bisa dibangun & dites di atas `userId` suntikan (dev/copycat). `Depends: T0.8` di bawah sekarang berarti "butuh final wiring MyRSIy untuk PRODUKSI", bukan "tak bisa mulai". UI Cari Teman (tab, add-by-handle, request, request-to-meet, guest-gate) sudah jadi di atas mock (`lib/friends.ts`).

### T2.1 — Skema Supabase (identity + friend graph)
- `TODO` · Backend · Depends: T0.8
- **Done when:** tabel `profiles` (map MyRSIy user ID → handle), `connections` (requester, addressee, status `pending|accepted`, timestamps) + RLS dibuat.

### T2.2 — Endpoint friend-request (FastAPI)
- `TODO` · Backend · Depends: T2.1
- **Done when:** `POST /api/friends/request`, `POST /api/friends/respond` (accept/reject), `GET /api/friends`, `DELETE /api/friends/{id}` jalan; rate-limit + block; **tidak ada** endpoint search direktori terbuka.

### T2.3 — Presence (status-only, tanpa lokasi)
- `TODO` · Backend · Depends: T2.1
- **Done when:** `GET /api/friends` mengembalikan `online | ar-active | offline` per teman; menghormati opt-out; TIDAK pernah kirim gedung/lantai/posisi.

### T2.4 — WebView: UI friendlist + add-by-identifier + pending
- `TODO` · WebView · Depends: T2.2, T2.3
- **Done when:** list teman + form add-by-identifier + daftar pending request, sesuai `DESIGN_SYSTEM`; hanya presence, tanpa lokasi/jarak.

### T2.5 — WebView: trigger `launchAR mode:findFriend`
- `TODO` · WebView · Depends: T2.4, T0.4
- **Done when:** tap "Navigasi ke teman" (yang `ar-active`) mengirim payload `findFriend` + `connectionId` yang valid.

### T2.6 — Unity: render posisi teman + navigasi
- `TODO` · Unity · Depends: T1.6, T2.5
- **Done when:** `mode:findFriend` → posisi teman + jarak muncul pasca-localize (reuse `PhotonManager`/`PlayerSync`), tombol navigasi jalan; murni visual, tanpa modal/keyboard di AR.

### T2.7 — Unity: auto-terminate share posisi
- `TODO` · Unity · Depends: T2.6
- **Done when:** salah satu pihak tutup sesi AR → posisi teman hilang, tidak ada cache posisi (ADR-011).

---

## Fase 3 — WebView UI (Home + Cari Lokasi)

### T3.1 — Setup Next.js dasar
- `DONE` · WebView · Depends: —
- **Done when:** project jalan sesuai `AGENTS.md`. Next.js 16 + React 19 + Tailwind v4, `npx next build` hijau (routes `/` + `/cari-lokasi`).

### T3.2 — Home screen
- `DONE` (UI) · WebView · Depends: T3.1
- **Done when:** sesuai `DESIGN_SYSTEM` + `FLOWS §2` — no header, no jarak, no kata "POI", FAB kamera + quick action.
- Sudah: search bar, quick action (Cari Lokasi), Destinasi Populer, Layanan Lainnya, FAB kamera. Data masih mock (real data nunggu T3.4). Quick-action grid dirapikan jadi 1 kolom (kartu "Lihat Peta" sudah dihapus per ADR-006; slot ke-2 nanti diisi "Cari Teman" saat Fase 2 unblocked).

### T3.3 — Cari Lokasi screen
- `DONE` (UI) · WebView · Depends: T3.1
- **Done when:** search + filter chip + confirmation card + CTA "Mulai Navigasi AR", tanpa meter.
- Sudah: search + clear, filter chip kategori, hasil (nama+gedung+lantai+badge, tanpa meter), empty state, recent searches, confirmation card, CTA. Data masih mock.

### T3.a — Bridge helper tunggal (sesuai kontrak terkunci)  *(pass 2026-07-02)*
- `DONE` · WebView · Depends: T0.3, T0.4
- **Done when:** `app/lib/bridge.ts` — satu `launchAR()` encode kontrak `API_CONTRACT.md` (channel `DarsiBridge`, payload `{action, mode, poiId, poiName, floor, building, connectionId}`), stub `console.log` kalau tanpa Flutter host. Dua helper inline yang lama (bentuknya beda + pakai nama channel salah `DarsiChannel`) dihapus. Field-set diverifikasi identik dengan `API_CONTRACT.md`.

### T3.b — Wire mode `launchAR` benar di kedua screen  *(pass 2026-07-02)*
- `DONE` · WebView · Depends: T3.a
- **Done when:** FAB Home → `freeExplore`; tap kartu Populer/Layanan + CTA Cari Lokasi → `navigate` + `poiId` (=poiName untuk sekarang, sesuai gap T1.4). Sebelumnya payload nggak ada field `mode` → Unity jatuh ke "Unknown mode"; sekarang cocok dengan `UaaLEntryPoint`.

### T3.4 — Integrasi API POI  *(breakdown 2026-07-02)*
Stack: **FastAPI + Supabase (Postgres)**. Prinsip **portability** (biar migrasi ke Postgres RS-hosted gampang, lihat ADR-001 catatan): WebView → FastAPI → Postgres SQL standar; jangan cantol dalam ke fitur proprietary Supabase (Auth/Realtime/PostgREST langsung). Data seed: **11 POI kampus** dari scene Unity (biar navigate bisa dites end-to-end). Field ownership per **ADR-014**: gedung/lantai milik POIData (Unity), status milik backend.

**Fasing:** untuk sekarang backend di-**seed manual** (SQL insert 11 POI + gedung/lantai/status/kategori diketik langsung). Model "Unity = sumber kebenaran" (ADR-014) baru aktif penuh pas sync tool dibangun (T3.4-L2, task terpisah nanti) — jangan blok WebView nunggu itu.

**⚠️ Butuh keputusan/eksternal sebelum backend jalan end-to-end:** (a) akun Supabase (provisioning project), (b) lokasi + nama repo backend baru (belum ada). Kode (schema, seed, FastAPI, WebView client) bisa ditulis duluan tanpa nunggu ini.

- **T3.4.0 — ADR-014** (`DECISIONS.md` dua repo): gedung/lantai di POIData, status di backend + alasan (data statis vs operasional). Bisa dikerjakan sekarang. `TODO`
- **T3.4.1 — Skema `pois`** (SQL standar): `id`, `name` (unik, = poiId untuk sekarang), `category`, `building`, `floor`, `status`, `is_popular`, `synonyms`, timestamps. `TODO` · Backend
- **T3.4.2 — Seed 11 POI kampus** (SQL insert): nama persis dari scene Unity (Perpustakaan, BAAK, Lab Teori 201/202/203, Lab Mikrotik, Mushola, Lab 102/103, Ruang Dosen, MMB Studio) + gedung/lantai/status/kategori manual. `TODO` · Backend
- **T3.4.3 — FastAPI read endpoints:** `GET /api/poi/popular`, `GET /api/poi/search?q=&category=`, `GET /api/poi/categories`. Read-only, **tanpa field jarak** (ADR-007). `TODO` · Backend
- **T3.4.4 — WebView API client + ganti mock Home** (popular + layanan) jadi fetch; loading/error state. `TODO` · WebView · Depends: T3.4.3
- **T3.4.5 — Ganti mock Cari Lokasi** (search + kategori) jadi fetch; loading/error/empty state; `poiId` yang dikirim ke `launchAR` pakai nama kanonik dari backend. `TODO` · WebView · Depends: T3.4.3
- **Done when:** ketiga endpoint dikonsumsi WebView, data 11 POI kampus tampil dari backend (bukan mock), response tanpa jarak, dan navigate end-to-end resolve di Unity.

### T3.4-L (later) — Unity jadi sumber kebenaran otomatis  *(task terpisah, "kedepannya")*
- **T3.4-L1 — Tambah `poiId` (GUID stabil, auto-generate via `Reset()`), `building`/`floor` ke `POIData.cs`** (aditif, file protected; per ADR-014). `DONE` (2026-07-05) · Unity
- **T3.4-L2 — Unity Editor "Sync POIs → Backend"** (`DARSI > Sync POIs to Backend`, `Assets/Editor/POISyncWindow.cs`) + endpoint tulis FastAPI (`POST /api/poi/sync`, header `X-Admin-Token`, upsert keyed by `unity_id` dengan fallback match by `name` untuk adopsi baris lama). Backend: kolom `unity_id text UNIQUE` ditambahkan ke `pois`. `DONE` (2026-07-05, **terverifikasi live**) · Unity+Backend · Depends: T3.4-L1, T3.4.3
  - **Verifikasi live (2026-07-05):** sync sukses dari Unity Editor ke Railway — `Synced: 11, created: 0, updated: 11` (11 POI seed lama ke-adopsi by `name`, `unity_id` ter-backfill). Migrasi kolom `unity_id` dijalankan manual sekali di Railway Query console.
  - Dua gotcha yang ketemu pas verifikasi (dicatat biar gak keulang): (1) admin token env var (`POI_SYNC_TOKEN`) sempat ke-set di service **Postgres** dengan nama salah ketik (`POI_SYCN_TOKEN`) — harus di service **backend**, key persis `POI_SYNC_TOKEN`. (2) Pool koneksi Postgres yang idle lama sempat diputus Railway (`SSL error: unexpected eof`) → fixed dengan `check=ConnectionPool.check_connection` di `app/main.py` lifespan, supaya koneksi mati di-deteksi & diganti sebelum dipakai, bukan gagal di tengah query.
  - `status` sengaja tidak pernah ikut ter-overwrite oleh sync (tetap backend-owned, ADR-014).

### T3.6 — Ikon POI per-kategori + taksonomi RS + guardrail kategori  *(2026-07-06)*
- `DONE` (WebView+Backend) · Depends: T3.4-L2
- **Done when:** ikon POI diturunkan dari `category` (bukan hardcode `pin`); taksonomi ikon rumah sakit siap; kategori divalidasi di boundary sync.
- Ikon WebView ditata per-segmen `public/icons/{ui,klinis,administrasi,fasilitas,sirkulasi}/`; `app/icons.tsx` pakai `FILE_ICON_DIR` (map nama→segmen) → URL `/icons/<segmen>/<nama>.svg`. ~22 ikon RS ditambah. `categoryIcon()` (`app/lib/api.ts`) memetakan kategori→ikon.
- **ADR-016** (dua repo): kategori POI = closed-set kanonik (`POI_CATEGORIES` di backend = SSOT), `POST /api/poi/sync` tolak 422 kalau ada kategori asing (fail-loud saat klik Sync di Unity), key `categoryIcon()` WebView mirror case-sensitive. Backend↔WebView terbukti identik (26 kategori).
- **Limiter demo kampus (bukan bug):** seed 11 POI cuma punya kategori `Umum`/`Administrasi`/`Laboratorium`, jadi Perpustakaan & Mushola sama-sama `Umum` → sama-sama ikon `pin`. Ikon RS variatif (mosque, cross, dll) baru nyala saat data kategori RS masuk. Daftar kategori final wajib divalidasi ke IT RSI. `POIData.kategori` masih `string` bebas + guardrail validasi; upgrade ke dropdown/enum ditunda sampai daftar RS fix.
- **⚠️ Pending push (Bagus):** 2 perubahan `darsi-backend/app/main.py` belum di-commit — `check=ConnectionPool.check_connection` + `POI_CATEGORIES`/validasi sync. Sudah `py_compile` OK, belum live-test (butuh redeploy Railway).

### T3.5 — Resume state (bukan reload) saat AR selesai
- `DONE` · WebView · Depends: T0.2
- **Done when:** `onARSessionClosed` → balik ke state Home/Cari Lokasi terakhir tanpa reload.
- `app/lib/bridge.ts` expose `onArSessionClosed(handler)` yang men-set `window.onARSessionClosed` (dipanggil Flutter). `app/ArSessionResume.tsx` (mount di layout) nampilin banner konfirmasi ("Kamu telah tiba di X" saat `arrived`), auto-dismiss 4s. State React kejaga karena WebView tidak reload. Diverifikasi live di preview: panggil `window.onARSessionClosed({arrived:true,poiId:'Perpustakaan'})` → banner muncul.

---

## Fase 4 — Flutter bridge (`My-eRSIy-CopyCat`)

> Repo Flutter dummy: `D:\Dev\Projects\Internship\My-eRSIy-CopyCat-` (GitHub `RockHead07/My-eRSIy-CopyCat-`). Ternyata SHELL-nya sudah sebagian besar jadi (dicek 2026-07-02). `webview_flutter: ^4.10.0` confirmed.

### T4.1 — Menu item "Navigasi Indoor"
- `DONE` · Flutter · Depends: Fase 0
- Sudah ada di `menu_items.dart` (`navigasi-indoor`, `actionType: native`, `routeName: darsi-navigation`); `MenuNavigator.handle` route ke `DarsiNavigationScreen`.

### T4.2 — DarsiNavigationScreen + AppBar hijau native
- `DONE` · Flutter · Depends: T4.1
- `lib/features/darsi/darsi_navigation_screen.dart` — header hijau native (back, "Navigasi Indoor", subtitle, ornamen stadium) sesuai ADR-004; body = WebView card dengan loading/error + `PopScope` back.

### T4.3 — Embed WebView + channel bridge
- `DONE` (channel name difix 2026-07-02) · Flutter · Depends: T4.2, T0.3
- `webview_flutter` load `http://10.0.2.2:3000/` (emulator → localhost PC). `JavaScriptChannel` nerima pesan, parse JSON, switch `action`. **Channel name difix `DarsiChannel` → `DarsiBridge`** biar cocok kontrak terkunci + WebView (sebelumnya bentrok → pesan nggak nyambung).

### T4.4 — Launcher UaaL + teruskan payload
- `DONE` (2026-07-04, verified di device) · Flutter · Depends: T4.3, T1.1
- Unity di-export ke Android library, di-embed ke `My-eRSIy-CopyCat-/android/` (modul `:unityLibrary`). `_launchAr` bukan stub lagi: MethodChannel `darsi/unity` method `launchAr` → `MainActivity` launch `DarsiUnityActivity` (subclass `UnityPlayerGameActivity`) dgn payload sebagai **intent-extra** (bukan `UnitySendMessage` — hindari race saat cold-launch); `UaaLEntryPoint.Start()` baca extra `darsiPayload`. Empat gotcha AGP-9 didokumentasi di memory + `tool/reintegrate-unity.ps1`.

### T4.5 — Relay event Unity → WebView + return flow
- `DONE` (2026-07-04) · Flutter · Depends: T4.4, T1.6
- Jalur balik: Unity C# `SendEventToFlutter` → `UnityBridge` (Kotlin static, hop ke platform thread) → MethodChannel → Dart handler → inject `window.onARSessionClosed(payload)` ke WebView. Back dari AR: `DarsiUnityActivity` **tidak destroy Unity** (destroy = crash native di JNI_OnUnload) — pakai `moveTaskToBack()` (Unity di task terpisah) balik ke `DarsiNavigationScreen` yang masih utuh; `excludeFromRecents` sembunyikan task Unity dari recent apps.

### T4.6 — Native method name Unity → host Activity
- `DONE` (2026-07-04) · Flutter+Unity · Depends: T4.4
- Placeholder `onUnityMessage` diganti: `SendEventToFlutter` sekarang panggil `com.rsislam.surabaya.rs_islam_app.UnityBridge.send(event, json)` (CallStatic), yang forward ke MethodChannel. Dua sisi selaras.

---

## Fase 5 — Polish AR UX  *(aditif, tidak nge-block fase lain)*

### T5.1 — Tombol back di dalam AR (kanan-atas)
- `TODO` · Unity · Depends: Fase 1
- Sekarang scene AR tidak punya affordance keluar yang terlihat — user cuma andalkan tombol back Android. Tambah tombol back uGUI di kanan-atas, wired ke `UaaLEntryPoint.CloseArSession` (jalur exit yang sudah ada → `arSessionClosed`). Sengaja **bukan** topbar Flutter asli: AR = Activity full-screen, overlay Flutter di atas AR butuh `flutter_unity_widget` (platform-view) yang rewel dgn ARCore — uGUI yang di-render Unity sendiri jauh lebih stabil (lihat pembahasan host/guest UaaL).

### T5.2 — Out-of-bounds coverage notice (ADR-019)
- `TODO` · Unity · Depends: Fase 1; tuning depends: scan RSI (Sprint 2)
- Komponen `NavBoundaryNotifier`: deteksi kamera keluar tepi NavMesh (auto-derive, hysteresis) → billboard AR menghadap user "Di luar jangkauan navigasi" + panah balik ke titik NavMesh terdekat. Framing = **coverage**, bukan larangan fisik (lorongnya nyata & bisa dijalani — lihat ADR-019). MVP dibangun sekarang (murni editor, tak butuh device); angka threshold & caveat NavMesh multi-lantai (ADR-018) di-tune saat scan RSI asli masuk. Butuh expose `UaaLEntryPoint.IsLocalized`.

### T5.3 — Navigasi lintas-lantai: rute tersegmentasi + handoff lift (ADR-020)
- `EDITOR-DONE` (2026-07-20) · Unity · **tes lapangan RSI = satu-satunya yang tersisa**
- Scene `DARSi-Indoor Navigation` punya **11 POI RSI 2 lantai** (4 Ground, 7 Lantai 1) + `[Ground] Lift`/`[Lantai1] Lift` sebagai penghubung vertikal.
- Terimplementasi & terverifikasi di Editor (termasuk lewat UnityEvent `LocalizationSuccess` asli, dua arah): `FloorTransitionController` state machine `Idle → ToConnector → AwaitingRelocalize → ToDestination`. Deteksi beda lantai dari `FloorVisibilityManager` (indeks cluster) vs `POIData.Floor`. Auto-relocalize diserahkan ke SDK (`SingleFrameLocalizationManager.backgroundLocalization`), bukan loop sendiri. Setelah re-localize, lantai user ditentukan ULANG dari clustering — tidak mengasumsikan user menuruti instruksi. Tombol batal wired ke `CancelTransition()` di setiap state.
- **`NavMeshLink` TIDAK dipakai** (menyimpang dari draft ADR-020 poin 3 → dicabut oleh **ADR-020-B**). Lantai tetap pulau NavMesh terpisah; jarak lintas-lantai dijumlahkan per-segmen (`DestinationFloorLabel`, tampil "24 m · Lantai 1"). AR route tidak pernah menembus plafon karena `currentDestination` selalu di-set ke lift, bukan POI lintas-lantai.
- Amandemen terkait: **ADR-020-A** (konektor vertikal = bukan destinasi; amenity multi-instance dikelompokkan) — belum diimplementasi di WebView, nunggu T5.3 stabil.
- Prasyarat data yang ditemukan: 4 `poiCollider` (Ground, Resepsionis, Farmasi, IGD) berjarak >1 m dari NavMesh → `PathInvalid`. Sudah dibetulkan; ambang snap vertikal `CalculatePath` ~1 m.
- **Belum terbukti (butuh device di RSI):** apakah localize sungguhan berhasil di lantai baru, berapa lama, apakah `LocalizeFrame()` me-restart jendela `bgLocalizationDuration` (60 dtk), apakah `AgentPosition` benar memindahkan agent ke pulau NavMesh lantai baru. Protokol: [FIELD-TEST-T5.3.md](FIELD-TEST-T5.3.md).

---

## Catatan sequencing

- **Fase 0 adalah gate mutlak.** T0.8 (identitas MyRSIy) mem-blok seluruh Fase 2 — kejar konfirmasi Pak Farris lebih awal biar tidak jadi bottleneck.
- **Fase 1 bisa jalan penuh** hanya bergantung Fase 0 (dokumen), tidak nunggu backend — kerjakan duluan.
- **Fase 3** (WebView UI POI) independen dari Fase 2 (friendlist) — bisa paralel.
- **Fase 4** shell-nya (menu, DarsiNavigationScreen, WebView embed, bridge receiver) ternyata sudah jadi (T4.1–T4.3 DONE). Sisa yang berat: **T4.4 launcher UaaL** — butuh Unity di-export ke Android library (.aar) dulu. Leg WebView↔Flutter (tanpa Unity) sudah bisa dites end-to-end sekarang.
- **UPDATE 2026-07-04:** Fase 4 (T4.4–T4.6) SELESAI & terverifikasi di device. Stack produksi berdiri: WebView → Vercel (`darsi-indoor-navigation.vercel.app`), backend+Postgres → Railway. APK default `_darsiUrl` → Vercel. Tes lapangan cukup HP + internet.

---

## Backlog / Known Issues (ditunda sadar — bukan bug yang harus segera)

- **PERF-1 — Navigasi Indoor terasa berat.** Ditemukan 2026-07-04 saat tes device. Dugaan penyebab (urut dampak, BELUM diukur): (1) **debug build** — semua APK sejauh ini `--debug` (JIT, no AOT, debug asserts) → bukan performa asli; validasi harus di `--profile`/`--release` dulu. (2) **Unity residency** — desain T4.5 sengaja TIDAK destroy Unity (destroy = crash), jadi setelah masuk AR sekali engine Unity+IL2CPP+ARCore tetap di RAM (ratusan MB) → bikin WebView & app sluggish di HP mid-range. (3) **animasi `.darsi-pulse`** di WebView nge-animate `box-shadow` infinite (bukan GPU-composited → repaint tiap frame). **Best-practice sebelum eksekusi: ukur dulu** — build `--profile`, kalau ringan berarti cuma debug-overhead (fix lain jadi YAGNI); kalau masih berat, `dumpsys meminfo` buktikan Unity residency sebelum sentuh arsitektur (langkah berisiko). Fix (3) aman dikerjakan kapan saja (ganti box-shadow → `transform: scale`+`opacity`).
- **Foto POI asli.** Kolom `photos` masih kosong → UI render placeholder. Isi URL setelah foto kampus ada.
- **Release build + kecilin APK.** Ganti `--debug` → `--release` (~300MB → ~150-180MB, strip symbol + R8). Untuk demo/distribusi.
- **Re-entry AR dgn POI berbeda.** Setelah balik dari AR (Unity paused), masuk lagi dgn POI lain: `DarsiUnityActivity` singleTask → `onNewIntent`, tapi `UaaLEntryPoint` baca intent cuma di `Start()`. Perlu handle intent baru saat resume kalau mau ganti tujuan tanpa restart Unity.
