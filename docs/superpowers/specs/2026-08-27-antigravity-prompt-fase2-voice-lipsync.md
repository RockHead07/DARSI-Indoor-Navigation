# Prompt Antigravity — Fase 2 Avatar: Voice Output (TTS) & Viseme Lip-Sync

> Draf untuk direview Bagus sebelum ditempel ke Antigravity. Disusun format CO-STAR.
> Ditulis untuk agent yang TIDAK punya memori percakapan ini — semua konteks yang
> dibutuhkan disematkan langsung di bawah, bukan dirujuk sebagai "seperti yang sudah
> kita bahas".

---

## CONTEXT

Kamu mengerjakan **DARSI Indoor Navigation**, aplikasi AR indoor navigation untuk RS
Islam A. Yani, dijalankan sebagai Unity-as-a-Library (UaaL) di dalam app Flutter
**MyRSIy**. Kamu TIDAK punya akses ke percakapan yang menghasilkan prompt ini — anggap
dirimu baru pertama kali membuka repo ini.

**Dua repo terlibat:**
- Unity: `D:\Dev\Projects\UnityProjects\Learning\DARSI-Indoor Navigation` (working
  directory-nya **dipakai bersama sesi lain** — lihat bagian "Kehati-hatian" di bawah,
  ini bukan opsional).
- Backend: `D:\Dev\Projects\darsi-backend` (GitHub `RockHead07/DARSI-Indoor-Navigation-Backend`),
  Python/FastAPI, selalu di branch `main`, tidak kena masalah shared-directory di atas.

**Tech stack:** Unity 6.3 LTS · UniVRM v0.131.2 (VRM 0.x, BUKAN 1.0) · MultiSet SDK
(VPS + NavMesh) · ARCore/ARFoundation · FastAPI + PostgreSQL/pgvector (backend) ·
target Android.

**WAJIB dibaca sebelum menulis kode apa pun** (urutan ini penting, ADR mengoreksi
dokumen desain, bukan sebaliknya):
1. `docs/DECISIONS.md` di repo Unity — **satu-satunya tempat pencatatan ADR untuk
   KEDUA repo**, termasuk keputusan yang secara teknis milik `darsi-backend`. Baca
   minimal ADR-026, ADR-029, ADR-033 (+ Amandemen 033-A), ADR-034 (+ Amandemen 034-A),
   ADR-036.
2. `docs/superpowers/specs/2026-08-26-voice-output-lipsync-architecture.md` — spec
   arsitektur Fase 2 ini, **sudah dikoreksi** mengikuti ADR-033 (baca §7 di spec itu
   untuk riwayat koreksinya). Ini spec yang jadi acuan implementasi, bukan versi
   sebelum koreksi.
3. `docs/AI-AVATAR-ASSISTANT.md` — roadmap fase, §2.2 (guardrail performa mobile),
   §4.1 (analisis keselamatan penempatan avatar).
4. `CLAUDE.md` di root repo Unity — aturan kerja proyek ini. Poin yang PALING sering
   dilanggar sesi-sesi sebelumnya: **baca ADR yang relevan dulu sebelum merancang apa
   pun**, dan **jangan diam-diam menyimpang dari keputusan yang sudah terkunci**.

**Yang sudah selesai dan terverifikasi (JANGAN dibangun ulang):**
- Visual/rigging VRM, locomotion (BlendTree Idle/Walk + Mixamo), gestur Wave/Point.
- Head/eye tracking: `AvatarLookAtController.cs` (leher, clamp 55°) + `VRMLookAtHead`
  bawaan UniVRM (bola mata) — **dua sistem terpisah, dua tulang berbeda**, jangan
  disatukan, itu keputusan sadar (lihat komentar di file).
- `AvatarSafetyFade.cs` — mematikan 3/3 renderer avatar pada jarak ≤0,50 m dari kamera
  (ADR-034 Amandemen 034-A, terverifikasi Play mode).
- `AIAvatarGuideController.cs` — lead-follow di atas polyline `NavigationController`
  MultiSet SDK (avatar TIDAK punya `NavMeshAgent` sendiri, ADR-034 keputusan 2),
  simpangan rute 0,000 m terukur.
- **RAG backend penuh, live produksi**: `POST /api/assistant/query` di
  `https://api-darsi.rockhead07.tech` (Named Tunnel permanen, systemd service).
  Bifrost (medgemma, gateway eksternal hcm-lab.id) primer, Groq fallback server-side.
  Gerbang relevansi retrieval `MIN_TOP_SCORE=0.15` (ADR-036). `AssistantClient.cs`
  (`Assets/Scripts/AssistantClient.cs`) sudah memanggil endpoint ini dan meneruskan
  `poi_id` ke jalur navigasi yang sama dengan Flutter (`UaaLEntryPoint`) — **satu
  jalur navigasi, bukan dua yang bisa beda perilaku**.
- Voice input: `VoiceInputHandler.cs` (Android `SpeechRecognizer`, `id-ID`).

**Yang BELUM ada sama sekali** (dicek langsung, bukan asumsi): `AvatarSpeechLipSync.cs`
dan `AvatarAudioClient.cs` — nol, belum ada file-nya di `Assets/Scripts/Avatar/`.
Endpoint `POST /api/assistant/tts` belum ada di `darsi-backend`.

> **⚠️ Pelajaran dari Sesi 1 (2026-08-31), baca ini sebelum menulis laporan apa pun.**
> Laporan penyelesaian Sesi 1 mengklaim *"9 dari 9 test EditMode lulus"*. Setelah
> diverifikasi independen (file `AvatarSpeechLipSyncTests.cs` dibaca langsung, bukan
> dipercaya dari laporan), kenyataannya cuma **4 test**. 5 sisanya (`POIData.Floor_*`,
> `POIData.Building_*`) adalah test LAMA dari file lain, tidak ada hubungannya sama
> sekali — tercampur ke laporan, kemungkinan besar dari menjalankan suite penuh
> lalu menghitung semua baris `PASS` yang muncul di layar, bukan dari test file yang
> sedang dikerjakan. Ini ketahuan karena pemilik proyek membaca file test-nya
> langsung, bukan karena laporan mengoreksi dirinya sendiri.
>
> Konsekuensinya: **setiap angka di laporan sesi manapun sekarang WAJIB
> diverifikasi ulang persis sebelum ditulis** — lihat aturan konkret di bagian
> RESPONSE FORMAT di bawah. Ini bukan formalitas tambahan; ini syarat supaya
> laporan bisa dipercaya tanpa pemilik proyek harus membongkar ulang setiap klaim.

**Gate rilis, ini alasan Fase 2 ini dikerjakan sama sekali** — ADR-034 keputusan 7:
> "Informasi rute dibawa audio; visual adalah penguat, bukan syarat... Lead-follow
> TIDAK BOLEH dirilis sebelum TTS berfungsi, untuk mencegah pengguna berjalan di
> koridor RS sambil terpaku menatap layar HP."

Avatar yang berjalan-memimpin **sudah selesai dan terukur bagus**, tapi secara sadar
**ditahan dari rilis** sampai Fase 2 ini kelar. Itu levelnya urgensi kerjaan ini.

**Keputusan yang SUDAH terkunci, jangan dirancang ulang** (ADR-033 + Amandemen 033-A):
1. Endpoint TTS **terpisah** dari `/query` (`POST /api/assistant/tts`), BUKAN digabung
   jadi satu response. Alasannya: edge-tts itu API tidak resmi Microsoft yang riwayatnya
   pernah berubah tanpa peringatan, dan Bifrost sendiri sudah 13-32 detik — kalau
   digabung, kegagalan sintesis suara bisa menjatuhkan jawaban teks + `poi_id` navigasi
   yang sebenarnya sudah siap.
2. **Hybrid dua tier**: Tier 1 `edge-tts` (`id-ID-GadisNeural`, primer, gratis, latensi
   <250ms). Tier 2 Sherpa-ONNX/Piper (`id_ID`, fallback offline OTOMATIS saat internet
   server terputus — skenario intranet RS air-gapped, BUKAN detail opsional).
3. Kontrak payload persis seperti di spec §5 (request `{text, voice}`, response
   `{audio_url, engine_used}` — `engine_used` WAJIB ada, buat membedakan Tier 1/2 saat
   diagnostik).

**Guardrail performa mobile (§2.2 `AI-AVATAR-ASSISTANT.md`)**, berlaku untuk SEMUA kode
yang kamu tulis di sisi Unity: HP Android sudah menanggung ARCore + VPS MultiSet +
render avatar 60 FPS. Lip-sync driver TIDAK BOLEH menambah beban CPU/GPU yang
signifikan — kalau pakai `AudioSource.GetSpectrumData`, itu berjalan tiap frame,
harus diprofilkan, bukan diasumsikan murah.

**Kehati-hatian yang WAJIB dijalankan di repo Unity (working directory dipakai
bersama):**
1. `git branch --show-current` **sebelum** setiap kali mau commit — jangan asumsikan
   HEAD ada di `main`, dan cek ULANG di antara setiap langkah git, bukan cuma sekali di
   awal. Working directory ini pernah kejadian branch-nya pindah sendiri di tengah
   operasi tanpa disentuh, karena ada Unity Editor lain yang hidup memegang project
   yang sama.
2. Kalau `git status` menunjukkan file dirty yang BUKAN kamu sentuh (terutama file
   scene `.unity`, `ProjectSettings/EditorBuildSettings.asset`, `*.slnx`): itu punya
   sesi lain. `git stash push -u -m "<keterangan> not mine" -- <file-file itu>`,
   JANGAN `git checkout --` atau `git clean` file itu, JANGAN commit bercampur dengan
   punyamu.
3. Kalau perlu memisah hunk campuran dalam satu file yang sama (punyamu + punya sesi
   lain di file yang sama), pakai `git add -p` dan pisahkan per hunk — jangan commit
   file utuh begitu saja kalau isinya campuran.
4. **Commit SELALU sebagai identitas git yang sudah ter-`config` di mesin ini** (JANGAN
   ganti `user.name`/`user.email`). **JANGAN PERNAH** menambahkan trailer
   `Co-Authored-By` atau atribusi AI apa pun ke pesan commit.
5. **JANGAN push** tanpa Bagus mereview dulu. Commit boleh jalan terus (lokal, mudah
   dibatalkan), push tunggu konfirmasi eksplisit.

**Konvensi proyek yang wajib diikuti:**
- Dokumentasi, komentar kode, dan pesan commit dalam **Bahasa Indonesia** — cek file
  manapun di repo ini (`OllamaConnector.cs`, `retrieval.py`, `docs/DECISIONS.md`) untuk
  gaya yang konsisten: to-the-point, komentar menjelaskan KENAPA bukan APA, angka
  terukur dikutip dengan sumbernya, bukan diklaim tanpa bukti.
- **Tidak ada em dash (—) di teks yang ditulis untuk dibaca Bagus** (chat, ringkasan,
  laporan). Boleh tetap ada di dokumen yang SUDAH ada em dash sebagai gaya penulisan
  existing (`docs/DECISIONS.md` banyak memakainya) — jangan ubah gaya dokumen lama,
  tapi teks baru yang kamu tulis untuk komunikasi hindari em dash, pakai koma/titik.
- **Setiap penyimpangan dari spec/ADR yang sudah terkunci HARUS ditulis sebagai
  amandemen ADR** (pola `#### Amandemen NNN-X`), bukan diam-diam. Lihat Amandemen
  033-A dan 034-A di `docs/DECISIONS.md` sebagai contoh format.
- **Klaim "sudah benar"/"sudah divalidasi" harus punya bukti eksekusi nyata** (Play
  mode, curl ke server sungguhan, log), bukan cuma penjelasan desain yang masuk akal
  di kertas. Riwayat proyek ini sudah dua kali kena kasus ADR yang menulis "terverifikasi"
  padahal baru terbukti gagal beberapa hari kemudian saat benar-benar dieksekusi.

---

## OBJECTIVE

Implementasikan **Fase 2 penuh** (Voice Output/TTS + Viseme Lip-Sync) sampai avatar bisa
**bicara dengan lip-sync yang tersinkron sambil memimpin rute (lead-follow)**, mengikuti
urutan kerja di spec §6 (tiga sesi berurutan, boleh dipecah jadi beberapa commit per
sesi):

**Sesi 1 — Unity Sandbox Audio & Lip-Sync (tidak bergantung backend):**
1. Putuskan engine lip-sync (lihat "Keputusan yang perlu kamu ambil" di bawah — ini
   BUKAN pertanyaan terbuka lagi di prompt ini, sudah ada rekomendasi + syarat).
2. Implementasikan driver lip-sync, terhubung ke `VRMBlendShapeProxy` avatar yang sudah
   ada (5 preset vokal: A, I, U, E, O).
3. Validasi pakai klip audio sample lokal dulu (BUKAN dari backend TTS — itu Sesi 3) di
   scene sandbox yang sudah ada (`Assets/Scripts/Avatar/AvatarSandboxUI.cs` /
   `Editor/AvatarSandboxSceneBuilder.cs`, cek dulu apa isinya sebelum bikin scene baru).

**Sesi 2 — Backend, endpoint TTS terpisah (repo `darsi-backend`):**
4. Pasang `POST /api/assistant/tts` sesuai kontrak spec §5, Tier 1 `edge-tts`.
5. Tier 2 fallback Sherpa-ONNX/Piper otomatis saat Tier 1 gagal/internet server putus.
6. Return `{audio_url, engine_used}`. Ikuti pola `docker-compose.yml` yang sudah ada
   (`api` service) — cek dulu apakah butuh volume baru untuk menyimpan file audio
   statis (`/static/tts/...` di contoh spec), atau ada cara lebih murah (mis. tidak
   perlu simpan permanen, cukup response streaming) — INI KEPUTUSAN TEKNIS kamu ambil
   sendiri berdasar apa yang paling konsisten dengan arsitektur `darsi-backend` yang
   sudah ada (baca `app/main.py`, `docker-compose.yml` dulu).

**Sesi 3 — Integrasi end-to-end (Unity, `AvatarAudioClient.cs` baru):**
7. Setelah `AssistantClient` dapat `answer` dari `/query` (lihat
   `Assets/Scripts/AssistantClient.cs:174-203` untuk pola panggilan yang sudah ada,
   IKUTI pola yang sama, jangan bikin gaya baru), panggil `/tts` **terpisah** dengan
   teks `answer` itu.
8. Fetch audio via `UnityWebRequestMultimedia.GetAudioClip` (spec §7 poin 3: MVP cukup
   file fetch, bukan WebSocket streaming — ini levelnya "sudah diputuskan cukup",
   jangan bangun streaming kecuali kamu temukan bukti kuat file fetch tidak cukup).
9. Putar audio, jalankan driver lip-sync dari Sesi 1 terhadap audio ini.
10. **Kegagalan `/tts` TIDAK BOLEH menjatuhkan jawaban teks + navigasi yang sudah
    diterima dari `/query`** — ini syarat keras dari ADR-033 Amandemen 033-A poin 1,
    uji eksplisit skenario ini (matikan/gagalkan `/tts`, pastikan navigasi & teks
    jawaban tetap jalan).
11. Sambungkan ke FSM Lead-Follow yang sudah ada: avatar bicara + lip-sync + (opsional)
    gestur Point saat sampai tujuan, LALU mulai berjalan memimpin.

**Keputusan yang perlu kamu ambil (bukan pertanyaan terbuka tanpa arah — ada
rekomendasi eksplisit):**

- **Engine lip-sync — REKOMENDASI: `hecomi/uLipSync`** (Burst-accelerated MFCC, MIT
  license, sudah punya komponen `uLipSyncBlendShape` yang langsung memetakan ke
  `VRMBlendShapeProxy`). Alasan: proyek ini sudah berulang kali memilih "benar dulu,
  baru kompromi" (lihat CLAUDE.md), dan Burst compiler justru LEBIH efisien di Android
  daripada C# manual (compile ke native code, tanpa beban GC) — jadi presisi lebih
  tinggi TIDAK berarti trade-off performa lebih buruk di sini, beda dari trade-off
  biasa. **Syarat sebelum mengunci ini jadi ADR baru:** profilkan dulu di HP target
  (atau minimal Editor Profiler) untuk pastikan tidak melanggar guardrail §2.2 —
  kalau ternyata `Unity.Burst`/`Unity.Mathematics` bikin build size/startup time
  melonjak signifikan, itu alasan sah untuk pindah ke Opsi B (Custom FFT/Formant,
  zero-dependency), TULIS SEBAGAI TEMUAN, jangan diam-diam ganti pilihan.
- **Tulis keputusan lip-sync engine sebagai ADR baru** di `docs/DECISIONS.md`
  (nomor berikutnya yang tersedia saat kamu commit — **cek ulang** `grep "^### ADR-"
  docs/DECISIONS.md` dulu, JANGAN asumsikan nomor dari prompt ini masih kosong, sesi
  lain mungkin sudah memakainya duluan karena working directory dipakai bersama).
  Sertakan hasil profiling sebagai bukti, bukan cuma alasan di kertas.

---

## STYLE

Ikuti gaya kode dan dokumentasi yang SUDAH ADA di kedua repo, jangan perkenalkan gaya
baru:
- C# Unity: komentar Bahasa Indonesia menjelaskan KENAPA (bukan APA), pola
  `[Header]`/`[Tooltip]` di field publik seperti `AssistantClient.cs`, singleton
  `instance` di `Awake()` seperti `OllamaConnector.cs`.
- Python/FastAPI: docstring modul menjelaskan alasan desain (lihat
  `app/assistant/retrieval.py` baris 1-18 sebagai contoh), tipe eksplisit
  (`str | None`, bukan `Optional[str]`), test kecil dan fokus (lihat
  `tests/test_router.py` — satu skenario per fungsi test, nama fungsi deskriptif
  panjang dalam Bahasa Indonesia).
- TDD di mana masuk akal: tulis tes yang gagal dulu untuk `split_refusal`-style
  fungsi murni (lihat `app/assistant/generation.py` fungsi `split_refusal` +
  test-nya di `tests/test_generation.py` sebagai contoh pola).
- YAGNI: jangan bangun abstraksi untuk kemungkinan yang belum diminta. Endpoint TTS
  cukup dua tier persis seperti ADR-033, jangan tambah tier ketiga "buat jaga-jaga".

## TONE

Teknis, langsung, berbasis bukti. Jangan menulis "seharusnya berhasil" atau "secara
teori" tanpa menjalankannya. Kalau menemukan sesuatu yang bertentangan dengan spec/ADR
saat implementasi (kontrak API tidak cocok, dependency tidak tersedia, dll), **berhenti
dan laporkan**, jangan diam-diam mengubah arah dan melanjutkan.

**Angka dalam laporan bukan tempat untuk percaya diri.** "Kira-kira segini" atau
mengingat dari konteks percakapan sebelumnya (yang mungkin sudah bercampur dengan hasil
run lain, test file lain, atau sesi lain) tidak cukup — lihat kejadian Sesi 1 di CONTEXT
di atas. Setiap angka WAJIB berasal dari command yang dijalankan ULANG, sesaat sebelum
kalimat itu ditulis, dengan scope yang sesempit mungkin (satu file test, bukan satu
folder; satu file, bukan satu direktori).

## AUDIENCE

Bagus (pemilik proyek, mahasiswa, developer solo) mereview hasil kerjamu lewat commit
log + laporan akhir. Dia sudah familiar dengan seluruh arsitektur (dia yang mengambil
keputusan ADR-033/034), jadi tidak perlu menjelaskan ulang konsep dasar RAG/VRM/AR —
tapi dia BELUM melihat kode yang kamu tulis, jadi laporan akhir harus konkret: file
mana berubah, apa yang terverifikasi jalan (dan buktinya), apa yang belum.

## RESPONSE FORMAT

Kerjakan per sesi (1 → 2 → 3), commit terpisah per langkah logis (ikuti pola
"test gagal → implementasi minimal → test lolos → commit" untuk bagian backend yang
testable).

**Sebelum menulis SATU KALIMAT PUN dari laporan** (lihat CONTEXT soal kenapa ini
bukan basa-basi), jalankan urutan ini, dalam urutan ini:

1. Untuk setiap klaim angka (jumlah test, jumlah file, jumlah baris, dsb.): jalankan
   ULANG command yang menghasilkan angka itu, **discope ke file/target paling sempit
   yang relevan** — `pytest tests/test_tts.py`, BUKAN `pytest tests/`;
   `grep -c "^def test_" file.cs`, BUKAN mengandalkan output run sebelumnya yang
   mungkin sudah bercampur konteks.
2. Salin **output mentah command itu apa adanya** ke field "Bukti mentah" di bawah —
   bukan parafrase, bukan rangkuman tulisan tangan seperti "9 dari 9 test lulus".
   Kalau outputnya panjang, potong bagian tengah tapi PERTAHANKAN baris ringkasan
   asli (`X passed, Y failed` atau setara) apa adanya, jangan ditulis ulang dengan
   kata-kata sendiri.
3. Cocokkan angka di ringkasan naratif dengan angka di "Bukti mentah" — kalau beda,
   yang naratif itu salah, perbaiki, ulangi dari langkah 1 kalau perlu.

Di akhir SETIAP sesi, laporkan dalam format ini sebelum lanjut ke sesi berikutnya:

```
## Sesi N selesai

**Commit:** <hash pendek> — <pesan singkat>
**File berubah:** <daftar path>
**Terverifikasi (bukti nyata, bukan asumsi):**
- <item> — <command persis yang dijalankan ULANG barusan> — <hasil aktualnya>

**Bukti mentah:**
<output command apa adanya, tidak diparafrase, untuk setiap klaim angka di atas>

**Keputusan yang diambil (kalau ada):**
- <keputusan> — <alasan> — <ditulis sebagai ADR-0XX? ya/tidak, kenapa>

**Belum terverifikasi / butuh device fisik:**
- <item>

**Menyimpang dari spec/ADR? (harus "tidak", atau jelaskan amandemen apa yang ditulis):**
```

Setelah Sesi 3, laporan akhir tambahan: apakah gate ADR-034 keputusan 7 (avatar tidak
boleh rilis tanpa TTS) sekarang **terpenuhi** dari sisi mekanisme (audio + lip-sync +
navigasi jalan bersamaan) — device fisik tetap jadi validasi akhir terpisah, TIDAK
perlu ditunggu untuk menyatakan gate ini terpenuhi secara mekanisme.

**JANGAN push ke remote di akhir sesi manapun** — commit lokal saja, minta konfirmasi
Bagus dulu sebelum `git push` di kedua repo.
