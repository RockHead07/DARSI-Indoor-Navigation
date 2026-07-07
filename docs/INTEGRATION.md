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

**Status implementasi:** belum ada. Ini task berikutnya setelah dokumentasi ini dikunci — buat `UaaLEntryPoint.cs` sebagai satu-satunya pintu masuk data dari Flutter.

## Message: Unity → Flutter → WebView (AR session events)

| Event | Kapan dikirim | Payload |
|---|---|---|
| `arSessionReady` | Setelah AR Canvas aktif, sebelum localize | `{}` |
| `localizationSuccess` | MultiSet berhasil localize | `{ building, floor }` — bisa reuse `PhotonManager.NotifyLocalizationSucceeded` |
| `navigationArrived` | User sampai tujuan | `{ poiId }` |
| `arSessionClosed` | User tap back / keluar AR | `{ arrived, poiId }` — `arrived` = `true` jika `navigationArrived` sempat terkirim sebelum sesi ditutup, `poiId` diambil dari payload `launchAR` yang aktif (null kalau free explore). Flutter yang menggabungkan (tracking state `navigationArrived` terakhir), Unity TIDAK perlu tahu soal ini — Unity cukup kirim `arSessionClosed` dengan `poiId` dari tujuan aktif + `arrived` dari flag internal `NavigationAdapter`. Flutter pakai event ini untuk kembali ke `DarsiNavigationScreen` dan meneruskan payload yang sama ke WebView (`onARSessionClosed`, lihat `API_CONTRACT.md`). |

## Message: Cari Teman (friend-request — lihat ADR-013)

Manajemen teman (kirim/accept request, lihat presence) sepenuhnya di WebView + backend. Unity **tidak pernah** memanggil endpoint friendlist dan **tidak pernah** menampilkan UI kode/request — Unity murni konsumen `connectionId` yang sudah valid (lihat section "Endpoint friendlist" di atas) untuk render posisi + navigasi. Detail flow ada di `FLOWS.md` bagian 5.

**Status implementasi:** belum ada. Perlu endpoint baru di FastAPI + logic render posisi di Unity (kemungkinan extend `FriendListPanel.cs` dan `PhotonManager.cs`/`PlayerSync.cs`).

## Catatan penting untuk siapapun yang lanjutkan development

- Jangan bikin Unity terima data langsung dari WebView tanpa lewat Flutter — WebView tidak punya akses `UnitySendMessage` langsung, harus lewat native bridge Flutter.
- `PhotonManager.NotifyLocalizationSucceeded(building, floor)` sudah ada di codebase — ini titik integrasi yang paling masuk akal untuk event `localizationSuccess` di atas.
- Belum ada `UaaLEntryPoint.cs` — ini yang paling prioritas dibuat sebelum fitur apapun di atas bisa jalan end-to-end.
