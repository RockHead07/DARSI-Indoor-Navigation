# FLOWS.md — User Flows (LOCKED)

> Semua flow di bawah ini sudah final berdasarkan diskusi arsitektur. Perubahan flow harus ditambahkan sebagai ADR baru di `DECISIONS.md`.

## 1. Entry Flow

```
MyRSIy Home
  → tap menu "Navigasi Indoor"
  → Flutter push DarsiNavigationScreen
      (AppBar hijau native: back button, "Navigasi Indoor", subtitle, ornamen)
  → body screen load WebView → URL Next.js DARSI
  → WebView tampilkan Home
```

Menu item "Navigasi Indoor" ditambahkan ke `menu_items.dart` (Flutter), action type `webview`, target `DarsiNavigationScreen`.

## 2. Browsing Flow (WebView — tanpa jarak)

**Home:**
- Search bar (placeholder "Cari ruangan, poli, layanan...")
- Quick action: "Cari Lokasi" (Lihat Peta sudah dihapus — lihat ADR-006)
- Destinasi Populer — horizontal card, tampilkan nama + lantai/gedung SAJA (tanpa meter)
- Layanan Lainnya — list, status badge (Buka/Antre/Penuh)
- Floating Action Button kamera — shortcut ke AR (free explore mode)

**Cari Lokasi:**
- Search input + filter chip kategori
- Hasil list: nama + lantai/gedung + status badge (TANPA meter — lihat ADR-007)
- Tap hasil → selected-location confirmation card muncul
- CTA "Mulai Navigasi AR" aktif setelah lokasi dipilih (atau langsung tersedia untuk free explore)

## 3. Transisi ke AR

```
User tap "Mulai Navigasi AR" (dari Home FAB atau Cari Lokasi CTA)
  → WebView: postMessage({ action: 'launchAR', poiId?, poiName?, floor?, building? })
  → Flutter native menerima message
  → Flutter launch Unity via UaaL (UnityPlayerActivity / setara)
  → Unity boot LANGSUNG ke AR Canvas (tanpa splash/login internal — sudah dihapus, lihat ADR-003)
```

## 4. AR Navigation Flow (Unity)

```
AR Canvas aktif
  → MultiSet SDK localize (bagian dari proses masuk AR, BUKAN gate terpisah — lihat ADR-008)
  → Localize berhasil:
      - Jika ada tujuan (poiId dari WebView) → AR Navigation state "Active"
          → arrow, dashed path, bottom drawer (jarak REAL + ETA)
          → state "Reroute" jika user keluar jalur
          → state "Arrived" saat sampai tujuan
      - Jika tidak ada tujuan (free explore) → tampilkan UI pilih tujuan di dalam AR,
        atau buka panel Cari Teman
```

## 5. Cari Teman Flow (friendlist — friend-request, lihat ADR-013)

**Prinsip pembagian (LOCKED):** semua manajemen teman (add-request, accept/reject, lihat presence, hapus koneksi) terjadi di **WebView** (2D, di luar AR). Semua yang spasial (lihat posisi, jarak, navigasi ke teman) terjadi di **dalam AR** (Unity). Alasan: mengikuti panduan resmi Google ARCore — hindari pop-up/full-screen takeover di dalam sesi AR, AR harus tetap jadi "magic lens" bukan tempat mengisi form. Divalidasi lewat riset, bukan preferensi teknis semata.

### 5a. Kelola friendlist (WebView, TANPA kamera aktif)

```
Home (WebView) → tap quick action "Cari Teman"
  → panel Friendlist (WebView, 2D): [Daftar Teman] [Tambah Teman] [Request Masuk]

Tambah Teman:
  → user A input identifier PERSIS milik B (username/ID/QR — BUKAN cari/browse direktori)
  → POST /api/friends/request { identifier } → backend catat status 'pending'
  → B melihat request masuk di panel "Request Masuk"

Request Masuk (sisi B):
  → POST /api/friends/respond { requestId, action: 'accept' | 'reject' }
  → jika accept → koneksi 'accepted' tersimpan permanen di kedua sisi (connectionId)
  → jika reject → request dibuang, A tidak diberi tahu alasan spesifik

Daftar Teman:
  → GET /api/friends → list koneksi accepted + presence status-only
      (online / ar-active / offline — TANPA gedung/lantai/posisi)
  → tombol "Navigasi ke [nama]" AKTIF hanya kalau status teman = ar-active
```

### 5b. Lihat posisi & navigasi ke teman (Unity, AR only — murni visual)

```
WebView: postMessage({ action: 'launchAR', mode: 'findFriend', connectionId })
  → Flutter → Unity boot AR + localize
  → localize sukses → render dot posisi teman + jarak real-time
  → tombol "Navigasi ke [nama]" → arrow + path (sama seperti navigasi lokasi biasa)
  → TIDAK ADA input/keyboard/modal di dalam AR — murni visual, tanpa full-screen takeover
```

### Lapisan keamanan (berlaku di semua tahap, non-negotiable)

- Add-friend **hanya** by exact identifier — **TIDAK ADA** direktori/search user yang bisa di-browse
- Koneksi butuh **mutual accept** — request tidak otomatis jadi teman
- Rate limit pengiriman friend-request (cegah spam ke banyak identifier)
- Block/report tersedia di sisi penerima
- Presence hanya terlihat oleh koneksi `accepted`, dan status-only — tidak pernah gedung/lantai/posisi
- User bisa opt-out (tampil offline ke semua orang)
- Posisi live di AR auto-terminate begitu salah satu pihak menutup sesi AR — tidak ada cache posisi setelah sesi berakhir
- **TIDAK ADA daftar "orang di sekitar yang terdeteksi otomatis" di manapun** — prinsip inti tetap dari ADR-010, sekarang diimplementasikan lewat friendlist (ADR-013)

## 6. Return Flow

```
User selesai (arrived / tap back di AR) 
  → Unity kirim signal ke Flutter (AR session closed)
  → Flutter tutup Unity, kembali ke DarsiNavigationScreen
  → WebView RESUME (state Home/Cari Lokasi terakhir, TIDAK reload dari awal)
  → back button di AppBar native → kembali ke MyRSIy Home
```

## Ringkasan: screen yang ADA vs yang DIHAPUS dari scope

| Screen | Status | Keterangan |
|---|---|---|
| Home (WebView) | ✅ Ada | Tanpa jarak meter |
| Cari Lokasi (WebView) | ✅ Ada | Tanpa jarak meter |
| Peta 2D (WebView) | ❌ Dihapus | ADR-006 |
| Deteksi Lokasi / scan gate (WebView) | ❌ Dihapus | ADR-008 |
| Splash / Auth / Login / Register / Home (Unity UI Toolkit) | ❌ Dihapus dari Unity | ADR-003 — sudah di-cleanup dari repo |
| AR Navigation (Unity) | ✅ Ada | Active, Reroute, Arrived states |
| Cari Teman (Unity, render posisi teman) | ✅ Ada (baru) | AR-only, friendlist di WebView, lihat flow #5 & ADR-013 |
| Voice Input (Unity uGUI) | ✅ Ada (existing) | Tidak berubah |
