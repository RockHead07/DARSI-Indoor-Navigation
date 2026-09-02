# Cari Teman (Friend Mode) — Design Spec

> Status: draft, menunggu review pemilik project (Bagus). Belum ada kode yang ditulis berdasarkan dokumen ini.

## Goal

Bangun fitur Cari Teman lintas-repo (darsi-backend, Unity, WebView terpisah) sesuai `docs/FLOWS.md` §5 dan ADR-010/011/013/017, dengan urutan kerja **Unity lebih dulu** supaya mekanisme render+navigasi teman "matang" sebelum WebView friendlist (yang UI-nya belum dirancang di sini) dibangun di atasnya.

## Non-goals (sengaja di luar spec ini)

- Desain UI WebView friendlist — dapat kontrak API saja di sini, desain visualnya brainstorming terpisah setelah Unity selesai (permintaan eksplisit pemilik project).
- Auth token sungguhan dari MyRSIy — tetap model client-trusted `user_id`, konsisten dengan `presence` yang sudah ada (ADR-017 belum menuntut lebih dari ini).
- Body-tracking/animasi gestur avatar teman — posisi+orientasi horizontal saja (lihat `PlayerSync.cs` existing, floor-snapped).
- Model 3D avatar teman yang baru — gap aset dicatat, keputusan pemakaian aset sementara ada di §4.4, tapi pengadaan aset baru bukan bagian pekerjaan kode ini.

## Amandemen: `docs/FLOWS.md` §5b (dot → humanoid avatar)

`FLOWS.md` ditandai `LOCKED` di headernya, dan teks §5b saat ini eksplisit bilang **"render dot posisi teman + jarak real-time"**. Permintaan pemilik project sesi ini eksplisit mengubah ini jadi render avatar humanoid, bukan dot. Ini BUKAN penyimpangan diam-diam — dicatat di sini sebagai amandemen sadar, dengan alasan: dot minim secara visual untuk fitur yang sudah dibangun mekanisme full-body-position-nya (`PlayerSync.cs`), dan constraint sesungguhnya di §5b ("TIDAK ADA input/keyboard/modal di dalam AR — murni visual, tanpa full-screen takeover", prinsip ARCore "magic lens") **tidak dilanggar** oleh avatar humanoid — itu tetap murni visual, tanpa modal/takeover apa pun. Hanya kata "dot" di teks §5b yang perlu diperbarui jadi "avatar humanoid" setelah spec ini disetujui.

## 1. Ringkasan temuan: sebagian besar mekanismenya sudah ada

Sebelum spec ini ditulis, enam file di `Assets/Scripts/Multiplayer/` dan `Assets/UI/` dibaca penuh: `PhotonManager.cs`, `PlayerSync.cs`, `PlayerNavigationController.cs`, `FriendListPanel.cs`, `FriendListEntry.cs`, `PlayerInfoPopup.cs`. Semuanya ada di daftar "sudah jalan, jangan diubah tanpa alasan kuat" di `CLAUDE.md`, tapi **tidak disambungkan ke scene manapun** (diverifikasi lewat grep `Assets/Scenes` — nol referensi) dan dibangun untuk model open-room lama, sebelum ADR-010/013 mengunci model friend-request.

**Keputusan: adaptasi, bukan bangun ulang.** Mekanisme render posisi, smoothing, distance calc, dan navigasi-ke-pemain sudah benar secara desain. Yang salah cuma SIAPA yang dirender — `PhotonManager` auto-join room per lantai (`buildingId_floorId`, `autoConnect=true`) dan `FriendListPanel` menampilkan SEMUA `PlayerSync` yang ditemukan di scene, alias "semua yang online di lantai ini" — persis pola auto-discovery yang ditolak ADR-010/013.

### 1.1 Sudah benar, best-practice, dipertahankan apa adanya

- `PlayerNavigationController.OnPlayerLeftRoom`: otomatis menghentikan navigasi kalau target keluar room. Ini SUDAH memenuhi requirement ADR-011 ("posisi live auto-terminate begitu salah satu pihak menutup sesi AR") tanpa perlu kode baru.
- `FriendListPanel`: cache entry di `Dictionary<int, FriendListEntry>`, cuma instantiate/destroy saat keanggotaan berubah, bukan setiap tick refresh.
- Floor-snapped position + rotasi Y-only (`PlayerSync.OnPhotonSerializeView`): simplifikasi yang tepat untuk wayfinding indoor, tidak perlu full 6DOF.

### 1.2 Best-practice yang perlu diperbaiki saat diadaptasi

1. **Room tidak benar-benar privat.** `RoomOptions` di `PhotonManager.TryJoinRoom` cuma set `MaxPlayers`, tidak pernah set `IsVisible = false`. Begitu nama room jadi rahasia pasangan-teman (`connectionId`), room itu wajib tidak muncul di listing lobby publik.
2. **Nickname bocor identitas yang salah.** `PhotonNetwork.NickName` diturunkan dari `SystemInfo.deviceUniqueIdentifier` (`GetDefaultNickname()`), dan `PlayerSync` menampilkan `NickName` mentah itu di nametag. Nama yang tampil ke teman jadi ID device terpotong, bukan handle teman yang sesungguhnya.
3. **API usang, tidak konsisten dengan kode lain di repo yang sama.** `FriendListPanel.ResolveArCamera` dan `FriendListEntry.UpdateDistance` pakai `Object.FindObjectOfType<Camera>()` (obsolete). `AssistantTestPanel.cs` (ditulis sesi ini) sudah benar pakai `FindAnyObjectByType`.
4. **Warna avatar tidak stabil antar-run.** `AvatarColors[Mathf.Abs(pName.GetHashCode()) % AvatarColors.Length]` — `string.GetHashCode()` tidak dijamin stabil lintas proses di semua runtime .NET/Mono. Warna lingkaran avatar teman bisa berubah tiap buka app.
5. **Belum pernah benar-benar dijalankan.** Karena tidak pernah disambungkan ke scene, "sudah divalidasi tidak ada compile error" cuma berarti "kompilasi", bukan "terbukti jalan lewat sesi Photon sungguhan". Verifikasi ulang WAJIB, bukan asumsi.

## 2. Backend (darsi-backend)

### 2.1 Skema

```sql
CREATE TABLE profiles (
  user_id text PRIMARY KEY,        -- sama seperti presence.user_id (ADR-017)
  handle text UNIQUE NOT NULL,     -- kode acak pendek, mis. "DARSI-7F3K"
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE connections (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  requester_id text NOT NULL REFERENCES profiles(user_id),
  addressee_id text NOT NULL REFERENCES profiles(user_id),
  status text NOT NULL CHECK (status IN ('pending','accepted','declined')),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (requester_id, addressee_id)
);

CREATE TABLE blocks (
  blocker_id text NOT NULL REFERENCES profiles(user_id),
  blocked_id text NOT NULL REFERENCES profiles(user_id),
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (blocker_id, blocked_id)
);

CREATE TABLE reports (
  reporter_id text NOT NULL REFERENCES profiles(user_id),
  target_id text NOT NULL REFERENCES profiles(user_id),
  reason text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);
```

`profiles` dibuat lazy: hanya saat user pertama kali pakai Cari Teman, bukan saat login biasa. `handle` di-generate kode acak pendek, retry pada tabrakan unique.

### 2.2 Endpoint

Terkunci di `FLOWS.md` §5a: `POST /api/friends/request {identifier}`, `POST /api/friends/respond {requestId, action}`, `GET /api/friends`.

Tambahan yang diperlukan di luar teks §5a (celah nyata: WebView butuh cara melihat daftar request masuk untuk render tombol accept/decline, dan ADR-013 mewajibkan block/report):
- `GET /api/friends?status=pending|accepted` — query param pada endpoint yang sudah ada, bukan route baru.
- `POST /api/friends/block {targetId}`.
- `POST /api/friends/report {targetId, reason}` — log minimal, tanpa aksi otomatis; tinjauan manual di luar scope backend ini.

Rate-limit: query `COUNT` sederhana di endpoint request (contoh: maks 20 pending outgoing per user, maks 5 request baru/jam) — tidak ada tabel/library baru.

Auth: trust `user_id` dari body, pola sama dengan endpoint `presence` yang sudah ada (`# ponytail:` comment yang sudah ada mengakui ini sengaja sementara).

## 3. Unity

### 3.1 `PhotonManager.cs`

- Hapus auto-connect ke room per-lantai saat `Start()`. Tambah method publik baru `JoinFriendRoom(string connectionId, string displayName)`:
  - Set `PhotonNetwork.NickName = displayName` SEBELUM connect (memperbaiki temuan 1.2.2 — nickname jadi milik SENDIRI, bukan device ID, dan Unity tetap tidak pernah menyentuh identitas PIHAK LAIN, ADR-017 tetap utuh).
  - `RoomOptions` untuk room ini WAJIB `IsVisible = false` (memperbaiki temuan 1.2.1) — nama room (`connectionId`) berfungsi sebagai kunci pasangan-privat, tidak boleh muncul di listing lobby.
  - `JoinOrCreateRoom(connectionId, options, TypedLobby.Default)` — dua klien dengan `connectionId` yang sama otomatis membentuk room privat berisi 2 orang, tanpa perlu registry room terpisah di backend.
  - Dipanggil dari `UaaLEntryPoint.ApplyPayload()`, `case "findFriend":` (`Assets/Scripts/UaaLEntryPoint.cs:176-182`) — titik ini SUDAH ADA, sudah menerima `connectionId` lewat `LaunchPayload`, dan saat ini cuma toast placeholder ("Fitur Cari Teman di AR belum tersedia") yang secara eksplisit mengutip ROADMAP.md T0.8 sebagai pemblokirnya. Spec ini yang menyelesaikan blocker itu. BUKAN dipanggil dari `Start()`.
  - `LaunchPayload` (struct yang sama) perlu field baru `displayName` — belum ada di field list saat ini (`action, mode, poiId, poiName, floor, building, connectionId`). WebView wajib mengirim ini di payload `launchAR` (lihat §4).
- Tambah `LeaveFriendRoom()`, dipanggil saat sesi AR Cari Teman berakhir (tombol tutup, app pause, atau AR session lain dimulai) — menegakkan "tidak ada cache posisi setelah sesi berakhir" (ADR-011) dengan cara paling sederhana: keluar room = tidak ada lagi yang men-sync posisi.
- `Connect()`/callback existing (`OnConnectedToMaster`, `OnJoinedRoom`, dst.) dipakai apa adanya, tidak perlu diubah strukturnya.

### 3.2 `PlayerSync.cs`

- `ApplyOwnershipVisuals()`: ganti `capsuleBody.SetActive(true)` / `humanoidBody.SetActive(false)` untuk kasus remote-player jadi sebaliknya, begitu prefab humanoid terisi (lihat §4.4 untuk keputusan aset). Field `humanoidBody` sudah ada, tidak perlu field baru.
- Sisanya (position/rotation sync, floor-snap, name-tag billboard) tidak berubah.

### 3.3 `FriendListPanel.cs` / `FriendListEntry.cs` / `PlayerInfoPopup.cs` / `PlayerNavigationController.cs`

- Tidak berubah secara struktural — begitu room hanya pernah berisi diri sendiri + satu teman `accepted`, "list semua `PlayerSync` di scene" otomatis benar. Perbaikan privasi ada di lapisan join room (§3.1), bukan di sini.
- Perbaikan kecil sambil menyentuh file ini: `Object.FindObjectOfType<Camera>()` → `FindAnyObjectByType<Camera>()` di `FriendListPanel.ResolveArCamera` dan `FriendListEntry.UpdateDistance` (temuan 1.2.3).
- `FriendListEntry.GetInitials`/warna avatar: ganti `pName.GetHashCode()` dengan hash deterministik sederhana (mis. jumlah kode karakter, atau FNV-1a manual) supaya warna avatar stabil antar-run (temuan 1.2.4).

### 3.4 Gap aset avatar humanoid

Tidak ada model avatar khusus teman. Yang ada: `Assets/Avatar/Model/AvatarSample_A.vrm`, sudah terpasang sebagai model avatar AI guide di scene produksi `TestingHCM.unity`. Memakainya ulang untuk teman berarti teman terlihat identik dengan avatar AI guide di dalam AR — membingungkan, bukan solusi gratis.

**Default pragmatis:** tetap pakai `capsuleBody` (sudah berfungsi, sudah tervisualisasi) sampai aset avatar teman yang berbeda tersedia; tinggal aktifkan `humanoidBody` kapan pun aset itu datang, nol perubahan kode. Ditandai di sini supaya tidak diam-diam memblokir "Unity selesai" — kalau pemilik project mau ini jadi blocker, bilang eksplisit sebelum implementasi mulai.

### 3.5 Verifikasi wajib sebelum "Unity selesai"

Karena cluster ini belum pernah benar-benar dijalankan (temuan 1.2.5): sesudah adaptasi, wajib uji dengan DUA device/emulator nyata terhubung ke Photon sungguhan (App ID Photon yang valid, bukan cuma compile check) — pastikan room privat benar-benar tidak terlihat pihak ketiga, posisi tersinkron, navigasi-ke-teman jalan sampai deteksi arrival, dan room ditinggalkan saat sesi AR ditutup.

## 4. WebView (kontrak saja, desain UI menyusul)

Interface yang dikunci untuk dipakai nanti: `POST /api/friends/request {identifier}`, `POST /api/friends/respond {requestId, action}`, `GET /api/friends?status=`, `POST /api/friends/block`, `POST /api/friends/report`, dan bentuk `postMessage` `{action:'launchAR', mode:'findFriend', connectionId}` (WebView juga bertanggung jawab mengirim `displayName`/handle sendiri lewat payload ini, dipakai `JoinFriendRoom` di §3.1). Tidak ada desain UI di spec ini.

## 5. Checklist keamanan/privasi vs ADR-010/011/013/017

| Requirement (FLOWS.md §5, non-negotiable) | Terpenuhi lewat |
|---|---|
| Add-friend hanya by exact identifier, tanpa direktori | `POST /api/friends/request {identifier}` — tidak ada endpoint listing/search user |
| Mutual accept | `connections.status` pending→accepted via `/respond` |
| Rate limit | COUNT query di `/request` (§2.2) |
| Block/report | `blocks`/`reports` table + endpoint (§2.2) |
| Presence status-only, cuma untuk accepted | pola sudah ada di `presence` table, tidak berubah |
| Opt-out | field `invisible` sudah ada di `presence` |
| Auto-terminate posisi saat sesi AR tutup | `LeaveFriendRoom()` (§3.1) + `OnPlayerLeftRoom` yang sudah ada (§1.1) |
| Tidak ada auto-discovery "orang di sekitar" | Room privat per-`connectionId`, bukan per-lantai (§3.1) — inilah perbaikan inti dari cluster lama |

## 6. Di luar scope sesi ini (dicatat, bukan dilupakan)

- Aset 3D avatar teman (§3.4) — kebutuhan konten, bukan kode.
- Desain UI WebView friendlist.
- Auth token sungguhan dari MyRSIy (tetap client-trusted `user_id`).
- Uji lapangan dua-device Photon sungguhan (§3.5) — dijadwalkan setelah implementasi, bukan bagian dari spec ini.
