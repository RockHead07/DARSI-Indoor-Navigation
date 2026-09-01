# Tes Lapangan Avatar Suara dan Lip-Sync - DARSI

> Checklist bawa-ke-lokasi, format sama seperti `FIELD-TEST-T5.3.md` dan
> `FIELD-TEST-RAG-ASSISTANT.md`. Menguji **avatar VRM berbicara dengan lip-sync
> tersinkron, lalu memimpin rute (lead-follow)** - pipeline lengkap dari mikrofon HP,
> melewati backend RAG + TTS, sampai avatar berjalan di atas rute AR sambil bicara.

**ADR terkait:** [ADR-034](DECISIONS.md#adr-034--model-penempatan-lead-follow-dan-safety-fade-avatar-companion-2026-08-26) (lead-follow dan safety fade),
[ADR-037](DECISIONS.md#adr-037--pemilihan-engine-lip-sync-hecomiulipsync-berbasis-mfcc-dan-burst-compiler-untuk-avatar-3d-vrm-2026-08-31) (engine lip-sync uLipSync),
[ADR-038](DECISIONS.md#adr-038-integrasi-klien-suara-avatar-avataraudioclient-isolasi-kegagalan-tts-dan-urutan-fsm-lead-follow-2026-09-01) (integrasi AvatarAudioClient, isolasi kegagalan TTS, urutan FSM)

**Tanggal rencana:** ___
**Tanggal aktual:** ___
**Lokasi:** RSI Ahmad Yani Surabaya
**Build SHA:** ___ (output `git rev-parse --short HEAD` dari build yang di-install)
**Device:** ___ (model HP, versi Android)

---

## Klarifikasi batasan: yang SUDAH vs yang BELUM terverifikasi

### Sudah terverifikasi (Play Mode / Editor, bukan klaim di kertas):
- **Lip-sync driver** (`AvatarSpeechLipSync`) menggerakkan 5 viseme (A, I, U, E, O) secara
  tersinkron terhadap audio via uLipSync MFCC Burst. 4/4 unit test EditMode lulus,
  probe PlayMode merekam ribuan frame dengan reset pasca-audio ke 0.000 sempurna
  ([ADR-037](DECISIONS.md#adr-037--pemilihan-engine-lip-sync-hecomiulipsync-berbasis-mfcc-dan-burst-compiler-untuk-avatar-3d-vrm-2026-08-31)).
- **Pemutaran audio TTS asli dari backend** (`edge-tts id-ID-GadisNeural`) melalui
  `AvatarAudioClient.SpeakText` berhasil di scene sandbox.
- **Isolasi kegagalan `/tts`:** endpoint offline/error ditangani aman (0 unhandled
  exception), callback navigasi langsung terpanggil seketika, teks jawaban tetap utuh.
- **Pemicu `AIAvatarGuideController.StartLeading()`:** transisi FSM dari `IdleStand` ke
  `LeadingPath` terbukti terpanggil pasca-bicara di probe PlayMode.
- 5/5 unit test EditMode `AvatarAudioClientTests.cs` lulus, 3/3 skenario probe integrasi
  lulus ([ADR-038](DECISIONS.md#adr-038-integrasi-klien-suara-avatar-avataraudioclient-isolasi-kegagalan-tts-dan-urutan-fsm-lead-follow-2026-09-01)).

### Yang HANYA bisa dijawab di lapangan (tujuan dokumen ini):
1. Apakah avatar **benar-benar berjalan di atas rute nyata** (ShowPath/NavigationController)
   sambil bicara, bukan cuma animasi jalan di tempat (sandbox tidak punya rute).
2. Apakah **urutan bicara-lalu-jalan** (ADR-038) tetap benar saat data audio datang dari
   jaringan sungguhan (latensi/jitter nyata, bukan loopback).
3. Apakah **lip-sync terlihat meyakinkan** di layar HP (resolusi, jarak pandang, frame rate)
   vs di Editor.
4. Apakah **beban gabungan** VRM + lip-sync Burst + Animator + ARCore + VPS MultiSet tidak
   menjatuhkan FPS di bawah 30 atau membuat HP terlalu panas.
5. Apakah **isolasi kegagalan TTS** berfungsi di kegagalan jaringan nyata (bukan port mati
   simulasi).
6. Berapa **latensi total** dari selesai bicara sampai jawaban bersuara mulai terdengar, di
   atas latensi Bifrost 12-32 detik yang sudah diketahui.

---

## 0. Prasyarat - selesaikan SEBELUM berangkat

Komponen Voice/Lip-Sync selama ini hanya ada di `Sandbox_AvatarCompanion.unity` (scene
terisolasi tanpa VPS/MultiSet). Untuk tes lapangan, komponen tersebut harus terpasang
di scene navigasi sungguhan.

### Checklist prasyarat:

- [ ] **Jalankan menu Editor `DARSI > Avatar > Wire Voice to TestingHCM`** di Unity Editor.
      Skrip ini menambahkan `AudioSource`, `uLipSync`, `AvatarSpeechLipSync`, dan
      `AvatarAudioClient` ke GameObject `Avatar_Guide` yang sudah ada di `TestingHCM.unity`,
      lalu menghubungkan semua referensi serialized dan menyimpan scene.
      Skrip: [`WireAvatarVoiceToTestingHCM.cs`](../Assets/Scripts/Avatar/Editor/WireAvatarVoiceToTestingHCM.cs)
- [ ] **Verifikasi wiring di Inspector** setelah skrip jalan:
      - `Avatar_Guide` sekarang memiliki: `AudioSource`, `uLipSync`, `AvatarSpeechLipSync`,
        `AvatarAudioClient` (selain `AIAvatarGuideController`, `AvatarSafetyFade`,
        `AvatarLookAtController`, `AvatarGuideNavigationBridge` yang sudah ada).
      - `AvatarAudioClient.baseUrl` = `https://api-darsi.rockhead07.tech`
      - `AvatarAudioClient.guideController` menunjuk ke `AIAvatarGuideController` di
        `Avatar_Guide` (bukan null).
      - `AvatarSpeechLipSync.blendShapeProxy` menunjuk ke `VRMBlendShapeProxy` di child
        model VRM (bukan null).
      - `AIAvatarGuideController.showPath` sudah terhubung ke `ShowPath` (sudah ada
        sebelumnya, bukan bagian dari skrip baru).
- [ ] **Konfirmasi `AssistantClient.baseUrl`** di `AssistantManager` GameObject =
      `https://api-darsi.rockhead07.tech` (sudah benar dari sesi sebelumnya, cek ulang saja).
- [ ] **Build APK** dari TestingHCM scene (pastikan scene ini ada di Build Settings).
- [ ] **Install dan uji buka-tutup di rumah** - pastikan APK terpasang, tidak crash saat
      dibuka.

---

## 1. Sebelum berangkat (setelah prasyarat 0 selesai)

- [ ] Build APK terbaru terpasang di HP, sudah dites buka-tutup di rumah
- [ ] Server produksi hidup: `curl https://api-darsi.rockhead07.tech/health` mendapatkan `{"ok":true}`
- [ ] HP terisi penuh + powerbank dibawa: audio TTS + lip-sync + AR + VPS sekaligus boros baterai
- [ ] Headphone/earphone dibawa: speaker HP di koridor RS ramai bisa bikin sulit menilai
      kejelasan suara asisten
- [ ] Laptop + kabel USB untuk logcat
- [ ] Uji baik WiFi lokasi MAUPUN data seluler (sama seperti `FIELD-TEST-RAG-ASSISTANT.md` S5):
      TTS juga lewat Bifrost/Groq/edge-tts, ikut kena soal jaringan yang sama

**Etika di lokasi.** Sama seperti dua dokumen field-test sebelumnya: ini rumah sakit
sungguhan. Avatar yang bicara + berjalan bisa menarik perhatian pasien/staf. Uji di
area yang tidak mengganggu jalur IGD/brankar, dan berhenti kalau diminta petugas.

---

## 2. Rekaman log

```bash
adb logcat -G 16M
adb logcat -c
# ... tes keliling ...
adb logcat -d > darsi-avatar-fieldtest-YYYYMMDD.log
```

**Tag penting yang dicari:**
- `AvatarAudioClient` - permintaan TTS, unduh audio, pemutaran, kegagalan
- `AvatarSpeechLipSync` - aktivasi lip-sync, deteksi fonem, fallback RMS
- `AIAvatarGuideController` - transisi state FSM (IdleStand, LeadingPath, WaitingForUser, ArrivalPointing)
- `[Voice]` - input suara pengguna
- `[Assistant]` - permintaan ke backend RAG

**Temuan kritis yang harus ditandai:**
- Baris yang menyebut fallback TTS (edge-tts gagal di lapangan, Sherpa-ONNX/Piper aktif)
- `StartLeading` yang tidak pernah muncul (lead-follow gagal terpicu)
- Error/exception apa pun yang menyebut nama komponen di atas

---

## 3. Skenario, urut prioritas

### S1 - Baseline: bicara lalu berjalan (WAJIB)

Ucapkan pertanyaan sederhana yang punya tujuan navigasi ("Toilet dimana" atau "Farmasi
dimana") lewat mikrofon.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Avatar bicara (audio terdengar dari HP) | ya | ___ |
| Mulut avatar bergerak mengikuti suara (bukan diam/statis) | ya | ___ |
| Avatar TIDAK mulai berjalan SEBELUM selesai bicara | ya - ini urutan yang dikunci ADR-038 keputusan 3 | ___ |
| Setelah selesai bicara, avatar mulai memimpin rute | ya - transisi FSM IdleStand ke LeadingPath | ___ |
| Rute yang diikuti avatar benar menuju tujuan (bukan cuma animasi jalan di tempat) | ya - ShowPath.path terbaca, avatar menyusuri polyline | ___ |
| Garis rute di lantai dan arah avatar konsisten (avatar tidak belok sendiri) | ya - ADR-034 keputusan 2: sumber rute = LineRenderer ShowPath | ___ |
| Safety fade aktif saat kamera mendekat < 0.9 m | ya - avatar memudar/menghilang, pandangan AR tidak terhalang | ___ |

**Kalau avatar mulai jalan SEBELUM selesai bicara**, atau bicara tapi TIDAK PERNAH
mulai jalan: itu regresi dari urutan FSM yang sudah dibuktikan di sandbox. Catat
persis kapan itu terjadi dan kondisinya (jaringan, pertanyaan apa).

**Kalau avatar jalan tapi tidak mengikuti garis rute** (jalan di tempat, atau arah
berbeda dari ShowPath): kemungkinan `_line` (LineRenderer) tidak terbaca di runtime.
Cek logcat untuk pesan dari `AIAvatarGuideController`.

### S2 - Isolasi kegagalan TTS di lapangan (PENTING)

Sudah terbukti di sandbox lewat port mati simulasi. Di lapangan, kegagalan lebih
mungkin datang dari jaringan asli.

**Cara memicu:** matikan data/WiFi sesaat SETELAH mengajukan pertanyaan (sehingga
`/query` RAG berhasil tapi `/tts` gagal), atau uji di area sinyal lemah.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Jawaban teks asisten tetap tampil di layar walau suara gagal | ya - ADR-033 Amandemen 033-A poin 1 | ___ |
| Navigasi (rute di lantai) tetap menyala walau suara gagal | ya - callback `onNavigationReady` tetap terpanggil | ___ |
| Avatar tetap mulai memimpin rute walau tanpa suara | ya - `StartLeading()` dipanggil di fallback | ___ |
| Tidak ada crash/freeze/ANR | ya | ___ |

### S3 - Latensi yang benar-benar dirasakan (PENTING)

Ucapkan pertanyaan, ukur dari selesai bicara sampai avatar mulai menjawab dengan suara
(bukan cuma teks muncul - TTS-nya sendiri butuh waktu sintesis di atas latensi Bifrost
12-32 detik yang sudah diketahui dari `FIELD-TEST-RAG-ASSISTANT.md`).

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Waktu tunggu: selesai bicara pengguna sampai teks jawaban muncul | dicatat (baseline RAG sudah diketahui) | ___ detik |
| Waktu tunggu: teks jawaban muncul sampai suara avatar mulai terdengar | dicatat (ini latensi sintesis + unduh TTS) | ___ detik |
| Total waktu tunggu: selesai bicara pengguna sampai jawaban bersuara mulai | dicatat, belum ada angka lapangan sebelumnya | ___ detik |
| Ada indikasi visual "sedang berpikir/menyiapkan suara" selama menunggu | kalau tidak ada, ini gap UX nyata | ada/tidak: ___ |

**Catatan:** jika total latensi terlalu lama untuk UX nyata (gabungan Bifrost + sintesis
TTS), itu temuan untuk revisit keputusan hybrid tier ADR-033, bukan bug kode.

### S4 - Frame rate dan panas HP (KALAU SEMPAT)

Avatar (VRM + lip-sync Burst + Animator) berjalan bersamaan dengan ARCore + VPS
MultiSet. Ini pertama kalinya beban gabungan ini diukur di device fisik.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| FPS terasa stabil (tidak patah-patah) saat avatar bicara+jalan bersamaan | ya | ___ |
| FPS terasa stabil saat avatar HANYA bicara (belum jalan) | ya | ___ |
| HP terasa panas berlebih setelah ~10 menit pemakaian terus-menerus | tidak | ___ |
| GC spike terasa (micro-stutter periodik) | tidak - uLipSync Burst seharusnya zero GC | ___ |

**Opsional jika bisa:** screenshot GPU Profiler dari logcat atau Android GPU Inspector.

### S5 - Uji jaringan: WiFi vs data seluler (KALAU SEMPAT)

Ulangi S1 di **kedua** jenis jaringan. TTS menggunakan endpoint yang sama dengan RAG
(`https://api-darsi.rockhead07.tech`), jadi patut memastikan keduanya bekerja.

| Kondisi jaringan | S1 lulus? | Latensi S3 | Catatan |
|---|---|---|---|
| WiFi RS | ___ | ___ detik | ___ |
| Data seluler | ___ | ___ detik | ___ |

### S6 - Transisi lantai dengan suara (BONUS, kalau situasi memungkinkan)

Ucapkan pertanyaan ke tujuan di lantai BERBEDA dari posisi pengguna. Ini menguji
koordinasi ADR-034 keputusan 9 (avatar disembunyikan selama `AwaitingRelocalize`,
muncul kembali setelah re-localize berhasil di lantai tujuan).

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Avatar bicara dan memimpin ke lift (segmen lantai saat ini) | ya | ___ |
| Avatar menghilang saat pengguna masuk lift / pindah lantai | ya - fase AwaitingRelocalize | ___ |
| Avatar muncul kembali setelah re-localize di lantai tujuan | ya - _needsSnap = true, snap ke rute baru | ___ |
| Avatar melanjutkan memimpin ke tujuan akhir di lantai baru | ya | ___ |

---

## 4. Sesudah pulang

- [ ] Simpan log logcat, beri nama bertanggal (`darsi-avatar-fieldtest-YYYYMMDD.log`)
- [ ] Isi seluruh tabel di atas, commit dokumen ini terisi
- [ ] **Prioritas tindak lanjut berdasarkan hasil:**

| Hasil | Prioritas |
|---|---|
| S1 gagal (urutan bicara-jalan terbalik atau lead-follow tidak terpicu) | **P0** - regresi FSM, sesi berikutnya paling utama |
| S1 gagal (avatar jalan tapi tidak mengikuti rute ShowPath) | **P0** - wiring LineRenderer ke AIAvatarGuideController |
| S2 gagal (crash saat TTS gagal) | **P1** - regresi isolasi kegagalan |
| S3 latensi total terlalu lama (> 45 detik) | **P2** - revisit keputusan hybrid tier ADR-033 |
| S4 FPS jeblok atau HP terlalu panas | **P2** - profiling Burst/VRM di device |
| S6 transisi lantai gagal | **P3** - perlu scene produksi multi-lantai yang sudah di-bake |

- [ ] Kalau ada temuan yang memerlukan amandemen ADR, catat di `docs/DECISIONS.md`

---

## Catatan penting

Ini pengujian **PERTAMA** untuk seluruh Fase 2 (suara + lip-sync + koordinasi lead-follow)
lewat perangkat fisik. Semua yang "terverifikasi" di ADR-037/038 itu lewat sandbox
terisolasi (`Sandbox_AvatarCompanion.unity`) yang **tidak punya SDK MultiSet/VPS sama
sekali** (nol referensi `MultiSetSdk`/`NavigationController`). Device fisik + scene
navigasi sungguhan + lingkungan RS nyata adalah tiga lapisan yang belum pernah diuji
bersamaan sama sekali.

Hasil tes ini menentukan apakah gate rilis ADR-034 keputusan 7 ("lead-follow TIDAK BOLEH
dirilis sebelum TTS berfungsi") benar-benar terpenuhi secara end-to-end, bukan hanya
secara mekanisme software di sandbox.
