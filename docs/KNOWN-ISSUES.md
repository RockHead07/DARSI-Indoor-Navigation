# Known Issues — DARSI Indoor Navigation (Unity)

Dokumen pelacak bug yang sudah pernah muncul, sudah diinvestigasi, dan berpotensi muncul
lagi di masa depan. Tujuannya supaya investigasi ulang tidak mulai dari nol.

---

## 🔴 Tombol mic ditekan, panel suara tidak muncul, malah "Localizing..."

**Status:** Kemungkinan besar SUDAH TERATASI (2026-08-18), dikonfirmasi tidak langsung —
voice input berhasil sampai tahap processing di tes berikutnya. Belum ada konfirmasi
bersih eksplisit ("panel muncul normal, tidak ada localize ikut nongol").

### Gejala

Tekan tombol mic di HUD AR → panel "Mendengarkan..." tidak pernah tampil, layar malah
stutter ~1 detik lalu overlay "Localizing..." MultiSet muncul (persis seperti relocalize
manual). Terjadi konsisten di device (APK), termasuk build baru dengan nama berbeda.

### Akar masalah yang TERKONFIRMASI

**`android.permission.RECORD_AUDIO` tidak pernah dideklarasikan** di
[`Assets/Plugins/Android/AndroidManifest.xml`](../Assets/Plugins/Android/AndroidManifest.xml)
— manifest cuma punya `CAMERA` dan `INTERNET`. Android tidak bisa memberi izin untuk
permission yang tidak dideklarasikan, jadi `Permission.HasUserAuthorizedPermission(Permission.Microphone)`
di [`VoiceInputHandler.StartListening()`](../Assets/Speech%20Recognition/VoiceInputHandler.cs)
selalu `false` → method berhenti di awal, `voiceUI.ShowPanel()` **tidak pernah kepanggil**.

**Perbaikan:** tambah `<uses-permission android:name="android.permission.RECORD_AUDIO" />`
ke manifest. Butuh build ulang (perubahan manifest tidak berlaku ke APK lama).

**Catatan:** bagian "malah muncul Localizing" kemungkinan cuma kebetulan waktu dengan
`backgroundLocalization: 1` (MultiSet relocalize otomatis berkala), bukan disebabkan
langsung oleh klik tombolnya — ini BELUM diverifikasi terpisah dari akar masalah utama.

### Jalan buntu yang SUDAH dicoba dan TERBUKTI BUKAN penyebabnya

Supaya tidak diulang kalau bug ini muncul lagi dalam bentuk berbeda:

1. **Listener `OnClick()` dangling di tombol mic** — ditemukan referensi GUID
   (`490dec563256e4c4493cb6574d3d583e`) yang tidak ada di manapun di project. Ini memang
   sampah/rusak, tapi listener dangling di Unity di-skip diam-diam saat diklik — tidak
   menyebabkan apapun terjadi, apalagi memicu localize. Bukan penyebab.
2. **`FriendListPanel`/`VoicePanel` ke-serialize aktif (`m_IsActive: 1`) di scene** —
   awalnya diduga ini bikin `ExclusivePanel.AnyOpen` macet `true` sejak scene load,
   sehingga `HideWhilePanelOpen` bikin tombol mic transparan-klik selamanya. **Ternyata
   salah** — kedua panel itu SENGAJA didesain start aktif lalu `Awake()` masing-masing
   men-nonaktifkan diri sendiri sendiri (`startHidden` flag di `VoiceUIController`,
   `panelRoot.SetActive(false)` di `FriendListPanel`) dalam frame yang sama. Mematikan
   `m_IsActive` langsung dari file scene MEMOTONG mekanisme ini — `Awake()` jadi tidak
   pernah jalan sampai sesuatu memanggil `ShowPanel()`/toggle manual, menyebabkan
   `Coroutine couldn't be started because the game object 'VoicePanel' is inactive!`
   di `VoiceUIController.SetTranscript()`. **Jangan ubah `m_IsActive` panel-panel ini
   langsung dari file scene** — kalau perlu diubah, ubah logic `Awake()`/`startHidden`
   di scriptnya, bukan raw serialized state.

### Kalau muncul lagi, cek urutan ini

1. `Assets/Plugins/Android/AndroidManifest.xml` — pastikan `RECORD_AUDIO` masih ada
   (bisa hilang lagi kalau file di-regenerate/di-merge ulang oleh tooling Android).
2. Di Android Settings device → App Info → DARSI → Permissions → pastikan
   Microphone benar-benar `Allowed` (bukan cuma "Ask every time" yang belum dijawab).
3. Baru setelah itu curigai wiring UI (`ExclusivePanel`/`HideWhilePanelOpen`) — dan kalau
   iya, JANGAN ulangi kesalahan di atas soal `m_IsActive`.

---

## 🟡 Fallback Ollama bisa bikin voice input terasa macet lama

**Status:** Fix diterapkan (2026-08-18), **belum di-build/dites**.

### Gejala

Setelah bicara, "Processing..." bisa menggantung sampai ~1 menit sebelum akhirnya
muncul hasil/error — terjadi kalau Groq (provider utama) gagal, lalu fallback ke Ollama
lokal yang alamatnya hardcoded ke IP WiFi rumah (`192.168.18.150`), tidak kejangkau dari
jaringan lain (mis. lapangan/kampus).

### Perbaikan

[`OllamaConnector.cs`](../Assets/Speech%20Recognition/OllamaConnector.cs) — `TryOllama()`:
timeout dipangkas 30 → 8 detik, percobaan dipangkas 2 → 1 (retry ke IP yang sama tidak
membantu kalau memang beda jaringan). **Perlu build baru untuk verifikasi.**

---

## 🟡 Endpoint Backend / Cloudflare Tunnel Mati (`DNS_PROBE_FINISHED_NXDOMAIN`)

**Status:** Teridentifikasi & Solusi Terdokumentasi (2026-08-22).

### Gejala
Browser / Unity menampilkan `DNS_PROBE_FINISHED_NXDOMAIN` saat mengakses URL `*.trycloudflare.com/api/...` setelah sesi SSH server ditutup.

### Akar Masalah
Quick Tunnel `cloudflared` mati saat SSH ditutup (SIGHUP) dan Cloudflare menghapus domain sementara tersebut.

### Solusi & Runbook Lengkap
Gunakan `tmux` agar proses tunnel tetap detached di background, atau gunakan Cloudflare Zero Trust Named Tunnel untuk URL permanen.
Panduan lengkap: [`docs/BACKEND-SERVER-OPERATIONS.md`](BACKEND-SERVER-OPERATIONS.md).


---

## 🟡 `SECURITY.md` di root: kebijakan wajib yang ~85% tidak berlaku di repo ini

**Status:** BELUM DIPERBAIKI (ditemukan 2026-08-25). File masih untracked, belum pernah
ikut commit apa pun. Keputusan penanganannya milik pemilik project.

### Gejala

`SECURITY.md` (1538 baris) menyatakan dirinya **"Status: Mandatory"** dan menutup dengan
*"Any implementation that conflicts with this document MUST be treated as a security
concern."* Padahal repo ini melanggar puluhan klausanya secara sepele.

### Kenapa ini masalah, bukan sekadar dokumen berlebih

Isinya kebijakan keamanan aplikasi web generik: RLS, IDOR, mass assignment, XSS, upload
file, cookie sesi, hashing Argon2id, CSP header, SBOM, container scanning, isolasi
multi-tenant. Repo ini **Unity AR client** — tidak ada server, database, autentikasi,
akun pengguna, HTML, maupun tenancy. Bagian yang benar-benar relevan berlaku di repo
**`darsi-backend`**, bukan di sini.

Nama filenya juga tidak sesuai konvensi: GitHub memperlakukan `SECURITY.md` sebagai
**kebijakan pelaporan kerentanan** (muncul di tab Security). Dokumen ini tidak punya
kontak, proses disclosure, maupun supported versions.

**Dampak sesungguhnya:** pembaca pertama langsung belajar bahwa dokumen bertanda "wajib"
di repo ini boleh diabaikan. Itu pola yang sudah tiga kali menggigit project ini —
ADR-028 (klaim perbaikan keselamatan yang masih gagal), ADR-031 (test gate berjalan tanpa
satupun test), dan `AvatarSafetyFade` (mengaku "Protokol Keselamatan" padahal tidak pernah
bisa menyala). Menambah satu dokumen wajib yang tidak ditegakkan memperkuat kebiasaan itu.

### Risiko keamanan NYATA repo ini yang tidak disebut sama sekali

1. `groqApiKey` pernah **benar-benar ter-serialize ke scene yang di-track git** (ADR-024).
2. Pola `groq-api-key.local.txt` (gitignored) sebagai satu-satunya jalur kunci yang sah.
3. `POI_SYNC_TOKEN`.
4. Model privasi Cari Teman: wajib friend-request + mutual accept, **dilarang**
   auto-discovery (ADR-010, ADR-013).
5. Data lokasi pasien di lingkungan rumah sakit.
6. Data apa saja yang menyeberangi batas UaaL dari Flutter ke Unity (`INTEGRATION.md`).

### Perbaikan yang disarankan (pecah per tujuan, jangan satu file raksasa)

| Tujuan | Tempat yang benar |
|---|---|
| Aturan perilaku agent AI | `CLAUDE.md` (sudah ada dan dipatuhi) atau `.agents/rules/` |
| Keamanan RAG/LLM/API | repo `darsi-backend`, menyebut endpoint dan tabelnya sendiri |
| Postur keamanan DARSI | dokumen pendek di sini, isinya 6 risiko nyata di atas |
| Pelaporan kerentanan | `SECURITY.md` beberapa baris: kontak, proses, versi didukung |

Bagian yang **layak diselamatkan** dan dipindah ke `darsi-backend`: §5 (prompt injection),
§5.4 (RAG security), §37 (LLM output validation), §38 (tool parameter validation). Itu
relevan nyata karena asisten DARSI menerima input suara dan menarik chunk dari corpus.

---

## ✅ SELESAI — Animasi avatar dulu diambil dari aset demo Photon

**Status:** **SELESAI (2026-08-25).** Seluruh klip sudah diganti ke Mixamo dan
`AvatarGuide.controller` terverifikasi **nol rujukan** ke `Assets/Photon/`. Dicatat di sini
sebagai jejak, bukan sebagai utang. Riwayat masalahnya di bawah.

### Apa yang dipakai

`AvatarGuide.controller` (BlendTree Locomotion) memakai dua klip:

```
Assets/Photon/PhotonUnityNetworking/Demos/Shared Assets/Animations/HumanoidWalk.fbx
  -> HumanoidIdle  (BlendTree @ 0)
  -> HumanoidWalk  (BlendTree @ 1.4)
```

Klip itu dipilih karena **sudah ada di project** dan bertipe Humanoid, sehingga otomatis
ter-retarget ke rig VRM tanpa perlu aset baru. Ditemukan lewat
`AssetDatabase.FindAssets("t:AnimationClip")` yang disaring `isHumanMotion`.

### Kenapa ini masalah

Aset di folder `Demos/` umumnya dilisensikan untuk **mempelajari SDK-nya**, bukan untuk
didistribusikan sebagai bagian dari produk. DARSI menyasar deployment di RS Islam A. Yani
dan menjadi bagian pengajuan paten DJKI, jadi provenance aset bukan detail administratif.

**Isi lisensi Photon-nya belum dibaca**, jadi status sebenarnya belum diketahui: bisa saja
diizinkan. Yang pasti adalah **belum diverifikasi**, dan aset itu sudah masuk repo.

Kekeliruan prosesnya: pemilihan aset ini tidak ditandai saat dipilih, padahal
saat itulah pertanyaan lisensi paling murah dijawab.

### Pilihan pengganti (semuanya gratis, semuanya humanoid)

| Sumber | Catatan |
|---|---|
| **Mixamo** (Adobe) | Paling umum. Gratis dengan akun, boleh untuk proyek komersial. Idle, walk, wave, dan point tersedia sekaligus |
| **Unity Starter Assets** | Gratis di Asset Store, sudah termasuk set locomotion |
| Rekam sendiri | Paling aman secara hukum, paling mahal secara waktu |

Rekomendasi: **Mixamo**, karena satu sumber menutup seluruh kebutuhan gestur ADR-034
(Idle, Walk, Wave, Point) dan formatnya humanoid sehingga tidak ada kerja retarget tambahan.

### Yang harus dikerjakan

1. Baca lisensi Photon (`Assets/Photon/PhotonUnityNetworking/readme.txt` dan halaman
   lisensi resmi Photon) untuk memastikan status aset `Demos/`.
2. Kalau tidak mengizinkan distribusi, ganti klip di `AvatarGuide.controller`. Karena
   BlendTree-nya sudah terpasang, penggantian hanya menukar dua `AnimationClip` dan
   menyesuaikan ambang kecepatan.
3. Catat sumber dan lisensi aset final di dokumen, supaya laporan KP dan berkas paten
   punya jejak provenance yang jelas.
