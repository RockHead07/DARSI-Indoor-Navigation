# Protokol tes lapangan — Avatar Suara & Lip-Sync (ADR-034, ADR-037, ADR-038)

Dokumen bawa-ke-lokasi, format sama seperti `FIELD-TEST-T5.3.md` dan
`FIELD-TEST-RAG-ASSISTANT.md`.

Yang sudah terverifikasi (Play Mode, git, Unity Editor hidup — bukan klaim di kertas):
lip-sync driver (ADR-037), pemutaran audio TTS asli dari backend, isolasi kegagalan
`/tts`, dan pemicu `AIAvatarGuideController.StartLeading()` — **semuanya di scene
sandbox terisolasi** (`Sandbox_AvatarCompanion.unity`), bukan scene navigasi
sungguhan.

---

## ⚠️ 0. Prasyarat yang BELUM selesai — baca ini dulu

**Checklist ini BELUM BISA dijalankan apa adanya.** `Sandbox_AvatarCompanion.unity`
dipakai untuk membuktikan tiap komponen bekerja secara terisolasi, dan sengaja **tidak
punya SDK MultiSet/VPS sama sekali** (dicek langsung: nol referensi
`MultiSetSdk`/`NavigationController` di scene itu) — jadi tidak ada rute AR sungguhan
untuk dijalani.

Sebelum ke lokasi, kerjakan dulu (bisa manual atau prompt Antigravity baru):

- [ ] Tambahkan `AvatarAudioClient` + `AvatarSpeechLipSync` (+ referensi VRM/audio
      yang dibutuhkan) ke scene navigasi sungguhan (`TestingHCM.unity`, yang sudah
      punya satu referensi `AIAvatarGuideController` — cek apakah itu instance yang
      sama atau perlu disambungkan ulang).
- [ ] Isi field `showPath`/`navigation`/`floorTransition` di `AIAvatarGuideController`
      pada scene itu — di sandbox field-field ini masih kosong (`None`), itu sebabnya
      `docs/AI-AVATAR-ASSISTANT.md` masih menandai "belum tersambung ke scene produksi".
- [ ] Build APK dari scene yang sudah tersambung itu, bukan dari sandbox.
- [ ] Konfirmasi `AssistantClient.baseUrl` di scene itu masih menunjuk Named Tunnel
      permanen (`https://api-darsi.rockhead07.tech`).

**Begitu prasyarat ini selesai, baru lanjut ke §1 dan seterusnya.**

---

## 1. Sebelum berangkat (setelah prasyarat §0 selesai)

- [ ] Build APK terbaru terpasang di HP, sudah dites buka-tutup di rumah
- [ ] Server produksi hidup: `curl https://api-darsi.rockhead07.tech/health` → `{"ok":true}`
- [ ] HP terisi penuh + powerbank — audio TTS + lip-sync + AR + VPS sekaligus boros baterai
- [ ] Headphone/earphone dibawa — speaker HP di koridor RS ramai bisa bikin sulit menilai
      kejelasan suara asisten
- [ ] Uji WiFi lokasi DAN data seluler (sama seperti `FIELD-TEST-RAG-ASSISTANT.md` §5) —
      TTS juga lewat Bifrost/Groq/edge-tts, ikut kena soal jaringan yang sama
- [ ] Laptop + kabel USB buat logcat

**Etika di lokasi.** Sama seperti dua dokumen field-test sebelumnya: ini rumah sakit
sungguhan. Avatar yang bicara + berjalan bisa menarik perhatian pasien/staf — uji di
area yang tidak mengganggu jalur IGD/brankar, dan berhenti kalau diminta petugas.

---

## 2. Rekaman log

```bash
adb logcat -G 16M
adb logcat -c
# ... tes keliling ...
adb logcat -d > darsi-avatar-fieldtest-YYYYMMDD.log
```

Yang dicari: `AvatarAudioClient`, `AvatarSpeechLipSync`, `AIAvatarGuideController`,
`[Voice]`. Baris yang menyebut fallback TTS (kalau `edge-tts` gagal di lapangan) atau
`StartLeading` yang tidak pernah muncul (lead-follow gagal terpicu) adalah temuan penting.

---

## 3. Skenario, urut prioritas

### S1 — Baseline: bicara lalu berjalan (wajib)

Ucapkan pertanyaan sederhana ("Toilet dimana") lewat mic.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Avatar bicara (audio terdengar) | ya | ___ |
| Mulut bergerak mengikuti suara (bukan diam/statis) | ya | ___ |
| Avatar TIDAK mulai berjalan sebelum selesai bicara | ya — ini urutan yang dikunci ADR-038 | ___ |
| Setelah selesai bicara, avatar mulai memimpin rute | ya | ___ |
| Rute yang diikuti benar menuju tujuan (bukan cuma animasi jalan di tempat) | ya | ___ |

**Kalau avatar mulai jalan SEBELUM selesai bicara**, atau bicara tapi tidak pernah
mulai jalan: itu regresi dari urutan FSM yang sudah dibuktikan di sandbox — catat
persis kapan itu terjadi.

### S2 — Isolasi kegagalan TTS di lapangan (penting)

Sudah terbukti di sandbox lewat port mati simulasi. Di lapangan, kegagalan lebih
mungkin datang dari jaringan asli (bukan simulasi) — matikan data/WiFi sesaat setelah
mengajukan pertanyaan, atau uji di area sinyal lemah.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Jawaban teks tetap tampil di layar walau suara gagal | ya | ___ |
| Navigasi tetap menyala walau suara gagal | ya | ___ |
| Tidak ada crash/freeze | ya | ___ |

### S3 — Latensi yang benar-benar dirasakan (penting)

Ucapkan pertanyaan, ukur dari selesai bicara sampai avatar mulai menjawab dengan suara
(bukan cuma teks muncul — TTS-nya sendiri butuh waktu sintesis di atas latensi Bifrost
12-32 detik yang sudah diketahui).

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Total waktu tunggu (bicara selesai → jawaban bersuara mulai) | dicatat, belum ada angka lapangan sebelumnya | ___ detik |
| Ada indikasi visual "sedang berpikir/menyiapkan suara" selama menunggu | kalau tidak ada, ini gap UX nyata | ___ |

### S4 — Frame rate & panas HP (kalau sempat)

Avatar (VRM + lip-sync Burst + Animator) berjalan bersamaan dengan ARCore + VPS
MultiSet. Ini pertama kalinya beban gabungan ini diukur di device fisik.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| FPS terasa stabil (tidak patah-patah) saat avatar bicara+jalan bersamaan | ya | ___ |
| HP terasa panas berlebih setelah ~10 menit pemakaian terus-menerus | tidak | ___ |

---

## 4. Sesudah pulang

- [ ] Simpan log, beri nama bertanggal
- [ ] Isi tabel di atas, commit dokumen ini terisi
- [ ] Kalau S1 gagal (urutan bicara→jalan terbalik atau lead-follow tidak terpicu):
      prioritas utama sesi berikutnya, di atas S2-S4
- [ ] Kalau S3 menunjukkan latensi total terlalu lama buat UX nyata (gabungan Bifrost +
      sintesis TTS): itu temuan untuk revisit keputusan hybrid tier ADR-033, bukan bug kode

---

## Catatan

Ini pengujian PERTAMA untuk seluruh Fase 2 (suara + lip-sync + koordinasi lead-follow)
lewat perangkat fisik. Semua yang "terverifikasi" di ADR-037/038 itu lewat sandbox
terisolasi — device fisik + scene navigasi sungguhan + lingkungan RS nyata adalah tiga
lapisan yang belum pernah diuji bersamaan sama sekali.
