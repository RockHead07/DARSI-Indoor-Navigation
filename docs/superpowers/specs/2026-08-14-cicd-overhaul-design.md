# Design Spec: Enterprise CI/CD Overhaul untuk DARSI-Indoor Navigation

## 1. Latar Belakang & Objektif
Meningkatkan stabilitas, ekstensibilitas, dan kapabilitas debugging dari alur kerja GitHub Actions saat ini. Pipeline lama masih monolitik dan rentan gagal (misal karena *storage* penuh) serta sulit di-debug (tidak ada ekstraksi log otomatis).

## 2. Arsitektur Reusable Workflows (Modularisasi)
Mengadopsi prinsip DRY (Don't Repeat Yourself) dengan memecah workflow.

### A. Modul Reusable (`.github/workflows/_*.yml`)
File-file ini tidak dijalankan langsung, melainkan dipanggil oleh *triggers*:
- `_unity-tests.yml`: Menjalankan EditMode dan PlayMode Tests menggunakan Game-CI.
- `_unity-build-uaal.yml`: Menjalankan ekspor Android UaaL menggunakan Game-CI.
- `_backend-tests.yml`: Menjalankan linting (Ruff) dan testing (PyTest) Python.

### B. Trigger Workflows (`.github/workflows/*.yml`)
Entry-point yang mendengarkan *event* dari GitHub:
- `pr-validation.yml`: Aktif pada *Pull Request*. Menjalankan `_unity-tests.yml` dan `_backend-tests.yml` secara paralel. Tujuannya sebagai *Quality Gate*.
- `release-main.yml`: Aktif saat Push/Merge ke `main`. Menjalankan `_unity-tests.yml`, jika lulus, lanjut ke `_unity-build-uaal.yml`, dan otomatis mentrigger integrasi ke Flutter (`MyRSIy`).

## 3. Daftar Best Practices yang Akan Diimplementasikan
1. **Free Disk Space Optimization**: Menyisipkan `jlumbroso/free-disk-space` sebelum fase Build Android untuk memberikan tambahan ~30GB penyimpanan pada runner `ubuntu-latest`.
2. **Automated Error Log Upload**: Menambahkan step khusus `if: failure()` untuk mem-bundle dan mengunggah `Editor.log` (log bawaan Unity) sebagai GitHub Artifact.
3. **Strict Cache Isolation**: Membedakan kunci *cache* (`restore-keys`) secara tegas antara build target `Android` dan `Standalone` (EditMode/PlayMode) agar tidak terjadi konflik pustaka Unity.
4. **Automated Dependency Updates (Dependabot)**: Menambahkan konfigurasi `.github/dependabot.yml` agar *version tags* dari GitHub Actions yang kita gunakan (`actions/checkout@v4`, `actions/cache@v4`, dll) otomatis di-maintenance oleh GitHub.
5. **Quality Gate Validation**: Menambahkan langkah PlayMode Tests ke dalam eksekusi test (sebelumnya hanya EditMode).

## 4. Pekerjaan Mendatang (Future Work)
Fitur-fitur ini **tidak** diimplementasikan pada fase ini, namun tercatat untuk *roadmap* selanjutnya:
- **Notification Alerts**: Integrasi webhook untuk mengirim laporan status sukses/gagal ke grup komunikasi developer (seperti Slack, Discord, atau Telegram) setelah tim menyepakati platform mana yang akan digunakan.
- **Branch Protection Rules**: Konfigurasi sisi GitHub Repository (via Web UI) untuk mengunci *branch* `main` dan `develop`, sehingga PR wajib berstatus "Passed" pada `pr-validation.yml` sebelum tombol Merge bisa diklik.
- **Static Code Analysis C#**: Menambahkan integrasi SonarQube atau Roslyn Analyzers lanjutan jika kompleksitas kode C# meningkat.
