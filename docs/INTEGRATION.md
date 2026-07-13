# INTEGRATION.md — Kontrak Komunikasi (Unity side)

> Kontrak ini WAJIB sama persis dengan yang didefinisikan di `API_CONTRACT.md` pada repo WebView. Kalau salah satu diubah, update dua-duanya.

## Alur komunikasi 3 arah

```
WebView (JS) --postMessage--> Flutter (Dart) --UnitySendMessage--> Unity (C#)
Unity (C#) --UnitySendMessage callback--> Flutter (Dart) --JS injection--> WebView (JS)
```

## Message: WebView → Flutter → Unity (launch AR)

**Trigger:** user tap "Mulai Navigasi AR" (dari Cari Lokasi), quick action "Cari Teman" di Home, atau FAB kamera di WebView.

```json
{
  "action": "launchAR",
  "mode": "navigate | freeExplore | findFriend",
  "poiId": "string | null",
  "poiName": "string | null",
  "floor": "string | null",
  "building": "string | null",
  "connectionId": "string | null"
}
```

`poiId` (mode `navigate`) = GUID stabil `POIData.poiId` (kolom `unity_id` di backend), BUKAN nama tampilan — supaya rename POI tak mematahkan navigasi. Unity resolve GUID dulu (`UaaLEntryPoint.ResolvePoi`), fallback fuzzy-match `poiName` untuk POI legacy tanpa GUID ter-sync. `poiName` tetap dikirim sebagai label + fallback.

`mode: 'findFriend'` — trigger dari WebView saat user tap "Navigasi ke [teman]" pada teman yang sudah berstatus `ar-active` di friendlist (lihat ADR-013, `FLOWS.md` bagian 5). Payload wajib sertakan `connectionId` (ID koneksi friendlist yang sudah `accepted`, BUKAN kode sekali-pakai). Unity TIDAK menampilkan UI friendlist/add-friend sama sekali — itu murni tanggung jawab WebView. Begitu localize, Unity langsung render posisi teman + jarak, tanpa modal/keyboard apapun (lihat ADR-013 — panduan Google ARCore soal menghindari full-screen takeover di dalam AR).

## Identitas user (host MyRSIy → WebView, BUKAN lewat Unity) — ADR-017

Fitur **Cari Teman = login-only**; navigasi lokasi tetap boleh tamu. Identitas user
**tidak** lewat Unity dan **tidak** ikut payload `launchAR` — ia disuntik host (MyRSIy via
Flutter) langsung ke WebView saat load: `window.__DARSI_USER__ = { userId, handle? }`
(atau `null` untuk tamu). Detail kontrak ada di `API_CONTRACT.md` (repo WebView).

Yang WAJIB dari MyRSIy cuma **`userId`** stabil + **tidak didaur ulang** (UUID/PK).
`handle` opsional — DARSI mint sendiri kalau tak ada (tak perlu MyRSIy expose PII). Unity
tak menyentuh identitas ini sama sekali; ia cuma terima `connectionId` yang sudah
`accepted` lewat payload `launchAR` seperti biasa.

## Endpoint friendlist (dipanggil dari WebView, BUKAN dari Unity langsung)

| Endpoint | Fungsi |
|---|---|
| `POST /api/friends/request` | Kirim friend-request via identifier persis (bukan direktori terbuka) |
| `POST /api/friends/respond` | Penerima accept/reject request |
| `GET /api/friends` | List koneksi `accepted` + presence status-only (`online`/`ar-active`/`offline`) |
| `DELETE /api/friends/{id}` | Hapus koneksi |

Unity hanya menerima `connectionId` yang sudah `accepted` dari Flutter (diteruskan dari WebView) — Unity tidak pernah panggil endpoint friendlist secara langsung, dan tidak pernah menerima data presence/lokasi lewat jalur lain selain payload ini.

Di sisi Unity, method penerima (perlu dibuat — belum ada di repo per hasil cleanup UI Toolkit):

```csharp
// GameObject penerima: "UaaLEntryPoint" (belum dibuat)
// Method: ReceiveLaunchPayload(string json)
// Parse JSON → jika mode == "navigate", langsung set tujuan ke NavigationAdapter
// setelah localize sukses. Jika "freeExplore", buka UI pilih tujuan di dalam AR.
```

**Status implementasi:** ✅ SUDAH ADA. `Assets/Scripts/UaaLEntryPoint.cs` live di scene `WholePSDKU` (singleton `Instance`, `DontDestroyOnLoad`). Payload masuk lewat **intent extra** `darsiPayload` (bukan `UnitySendMessage` — AR launch sebagai activity baru, player belum load saat Flutter fire), di-buffer sampai localize sukses (ADR-007), lalu di-route: `navigate` → `NavigationAdapter.NavigateToPOI` (resolve GUID-first, fallback fuzzy name), `freeExplore` → `NavigationUIController.ToggleDestinationSelectUI`, `findFriend` → **stub** (toast "belum tersedia", blocked ROADMAP T0.8).

## Message: Unity → Flutter → WebView (AR session events)

**Mekanisme (✅ tersambung end-to-end):** `UaaLEntryPoint.SendEventToFlutter` → `UnityBridge.send` (Kotlin static, `com.rsislam.surabaya.rs_islam_app.UnityBridge`) → hop ke platform thread → `MethodChannel("darsi/unity").invokeMethod` → Dart `_onUnityEvent` (`darsi_navigation_screen.dart`) → untuk `arSessionClosed`, teruskan ke WebView via `window.onARSessionClosed(payload)`.

| Event | Kapan dikirim | Payload |
|---|---|---|
| `arSessionReady` | Setelah AR Canvas aktif, sebelum localize | `{}` |
| `localizationSuccess` | MultiSet berhasil localize | `{ building, floor }` — di-fire dari `OnLocalizationSuccess` (wired ke MultiSet `LocalizationSuccess` UnityEvent). Sampai ke Dart tapi **belum ada aksi host** (no consumer). |
| `navigationArrived` | User sampai tujuan POI | `{ poiId }`. **Deteksi arrival ada di `MultiSetSDK.dll` (privat)** — di-hook lewat toast SDK "You arrived at the destination!" yang dicegat `ToastTranslator`, lalu memanggil `UaaLEntryPoint.ReportArrivalAtActiveTarget()` (ter-guard: hanya sesi `navigate` aktif, sekali). Sampai ke Dart tapi belum ada aksi host. |
| `arSessionClosed` | User tap back / keluar AR | `{ arrived, poiId, poiName }` — `arrived` = flag internal `_arrived` (jadi `true` hanya kalau arrival POI sempat terdeteksi, lihat baris `navigationArrived`), `poiId` (GUID) dari payload `launchAR` aktif (null kalau freeExplore), `poiName` = nama tampilan POI yang di-resolve (dipakai WebView untuk banner "Kamu telah tiba di …", karena `poiId` kini GUID). Dari `OnApplicationQuit`. Flutter teruskan ke WebView (`onARSessionClosed`, lihat `API_CONTRACT.md`). |

## Message: Cari Teman (friend-request — lihat ADR-013)

Manajemen teman (kirim/accept request, lihat presence) sepenuhnya di WebView + backend. Unity **tidak pernah** memanggil endpoint friendlist dan **tidak pernah** menampilkan UI kode/request — Unity murni konsumen `connectionId` yang sudah valid (lihat section "Endpoint friendlist" di atas) untuk render posisi + navigasi. Detail flow ada di `FLOWS.md` bagian 5.

**Status implementasi:** belum ada. Perlu endpoint baru di FastAPI + logic render posisi di Unity (kemungkinan extend `FriendListPanel.cs` dan `PhotonManager.cs`/`PlayerSync.cs`).

## Catatan penting untuk siapapun yang lanjutkan development

- Jangan bikin Unity terima data langsung dari WebView tanpa lewat Flutter — WebView tidak punya akses `UnitySendMessage` langsung, harus lewat native bridge Flutter.
- Deteksi arrival POI **tidak** ada di `NavigationAdapter` (script itu tak punya konsep arrival) — ada di `MultiSetSDK.dll`, di-observasi lewat toast di `ToastTranslator`. Kalau string toast SDK berubah di update, samakan `ToastTranslator.ArrivalKeyEn`.
- `localizationSuccess` & `navigationArrived` sudah mengalir ke Dart tapi belum dikonsumsi host (no-op by design, tambah handler kalau butuh). Hanya `arSessionClosed` yang diteruskan ke WebView.
- Gap terbuka: `mode:findFriend` masih stub (blocked ROADMAP T0.8 — butuh friend graph + identity + Photon render), dan outbound `localizationSuccess/navigationArrived` belum ada consumer di WebView.
