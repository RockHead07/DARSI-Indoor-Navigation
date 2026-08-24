---
description: Unity Development & Coplay MCP Rules for DARSI
trigger: always_on
---

# Unity Development & Coplay MCP Rules for DARSI

## 1. Branching & Feature Isolation
- **Feature Branching:** Fitur eksperimental atau subsistem sekunder (seperti 3D Avatar/VRM) WAJIB dikembangkan pada dedicated `feature/*` branch dan diuji di scene sandbox (contoh: `Assets/Scenes/Sandbox_*.unity`), tidak boleh menyentuh scene produksi seperti `WholePSDKU` di branch `main`.
- **Git Metadata (.meta):** Selalu stage dan commit file `.meta` Unity bersamaan dengan setiap skrip, scene, prefab, atau aset baru untuk mencegah GUID rusak.
- **Pencatatan Arsitektur:** Setiap keputusan pemisahan fitur, urutan kerja, atau perubahan strategi harus dicatat resmi sebagai ADR di `docs/DECISIONS.md`.
- **Git Author Invariant:** Seluruh commit Git wajib menggunakan identitas author pemilik proyek (`Bagus Insan Pradana <dana.bagus07@gmail.com>`) dengan format Conventional Commits bersih tanpa co-author atau metadata AI.

## 2. Coplay MCP & Automasi Unity
- **Project Root Setup:** Saat berinteraksi dengan Unity Editor via Coplay MCP, selalu verifikasi dan set project root (`set_unity_project_root`) ke path workspace saat ini.
- **Automasi Scene & Eksekusi Skrip:** Utamakan penggunaan `execute_script` untuk menjalankan method setup otomatis (seperti `SceneBuilder`) dan `save_scene` daripada meminta user menyusun hierarki scene secara manual.
- **Konvensi MenuItem:** Daftarkan menu toolbar dengan prefiks standar proyek (`DARSI/...` dan `Tools/...`) agar mudah ditemukan di menu toolbar atas Unity Editor.

## 3. Runtime glTF/VRM & Avatar Rigging Standards
- **Look-At Anti-Spinning:** Saat memanipulasi rotasi tulang di `LateUpdate()` pada model glTF/VRM tanpa AnimationClip reset, selalu simpan *base rest pose* sekali dan hitung rotasi absolut dari parent tulang (`headBone.parent.rotation * _headRestLocalRot`), jangan mengalikan rotasi frame ke frame sebelumnya untuk mencegah putaran looping 360°.
- **Dynamic VisualRoot Binding:** Saat model 3D runtime menggantikan placeholder visual, selalu alihkan referensi `VisualRoot` pada lifecycle controller ke instance runtime baru (`companion.VisualRoot = _vrmInstance`) dan nonaktifkan placeholder permanen agar siklus *Spawn / Dismiss* tidak mengaktifkan kembali objek dummy.
