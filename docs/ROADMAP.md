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
- `TODO` · WebView · Depends: —
- **Done when:** project jalan sesuai `AGENTS.md` (baca guide Next.js versi repo dulu).

### T3.2 — Home screen
- `TODO` · WebView · Depends: T3.1
- **Done when:** sesuai `DESIGN_SYSTEM` + `FLOWS §2` — no header, no jarak, no kata "POI", FAB kamera + quick action.

### T3.3 — Cari Lokasi screen
- `TODO` · WebView · Depends: T3.1
- **Done when:** search + filter chip + confirmation card + CTA "Mulai Navigasi AR", tanpa meter.

### T3.4 — Integrasi API POI
- `TODO` · WebView+Backend · Depends: T3.2, T3.3
- **Done when:** `/api/poi/popular`, `/api/poi/search`, `/api/poi/categories` konsumsi jalan; response tanpa field jarak.

### T3.5 — Resume state (bukan reload) saat AR selesai
- `TODO` · WebView · Depends: T0.2
- **Done when:** `onARSessionClosed` → balik ke state Home/Cari Lokasi terakhir tanpa reload.

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
