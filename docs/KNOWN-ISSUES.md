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

---

## 📌 Serah terima kerja avatar (2026-08-25) — halangan & jebakan yang sudah terbukti

Ditulis di akhir sesi panjang, supaya sesi berikutnya tidak mengulang penemuan yang sama.
**Baca ADR-034 + Amandemen 034-A dulu**; bagian ini hanya memuat yang TIDAK masuk ADR.

### Halangan yang belum bisa diselesaikan dari Editor

| Halangan | Kenapa terhenti |
|---|---|
| Serah terima lift (ADR-034 keputusan 9) | `AwaitingRelocalize` hanya muncul saat localize benar-benar putus lalu pulih. Mustahil dipicu di Editor |
| NavMesh saat re-localize | Butuh MultiSet me-reposisi content root di device |
| Model ≤ 15.000 tris (Fase 1) | Menunggu model RS sungguhan, bukan pekerjaan kode |
| RAG lewat mic asli | Utang lama, seluruh verifikasi masih Editor Play mode |

### ⚠️ `TestingHCM` TIDAK mewakili produksi untuk urusan lintas lantai

Terukur dua kali (2026-08-24 dan 2026-08-25), rute `[Ground] Parkir Mobil` → `[Lantai1] IGD`:

| Scene | Hasil |
|---|---|
| `DARSi-Indoor Navigation` (produksi) | `PathPartial`, 2 corner, 0,90 m |
| `TestingHCM` | **`PathComplete`, 17 corner**, naik 4,21 m |

Lantai di `TestingHCM` **tersambung di navmesh** (lompatan Y 2,04 m, tanpa OffMeshLink),
bake-nya kemungkinan mendahului amandemen 020-B. Konsekuensinya `TestingHCM` akan
**meluluskan uji yang gagal di produksi**. Sah untuk lead-follow dalam satu lantai; haram
untuk menyimpulkan apa pun soal lintas lantai.

**Jebakan turunannya:** siapa pun yang melihat `PathPartial` di produksi akan mengira
NavMesh rusak lalu tergoda memasang `NavMeshLink`. Itu mencabut 020-B diam-diam dan
menghidupkan lagi garis rute menembus plafon. Sudah dicatat di "Yang Ditolak" ADR-034.

### Jebakan alat yang memakan waktu nyata sesi ini

1. **Coplay timeout ≠ Unity mati.** Semua tool `mcp__coplay-mcp__*` timeout tanpa pesan
   kalau `set_unity_project_root` belum dipanggil, dan root-nya bisa ter-reset di tengah
   sesi. Refleks: panggil ulang set-root, baru simpulkan.
2. **Jangan `git restore` aset Unity selagi Editor membukanya.** Salinan di memori jadi
   invalid sementara file di disk sehat. Sesi ini sempat menyimpulkan "NavMesh rusak"
   padahal hanya perlu memuat ulang scene. NavMesh-nya utuh: 4.278 vertex, 11/11 POI.
3. **Membangun ulang aset mengubah GUID-nya** dan memutus semua referensi tanpa error.
   `AvatarGuide.controller` dibangun ulang → `Animator` di prefab DAN scene jadi `NULL`.
   Selalu verifikasi referensi setelah `DeleteAsset` + `CreateAsset`.
4. **Reimport FBX memutus referensi BlendTree.** Slot `Walk` jadi `NULL` tanpa error dan
   avatar meluncur dalam pose Idle. Periksa slot kosong setelah `SaveAndReimport()`.
5. **Unity crash OOM** setelah puluhan `execute_script` beruntun (tiap panggilan memicu
   compile + domain reload). Gabungkan operasi jadi satu script.

### Kegagalan senyap: pola yang paling sering menggigit

Semua bug mahal sesi ini nol error dan nol warning:

* `AvatarSafetyFade.CacheRenderers()` ter-guard `Length == 0`, memudarkan renderer dummy
* `AddComponent` MonoBehaviour dari assembly `Editor/` ditolak saat Play mode
* Pencocokan nama file eksak melewatkan `Walk.fbx` saat `Walking.fbx` diganti
* Slot BlendTree `NULL` setelah reimport
* `_currentWeight` ditulis dari dua tempat, saling tarik, kepala mentok 19,7 dari 55°

**Yang membongkar semuanya bukan penalaran, tapi mengukur satu angka konkret lalu
membandingkannya dengan angka yang diharapkan.** Contoh paling jelas: kepala butuh 55°
tapi hanya berputar 6,5°, dan `deltaTime * lookSpeed = 0,1` — 10% dari 55 adalah 5,5.
Kecocokan angka itu yang menunjuk akar masalahnya, bukan membaca kode.

### Duplikasi yang disadari dan belum diselesaikan

Angka **1,589** (kecepatan asli klip `Walk`) ada di dua tempat: threshold BlendTree
`Locomotion` dan field `walkClipSpeed` di `AIAvatarGuideController`. Tooltip menandai
keduanya wajib sama. **Kalau klip `Walk` diganti, DUA-DUANYA harus disetel ulang** ke
`clip.averageSpeed` klip baru, kalau tidak kaki akan selip lagi.

### Perancah yang harus dihapus setelah lead-follow tervalidasi di device

```
Assets/Scripts/Avatar/Editor/GuideWalkProbe.cs     (Editor-only, aman)
Assets/Scripts/Avatar/Editor/SafetyFadeProbe.cs    (Editor-only, aman)
Assets/Scripts/Avatar/ProbeUserWalker.cs           <- IKUT TER-BUILD
Assets/Scripts/Avatar/SimpleSandboxFreeCam.cs      <- IKUT TER-BUILD
```

Dua terakhir ada di folder runtime sehingga ikut ter-compile ke APK. `ProbeUserWalker`
sengaja ditaruh di sana karena Unity menolak `AddComponent` untuk MonoBehaviour dari
assembly editor-only saat Play mode. Tidak berbahaya selama tidak terpasang di GameObject
mana pun, tapi tetap memperbesar build.

### Menggantung di working tree, bukan dari pekerjaan avatar

* `SECURITY.md` — untracked, dari sesi lain. Analisisnya sudah dicatat di bagian atas
  berkas ini (kebijakan wajib yang ~85% tidak berlaku di repo Unity ini).
* `TestingHCM` juga aktif di Build Settings, jadi scene testing ikut masuk APK. Tidak
  melanggar aturan tercatat, tapi memperbesar build.
