# ROADMAP.md — DARSI Indoor Navigation (whole-project)

> Task breakdown lintas semua layer DARSI (Unity · WebView · Backend · Flutter bridge).
> Format tiap task: **Status · Repo · Depends** lalu **Done when**.
> Status: `TODO` / `WIP` / `BLOCKED` / `DONE`. Perubahan arsitektur → catat ADR baru di `DECISIONS.md` dulu.
>
> Urutan wajib: **Fase 0 → Fase 1**. Fase 2/3/4 bisa jalan lebih paralel setelah Fase 0 clear.
> `⚠️ NEEDS DECISION` = nunggu jawaban eksternal (Pak Farris / IT RSI), bukan bisa diputus sendiri.

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

### T0.8 — ⚠️ NEEDS DECISION: identitas user stabil dari MyRSIy
- `BLOCKED` · Backend/Flutter · Depends: T0.7 · **Blocker untuk seluruh Fase 2**
- **Done when:** Pak Farris/IT RSI konfirmasi MyRSIy menyediakan **user ID stabil + handle** yang bisa jadi target friend-request lewat bridge. Friend-request TIDAK bisa dibangun tanpa ini. Jawaban dicatat sebagai ADR.

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
- **Known gap (flag to Bagus):** `POIData` has no stable ID field distinct from display name — `poiId` from Flutter must match `POIData.poiName`/GameObject name exactly. `FindBestMatchWithContext` is fuzzy-match, not strict-ID lookup. Fine for now (exact match resolves first), but worth a real `id` field on `POIData` before Flutter integration if names can ever drift from IDs.

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

## Fase 2 — Friendlist + Cari Teman  *(BLOCKED oleh T0.8)*

Model final (ADR-013): friend-request persisten. Data-entry & manajemen teman di **WebView**; posisi live cuma di **AR**.

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
- **T3.4-L1 — Tambah `building`/`floor` ke `POIData.cs`** (aditif, di-flag karena file protected; per ADR-014). `TODO` · Unity
- **T3.4-L2 — Unity Editor "Sync POIs → Backend"** (tombol menu) + endpoint tulis FastAPI (`POST /api/poi/sync`, auth admin, upsert). Menggantikan seed manual → Unity jadi sumber tunggal. `TODO` · Unity+Backend · Depends: T3.4-L1, T3.4.3

### T3.5 — Resume state (bukan reload) saat AR selesai
- `DONE` · WebView · Depends: T0.2
- **Done when:** `onARSessionClosed` → balik ke state Home/Cari Lokasi terakhir tanpa reload.
- `app/lib/bridge.ts` expose `onArSessionClosed(handler)` yang men-set `window.onARSessionClosed` (dipanggil Flutter). `app/ArSessionResume.tsx` (mount di layout) nampilin banner konfirmasi ("Kamu telah tiba di X" saat `arrived`), auto-dismiss 4s. State React kejaga karena WebView tidak reload. Diverifikasi live di preview: panggil `window.onARSessionClosed({arrived:true,poiId:'Perpustakaan'})` → banner muncul.

---

## Fase 4 — Flutter bridge (`My-eRSIy-CopyCat`)

> Di luar dua repo utama; jadwalkan setelah kontrak (Fase 0) + minimal Fase 1 siap dites.

### T4.1 — Menu item "Navigasi Indoor"
- `TODO` · Flutter · Depends: Fase 0
- **Done when:** item ditambah ke `menu_items.dart`, action `webview` → `DarsiNavigationScreen`.

### T4.2 — DarsiNavigationScreen + AppBar hijau native
- `TODO` · Flutter · Depends: T4.1
- **Done when:** AppBar native (back, judul, subtitle, ornamen) sesuai ADR-004; body kosong siap WebView.

### T4.3 — Embed WebView + channel `launchAR`
- `TODO` · Flutter · Depends: T4.2, T0.3
- **Done when:** `webview_flutter` load URL Next.js; `JavaScriptChannel` nerima `launchAR`.

### T4.4 — Launcher UaaL + teruskan payload
- `TODO` · Flutter · Depends: T4.3, T1.1
- **Done when:** terima `launchAR` → launch Unity (UaaL) → `UnitySendMessage("UaaLEntryPoint", "ReceiveLaunchPayload", json)`.

### T4.5 — Relay event Unity → WebView + return flow
- `TODO` · Flutter · Depends: T4.4, T1.6
- **Done when:** event Unity (`arSessionClosed` dll) di-inject balik ke WebView (JS); tutup Unity → balik ke `DarsiNavigationScreen` → WebView resume (ADR return flow).

---

## Catatan sequencing

- **Fase 0 adalah gate mutlak.** T0.8 (identitas MyRSIy) mem-blok seluruh Fase 2 — kejar konfirmasi Pak Farris lebih awal biar tidak jadi bottleneck.
- **Fase 1 bisa jalan penuh** hanya bergantung Fase 0 (dokumen), tidak nunggu backend — kerjakan duluan.
- **Fase 3** (WebView UI POI) independen dari Fase 2 (friendlist) — bisa paralel.
- **Fase 4** butuh Fase 1 minimal untuk end-to-end test bridge.
