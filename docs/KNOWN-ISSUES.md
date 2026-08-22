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

