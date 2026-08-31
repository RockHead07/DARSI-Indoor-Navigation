# AI-AVATAR-ASSISTANT.md — AI Avatar Assistant (VRM + RAG) Specification

> **Status:** KAJIAN ARSITEKTUR & DESAIN (Exploratory / Future Roadmap).
> Dokumen ini mendokumentasikan spesifikasi teknis, arsitektur data, integrasi format **VRM**, serta analisis keselamatan UX untuk pengembangan asisten avatar interaktif di DARSI Indoor Navigation.

> **⚠️ Fase 3 sudah punya spec implementasi tersendiri (2026-08-20).**
> Backend RAG (§3 dokumen ini) dipecah jadi sub-project yang dikerjakan lebih dulu dan berdiri
> sendiri, tanpa avatar/TTS/gesture. Spec-nya ada di **repo `darsi-backend`**
> (GitHub: `RockHead07/DARSI-Indoor-Navigation-Backend`), berkas
> `docs/superpowers/specs/2026-08-20-rag-assistant-backend-design.md`.
>
> **Fase 3 sudah DIIMPLEMENTASIKAN dan DIUKUR (2026-08-20).** Keputusan arsitekturnya
> terkunci di **ADR-026** (`docs/DECISIONS.md` repo ini), angka dan metodologinya di
> `docs/RETRIEVAL-EVALUATION.md` repo `darsi-backend`.
>
> Yang paling penting diketahui lebih dulu: **relevansi diputuskan LLM, bukan ambang
> skor kemiripan.** Rancangan awal mengasumsikan ada ambang yang bisa menyaring
> pertanyaan di luar cakupan, dan asumsi itu terbukti salah saat diukur (pertanyaan
> sampah bisa mendapat skor lebih tinggi daripada pertanyaan sah, pada dua model
> embedding berbeda). Angka sah untuk dilaporkan: **recall@3 71,9%** pada set uji
> bersih, corpus simulasi.
>
> **Baca spec itu dulu sebelum menyentuh apa pun soal RAG.** Spec tersebut memuat beberapa
> keputusan yang **mengoreksi** rancangan awal di §3 di bawah, khususnya:
> - **Jadwal dokter TIDAK lewat vector search**, tapi query SQL biasa (data terstruktur =
>   lookup, bukan pencarian makna). Diagram §3 di bawah menulis "Vector Search (... Jadwal ...)",
>   itu sudah direvisi jadi retrieval hybrid.
> - **`poi_id` diturunkan dari metadata chunk**, tidak pernah dihasilkan LLM.
> - **`audio_url` / `gesture` / `expression`** di kontrak §3.1 belum diimplementasikan
>   (menunggu Fase 1/2), jadi jangan diasumsikan sudah tersedia.
> - Seluruh isi corpus tahap ini adalah **data simulasi** dan wajib ditandai seperti itu.
>
> **✅ Update 2026-08-26: seluruh punch-list di bawah SUDAH DITUTUP, kecuali
> device fisik.** Gerbang kerja "jangan mulai avatar sebelum RAG matang" yang
> tertulis di sini (2026-08-24) sudah dicabut Amandemen 030-A untuk alasan
> operasional (bukan menunggu RAG), dan sekarang RAG-nya sendiri juga sudah
> matang — dua alasan berbeda, kesimpulan sama. Riwayat lengkap di bawah
> dipertahankan sebagai jejak, bukan dihapus.
> - ~~`POI_SYNC_TOKEN` di server belum dirotasi dari default~~ **SELESAI
>   (2026-08-24)**, sudah dirotasi + diverifikasi (token lama ditolak 401).
> - ~~`eval_llm_judge.py` — angka belum final~~ **SELESAI (2026-08-26)**:
>   `#06` (luka-robek) dan penolakan out-of-scope (`poi_id` tidak bocor)
>   terverifikasi **PASS di 3 dari 3** run susulan berturut-turut. Riwayat
>   angka: 45/52 (86,5%) → 46/52 → 51/52 (98,1%, run bersih terakhir sebelum
>   fix ambang) → 3 run terakhir semuanya kena kontaminasi infrastruktur
>   (503/koneksi, 6-8 dari 52 tiap run) sehingga tidak ada angka 52/52 yang
>   benar-benar bersih pasca-ADR-036, TAPI sinyal kontennya konsisten:
>   dua perbaikan paling penting (keselamatan + kebocoran `poi_id`) terbukti
>   kokoh, bukan kebetulan sekali jalan. Detail lengkap + tabel kategori:
>   `README.md` repo `darsi-backend`.
>   - ~~`poi_id`/`poi_name` bocor saat menolak~~ **SELESAI (2026-08-26)**:
>     LLM sekarang menandai jawabannya sendiri (`[TOLAK]`) saat menolak,
>     backend membuang `poi_id` berdasarkan penanda itu — bukan ditebak dari
>     kata kunci. Lihat ADR-036 (§ "Amandemen" di dalamnya).
>   - ~~#6 (luka robek/berdarah)~~ **SELESAI (2026-08-26)**: akar masalahnya
>     BUKAN celah kosakata (dugaan awal keliru, sudah dikoreksi) — gerbang
>     `MIN_TOP_SCORE` cuma baca skor vector, kata kunci literal pun tidak
>     menolong. Ditambal turunkan ambang 0,22→0,15 (ADR-036), divalidasi set
>     uji baru yang ditulis buta sebelum pengukuran (`test-3`/`test-4`).
>   - #12/#13 (jadwal dokter kadang tidak sebut lokasi) TETAP terbuka, prioritas
>     rendah — diduga variasi sampling LLM, bukan bug kode.
>   - **Utang BARU (2026-08-26), belum diselidiki**: Bifrost/jaringan gagal
>     ~10-15% di bawah beban 52 panggilan berturut-turut, di 3 run independen.
>     Diagnosis lewat `docker compose logs` buntu total (container tidak
>     mencetak log request/exception). Bukan bug RAG, tapi relevan untuk
>     kesiapan lapangan. Detail: `README.md` repo `darsi-backend`.
> - ~~Cloudflare Tunnel masih quick tunnel~~ **SELESAI (2026-08-24)**: Named
>   Tunnel permanen aktif di `https://api-darsi.rockhead07.tech` (domain
>   `rockhead07.tech`, dibuat lewat CLI `cloudflared tunnel login/create/route
>   dns` — BUKAN dashboard Zero Trust, itu minta aktivasi produk berbayar
>   yang butuh kartu), jalan sebagai systemd service, survive restart/reboot.
>   Sudah dipasang ke `AssistantClient.baseUrl` di scene `TestingHCM.unity`.
> - ~~Gerbang skor retrieval (`MIN_TOP_SCORE=0.22`) menolak sebagian query
>   valid~~ **SELESAI (2026-08-26)**, lihat ADR-036 di atas.
> - **SATU-SATUNYA yang masih terbuka: belum pernah dites di device Android
>   fisik pakai mic asli.** Seluruh verifikasi sejauh ini lewat Unity Editor
>   Play mode + `curl` langsung + `eval_llm_judge`. Checklist siap dibawa ke
>   lokasi: [`docs/FIELD-TEST-RAG-ASSISTANT.md`](FIELD-TEST-RAG-ASSISTANT.md).
>
> 📌 **Update 2026-08-24 (ADR-030):** Eksplorasi visual & aset avatar 3D (Tahap 1: Visual Companion)
> diizinkan berjalan secara paralel dan terisolasi pada branch terpisah `feature/vrm-avatar-assistant`
> dengan scene sandbox `Sandbox_AvatarCompanion.unity` tanpa mengganggu branch `main` atau
> scene produksi `WholePSDKU`.


---

## 1. Ringkasan Eksekutif & Visi Konsep

Fitur **AI Avatar Assistant** dirancang untuk menghadirkan asisten virtual interaktif 3D yang dapat berdialog secara natural dengan pasien dan pengunjung RS Islam A. Yani. Terinspirasi dari karakter interaktif ekspresif (seperti karakter *Mita* pada game *MiSide* / karakter VRoid), avatar ini berfungsi sebagai **pemandu rumah sakit cerdas** yang:
1. Menatap langsung ke arah pengguna secara dinamis (*dynamic head/eye look-at tracking*).
2. Menjawab pertanyaan seputar layanan RS (jadwal dokter, alur pendaftaran BPJS, lokasi poli) menggunakan **Retrieval-Augmented Generation (RAG)**.
3. Memberikan panduan navigasi spasial dengan gestur penunjuk arah (*pointing gesture*) sebelum mengaktifkan rute navigasi AR.

---

## 2. Spesifikasi Teknis Model 3D: Format VRM

Avatar 3D menggunakan standar **VRM (Virtual Reality Model)** berbasis glTF 2.0 yang diintegrasikan ke Unity menggunakan paket **UniVRM**.

```
                           ┌─────────────────────────────────────────┐
                           │            MODEL 3D (.VRM)              │
                           └─────────────────────────────────────────┘
                                                │
         ┌──────────────────────────────────────┼──────────────────────────────────────┐
         ▼                                      ▼                                      ▼
┌──────────────────┐                  ┌──────────────────┐                  ┌──────────────────┐
│  VRMLookAtHead   │                  │VRMBlendShapeProxy│                  │ Unity Humanoid   │
│  (Eye Tracking)  │                  │  (Lip-Sync &     │                  │     Mecanim      │
│  Tatap kamera AR │                  │   Ekspresi)      │                  │ Gestur & Pose    │
└──────────────────┘                  └──────────────────┘                  └──────────────────┘
```

### 2.1. Keunggulan Format VRM untuk DARSI
* **Built-in Look-At (`VRMLookAtHead`):** Mengarahkan pandangan kepala dan bola mata avatar ke `Camera.main` (kamera AR) secara otomatis dan natural tanpa memerlukan kalkulasi Inverse Kinematics (IK) manual.
* **Standardized Lip-Sync Visemes:** Memiliki *blendshape preset* standar untuk bentuk mulut vokal: `A`, `I`, `U`, `E`, `O`. Saat audio Text-to-Speech (TTS) diputar, sistem menggerakkan viseme ini untuk sinkronisasi bibir yang akurat.
* **Standardized Facial Expressions:** Preset emosi bawaan (`Joy`, `Angry`, `Sorrow`, `Fun`, `Blink`, `Neutral`) untuk memberikan reaksi emosional yang ramah saat menyapa atau berpikir.
* **Humanoid Rig Compatibility:** Otomatis kompatibel dengan ribuan animasi humanoid standar Unity (Mecanim Animator Controller).

### 2.2. Guardrail Kinerja Mobile (UaaL Android Safe)
Model VRM standar dari authoring tool (seperti *VRoid Studio*) biasanya berukuran besar. Agar tidak membebani performa HP di lingkungan Unity as a Library (UaaL) bersama ARCore dan VPS MultiSet, aturan berikut **wajib** dipatuhi:

| Parameter | Standar VRoid Baku | Target Optimasi DARSI | Alasan |
|---|---|---|---|
| **Poligon (Triangles)** | 30.000 – 70.000 | **$\le$ 15.000 tris** | Mencegah *bottleneck* geometri pada GPU mobile |
| **Material Slots** | 8 – 16 material | **1 – 2 Material (Atlas)** | Mengurangi *Draw Calls* agar FPS AR stabil di 60 FPS |
| **Tekstur Resolution** | 2048 × 2048 (banyak file) | **1024 × 1024 (Bake Atlas)** | Menghemat alokasi VRAM |
| **SpringBones (Physics)** | 40 – 80 rantai tulang | **$\le$ 8 rantai utama** (atau dinonaktifkan) | Menghemat beban komputasi CPU di Android |

---

## 3. Arsitektur Sistem: Modular Cloud-Edge Brain

Untuk menjaga agar aplikasi mobile tetap ringan dan data rumah sakit dapat diperbarui secara dinamis (OTA), pemrosesan AI cerdas dipusatkan di **FastAPI Backend**.

```
[ Pengguna ] ──(Voice Input)──► Unity Client (DARSI)
                                       │
                                       ▼ (HTTP POST /api/assistant/query)
                                FastAPI Backend
                                  ├── 1. Embed Query
                                  ├── 2. Vector Search (Supabase pgvector: SOP, Jadwal, Poli)
                                  ├── 3. LLM Reasoning (Groq API — System Prompt RS)
                                  └── 4. Text-to-Speech Synthesizer (Edge-TTS / Natural ID)
                                       │
                                       ▼ (JSON: text, audio_url, gesture, poi_id)
                                Unity Client (DARSI)
                                  ├── Mainkan Animasi Gestur (Animator)
                                  ├── Gerakkan Bibir Viseme (VRMBlendShapeProxy)
                                  ├── Putar Audio Suara (AudioSource)
                                  └── Jika ada target lokasi ──► Mulai Navigasi AR
```

### 3.1. Kontrak Payload Komunikasi (Unity ↔ Backend)

#### Request dari Unity ke Backend:
```json
{
  "user_text": "Di mana letak poli anak dan dokternya siapa?",
  "current_floor": "Lantai 1",
  "building": "RS Islam Ahmad Yani"
}
```

#### Response dari Backend ke Unity:
```json
{
  "status": "success",
  "response_text": "Poli Anak berada di Lantai 2. Hari ini ada dr. Ahmad Sp.A yang praktek hingga pukul 14.00. Saya siapkan rutenya ya!",
  "audio_url": "https://backend.darsi.id/static/tts/response_8921.mp3",
  "gesture": "point_upstairs",
  "expression": "Joy",
  "poi_id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "poi_name": "Poli Anak"
}
```

---

## 4. Analisis Keselamatan & UX Rumah Sakit

Berdasarkan panduan resmi **Google ARCore** dan keputusan arsitektur proyek ([`FLOWS.md`](file:///D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor%20Navigation/docs/FLOWS.md#L59)), AR harus tetap menjadi *"magic lens"* dan tidak boleh menimbulkan bahaya fisik bagi pasien/pengunjung di koridor rumah sakit.

### 4.1. Analisis Risiko Penempatan Karakter di AR

| Model Penempatan | Risiko Keselamatan / Performa | Rekomendasi |
|---|---|---|
| **Spawn Besar 1.5m di Depan Kamera** | ❌ **Tinggi:** Menutupi pandangan fisik dunia nyata (pasien tidak melihat orang lain, kursi roda, atau tangga di koridor). | **Ditolak** untuk sesi navigasi berjalan. |
| **Mini Floating Companion (Pojok HUD)** | ✅ **Aman:** Berukuran kecil di sudut pandang, tidak memblokir jalur jalan, tetap memberikan reaksi suara dan visual. | **Disetujui** sebagai opsi pemandu AR. |
| **Virtual Info Kiosk (Lobi / Titik Statis)** | ✅ **Aman:** Avatar hanya berdiri di titik awal (seperti meja informasi lobi), menghilang saat user mulai berjalan. | **Disetujui** sebagai opsi resepsionis digital. |
| **Pre-AR Assistant (Layar Home / WebView)** | ✅ **Sangat Aman & Ringan:** Interaksi penuh tanya jawab konsultasi terjadi sebelum user berjalan di koridor. | **Sangat Direkomendasikan** untuk tahap awal. |
| **Lead-Follow Guide (berjalan memimpin di rute)** | ⚠️ **Sedang, bisa dikelola:** Avatar bergerak *menjauh* sambil memimpin sehingga tidak parkir di depan wajah, tetapi bisa membelok di tikungan dan muncul mendadak di jarak dekat. | **Disetujui bersyarat** (ADR-034), wajib dengan safety fade yang **terbukti menyala** dan gate lokalisasi. |

> 📌 **Update 2026-08-24 (ADR-034).** Baris *Lead-Follow Guide* adalah model penempatan yang
> dipilih untuk pengembangan lanjutan, menggantikan spawn statis Tahap 1. Dua batasan yang
> mengikat dan tidak boleh dilewati: (1) avatar **tidak memiliki `NavMeshAgent` sendiri**, ia
> menyusuri polyline rute yang sudah dihitung MultiSet SDK; (2) locomotion **tidak boleh
> diaktifkan** sebelum `AvatarSafetyFade` terbukti bekerja lewat observasi Play mode sungguhan;
> (3) **informasi rute dibawa audio**, visual hanya penguat, sehingga lead-follow tidak boleh
> dirilis sebelum TTS (ADR-033) berfungsi. Tanpa lapisan audio, model ini berubah jadi "pengguna
> berjalan di koridor sambil menatap layar", persis bahaya yang tabel di atas ingin cegah.
> Baca ADR-034 di `docs/DECISIONS.md` sebelum menyentuh kode avatar. State machine §5 di bawah
> masih menggambarkan alur Tahap 1 (statis) dan akan menyusul diperbarui ke FSM Lead-Follow;
> kalau keduanya berbeda, **ADR-034 yang berlaku**.

---

## 5. State Machine Interaksi Karakter (Unity)

Di sisi Unity, avatar dikendalikan oleh *State Machine* berikut:

```
[Hidden] ──(Mic/Ask Trigger)──► [Spawning] ──(Wave/LookAt)──► [Listening]
                                                                  │
                                                        (User Finish Speaking)
                                                                  ▼
[Hidden] ◄──(Fade Out)── [Despawning] ◄──(Dialog/Nav)─── [Speaking/Pointing] ◄──(RAG Ready)── [Thinking]
```

1. **`Hidden`:** Avatar tidak aktif (game object non-aktif untuk menghemat CPU/GPU).
2. **`Spawning`:** Muncul dengan animasi menyapa (*Wave*) dan `VRMLookAtHead` aktif mengunci posisi kamera AR.
3. **`Listening`:** Animasi idle aktif mendengarkan (indikator mic aktif di UI).
4. **`Thinking`:** Animasi tangan di dagu/berpikir saat backend sedang melakukan RAG retrieval.
5. **`Speaking`:** Memutar audio TTS, viseme mulut `A-I-U-E-O` bergerak, ekspresi wajah ramah (`Joy`).
6. **`Pointing`:** Tangan mengarah ke arah tujuan saat rute navigasi AR digambar di lantai.
7. **`Despawning`:** Karakter melambai dan menghilang secara halus (*fade-out*), memberikan pandangan penuh kepada pengguna untuk berjalan mengikuti panah navigasi.

---

## 6. Rencana Tahapan Pengembangan (Roadmap)

> ### 📍 STATUS NYATA per 2026-08-25 (branch sudah di-merge ke `main`, ADR-030 Amandemen 030-A)
>
> **Fase 1 dan seluruh ADR-030 Tahap 1 SELESAI, malah terlampaui.** Yang dibangun jauh
> melewati "Visual Companion pasif": avatar kini **berjalan memimpin pengguna** menyusuri
> rute MultiSet (lead-follow, ADR-034), lengkap dengan animasi Mixamo, gestur sapa/tunjuk,
> tatapan kepala + bola mata, dan serah terima lintas lantai.
>
> **Terukur, bukan diklaim** (semua dari Play mode, angka lengkap di pesan commit):
> simpangan avatar dari garis rute **0,000 m**; **0 dari 410 frame** avatar tertinggal di
> belakang pengguna; `WaitingForUser` menyala di ambang 1,5 s lalu badan berputar ke
> **0,0°** menghadap pengguna; safety fade mematikan **3/3 renderer pada 0,50 m**.
>
> **Fase 2 (Voice & Viseme Lip-Sync) BELUM DIMULAI SAMA SEKALI.** `AvatarSpeechLipSync.cs`
> dan `AvatarAudioClient.cs` nol, belum ada filenya. Prasyaratnya sudah siap: prefab VRM
> punya `VRMBlendShapeProxy` dan 19 blendshape termasuk viseme **A, I, U, E, O** lengkap.
>
> **Fase 2 adalah GATE RILIS, bukan sekadar item berikutnya.** ADR-034 keputusan 7
> melarang lead-follow dirilis sebelum TTS berfungsi: tanpa lapisan audio, pemandu ini
> berubah jadi "pengguna berjalan di koridor RS sambil menatap layar", persis bahaya yang
> §4.1 ingin cegah. Jadi Fase 2 memblokir apa yang sudah jadi.
>
> Baca **ADR-034 beserta Amandemen 034-A** di `docs/DECISIONS.md` sebelum menyentuh kode
> avatar. Sembilan keputusannya mengikat, dan beberapa mengoreksi rancangan di dokumen ini.

* [x] **Fase 1 (Aset & Pipeline VRM):** SELESAI, kecuali satu item.
  * [x] Import paket `UniVRM` ke Unity. **`com.vrmc.gltf` + `com.vrmc.univrm` v0.131.2**
    (VRM **0.x**, bukan 1.0). Versi dipatok sadar: release itu yang memperbaiki import
    exception pada Unity 6.2-6.5, dan project ini Unity 6.3.
  * [ ] **BELUM: model VRM rendah poligon ($\le 15.000$ tris).** Yang dipakai
    `AvatarSample_A` bawaan UniVRM, **25,5 MB**, dan tabel §2.2 sendiri menyebut model
    VRoid baku 30.000-70.000 tris. Ini menunggu model RS sungguhan, bukan pekerjaan kode.
    Placeholder-nya **gitignored** supaya blob-nya tidak permanen di history.
  * [x] Validasi look-at terhadap kamera di Play Mode. **Temuan penting: `VRMLookAtHead`
    bawaan UniVRM hanya menggerakkan BOLA MATA**, tidak pernah memutar kepala. Di VRM 0.x
    memutar kepala adalah tugas aplikasi. Jadi §2.1 dokumen ini keliru saat menyiratkan
    look-at bawaan menangani kepala sekaligus. Kepala dipegang `AvatarLookAtController`
    (clamp 55°), mata oleh `VRMLookAtHead` + `VRMLookAtBoneApplyer`. Dua sistem, dua tulang
    berbeda, tidak berebut.
* [ ] **Fase 2 (Voice Output, TTS & Viseme Lip-Sync):** — **Kajian arsitektur & spec: [`docs/superpowers/specs/2026-08-26-voice-output-lipsync-architecture.md`](superpowers/specs/2026-08-26-voice-output-lipsync-architecture.md)**
  * Evaluasi engine lip-sync: `hecomi/uLipSync` (MFCC Burst) vs Custom C# FFT Formant Driver.
  * Implementasi *audio amplitude/formant to viseme driver* untuk menggerakkan `A, I, U, E, O` pada `VRMBlendShapeProxy`.
  * Integrasi TTS audio (`edge-tts` `id-ID-GadisNeural`) dan pemutaran audio panduan suara.
* [ ] **Fase 3 (Backend RAG & TTS Integration):** — **sudah punya spec, lihat catatan di kepala dokumen.**
  Spec: repo `darsi-backend`, `docs/superpowers/specs/2026-08-20-rag-assistant-backend-design.md`.
  * Penyusunan tabel dokumen RS + pgvector (`knowledge_chunks`) **dan tabel terstruktur
    `doctor_schedules`** yang sengaja tidak di-embed (retrieval hybrid).
  * Endpoint FastAPI `POST /api/assistant/query` untuk pipeline RAG.
  * Groq dipanggil dari server (menutup utang keamanan `groqApiKey` di `OllamaConnector.cs`).
  * Edge-TTS generator **belum masuk lingkup spec itu**, menyusul setelah RAG terbukti jalan.
* [ ] **Fase 4 (Pengujian Lapangan & Evaluasi UX):**
  * Pengujian performa frame rate (FPS) dan memori di perangkat Android target.
  * Evaluasi kenyamanan pengguna saat berdialog dengan avatar di lingkungan RS Islam A. Yani.
