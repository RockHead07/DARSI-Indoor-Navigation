---
description: Unity Development, Git Discipline & AI Avatar Rules for DARSI
trigger: always_on
---

# Unity Development & Project Workflow Rules for DARSI

## 1. Branching, Shared Workspace & Git Discipline
- **Branching & Scene Isolation (Amandemen 030-A):** Pengembangan fitur (termasuk AI 3D Avatar/VRM) berjalan langsung di branch `main` karena gerbang isolasi branch dicabut via Amandemen 030-A guna menghindari tabrakan file pada *shared working tree*. Namun, **isolasi scene tetap mutlak**: pengembangan dan eksplorasi WAJIB dilakukan di scene sandbox (contoh: `Assets/Scenes/Sandbox_*.unity` atau `TestingHCM.unity`), dan DILARANG MENGUBAH scene produksi (`DARSi-Indoor Navigation.unity`) untuk keperluan eksplorasi awal.
- **Testing Scene Priority:** Saat pengujian di Editor, perbaikan interaktif, atau debugging fitur, selalu utamakan scene sandbox/testbed aktif (`TestingHCM.unity` atau `Sandbox_*.unity`) sesuai fokus user saat ini.
- **Git Metadata (.meta):** Selalu stage dan commit file `.meta` Unity bersamaan dengan setiap skrip, scene, prefab, atau aset baru untuk mencegah GUID rusak.
- **Shared Working Directory Care:**
  - Selalu jalankan `git branch --show-current` dan `git status` sebelum dan di antara setiap operasi git/commit untuk memastikan posisi branch (selalu di `main`) dan mendeteksi perubahan dari sesi lain.
  - Jika terdapat file dirty yang bukan milik sesi aktif (terutama `.unity`, `ProjectSettings/EditorBuildSettings.asset`, `*.slnx`), amankan dengan `git stash push -u -m "<keterangan> not mine" -- <files>`. JANGAN PERNAH menjalankan `git checkout --` atau `git clean` pada file sesi lain.
  - Gunakan `git add -p` untuk memisahkan hunk campuran dalam file yang sama.
- **Git Author & Push Invariant:**
  - Seluruh commit Git wajib menggunakan identitas author pemilik proyek (`Bagus Insan Pradana <dana.bagus07@gmail.com>`) dengan format Conventional Commits bersih.
  - DILARANG menambahkan trailer `Co-Authored-By` atau metadata AI pada pesan commit.
  - DILARANG melakukan `git push` ke remote tanpa persetujuan eksplisit dari Bagus.

## 2. ADR Governance & Single Source of Truth
- **Pencatatan Arsitektur Tersentralisasi:** Seluruh keputusan arsitektur (ADR) untuk KEDUA repo (Unity & Backend) dicatat di `docs/DECISIONS.md` di repo Unity.
- **ADR Amendment:** Setiap penyimpangan atau penyesuaian dari keputusan terkunci wajib dicatat resmi sebagai amandemen ADR (`#### Amandemen NNN-X`).
- **Evidence-Based Verification:** Klaim tervalidasi/berhasil wajib menyertakan bukti nyata (log Play mode, curl output, unit test pass), bukan sekadar analisis teoritis.
- **Scope Isolation in Test Reporting:** Laporan hasil pengujian (unit test, integration test, probe) WAJIB mengisolasi dan menyebutkan jumlah test yang secara eksklusif milik file/komponen/fitur yang diuji pada sesi terkait. DILARANG mencampurkan atau mengklaim hasil test dari file suite lain yang tidak terkait ke dalam angka kelulusan sesi. Jika melaporkan hasil regresi global, pisahkan secara tegas antara hasil file target dan hasil test suite proyek keseluruhan.

## 3. Gaya Komunikasi & Dokumentasi
- **Bebas Em Dash:** Hindari karakter em dash (—) dalam seluruh teks yang ditulis untuk komunikasi dengan Bagus (chat, ringkasan, laporan). Gunakan koma, titik dua, tanda hubung biasa (-), atau titik.
- **Bahasa Indonesia Standar:** Seluruh dokumentasi, pesan commit, dan komentar kode ditulis dalam Bahasa Indonesia. Komentar kode fokus menjelaskan KENAPA (alasan teknis), bukan sekadar APA.
- **Clickable File & Symbol Links:** Wajib menyertakan tautan markdown `file://` untuk setiap penyebutan file, kelas, method, atau simbol kode.

## 4. Coplay MCP & Automasi Unity
- **Project Root Setup:** Saat berinteraksi dengan Unity Editor via Coplay MCP, selalu verifikasi dan set project root (`set_unity_project_root`) ke path workspace saat ini.
- **Automasi Scene & Eksekusi Skrip:** Utamakan penggunaan `execute_script` untuk menjalankan method setup otomatis (seperti `SceneBuilder`) dan `save_scene` daripada meminta user menyusun hierarki scene secara manual.
- **Konvensi MenuItem:** Daftarkan menu toolbar dengan prefiks standar proyek (`DARSI/...` dan `Tools/...`) agar mudah ditemukan di menu toolbar atas Unity Editor.

## 5. AR Overlay & Voice UI Standards
- **Auto-Dismiss on Navigation:** Panel overlay modal / bottom sheet (`VoicePanel`) WAJIB otomatis ditutup (`HidePanel()`) begitu navigasi AR aktif agar tidak menghalangi pandangan kamera AR dan garis rute navigasi di lantai.
- **Isolated Vertical Layout:** Komponen UI bertingkat (ilustrasi, teks transkrip dinamis, status pill/waveform) harus menggunakan pembagian zona vertikal proporsional dengan TextMeshPro auto-sizing agar tidak bertabrakan (*overlap*) saat teks memanjang.
- **Coroutine Active Guard:** Komponen MonoBehaviour pada panel UI yang bisa dinonaktifkan wajib mengecek `gameObject.activeInHierarchy` sebelum memanggil `StartCoroutine()` untuk mencegah runtime exception.

## 6. Runtime glTF/VRM & Avatar Rigging Standards
- **Look-At Anti-Spinning:** Saat memanipulasi rotasi tulang di `LateUpdate()` pada model glTF/VRM tanpa AnimationClip reset, selalu simpan *base rest pose* sekali dan hitung rotasi absolut dari parent tulang (`headBone.parent.rotation * _headRestLocalRot`), jangan mengalikan rotasi frame ke frame sebelumnya untuk mencegah putaran looping 360°.
- **Dynamic VisualRoot Binding:** Saat model 3D runtime menggantikan placeholder visual, selalu alihkan referensi `VisualRoot` pada lifecycle controller ke instance runtime baru (`companion.VisualRoot = _vrmInstance`) dan nonaktifkan placeholder permanen agar siklus *Spawn / Dismiss* tidak mengaktifkan kembali objek dummy.

## 7. Voice Output (TTS) & Mobile Performance Guardrails
- **Fault Isolation:** Endpoint TTS (`POST /api/assistant/tts`) berdiri terpisah dari endpoint RAG (`/query`). Kegagalan sintesis suara tidak boleh menggagalkan respon teks atau data navigasi (`poi_id`).
- **Hybrid 2-Tier TTS:** Tier 1 `edge-tts` (`id-ID-GadisNeural`) sebagai cloud primer dan Tier 2 Sherpa-ONNX / Piper (`id_ID`) sebagai fallback otomatis saat koneksi offline.
- **Mobile Frame Budget:** Lip-sync driver dan modul audio tidak boleh membebani alokasi GC per frame agar FPS AR tetap stabil di 60 FPS pada perangkat target Android.
