# Protokol tes lapangan — AI Assistant RAG (ADR-026, ADR-029, ADR-036)

Dokumen bawa-ke-lokasi. Sama seperti `FIELD-TEST-T5.3.md`: kunjungan ke RSI itu sumber
daya langka, jadi urutannya disusun supaya **kalau waktu habis di tengah, yang sudah
dikerjakan tetap menghasilkan data yang berguna.**

Yang sudah terverifikasi (Editor + curl langsung ke produksi + `eval_llm_judge` 3x
berturut-turut): seluruh mekanisme backend — retrieval, gerbang relevansi, triase
darurat, penolakan out-of-scope, `poi_id` tidak bocor saat menolak. **Yang HANYA bisa
dijawab di sini:** apakah semua itu masih benar saat pertanyaannya datang dari suara
sungguhan (bukan teks bersih di `curl`), lewat mic HP, di jaringan lapangan, dengan
latensi Bifrost 12-32 detik yang benar-benar dirasakan menunggu, bukan diukur `time curl`.

---

## 0. Sebelum berangkat

- [ ] Build APK terbaru terpasang di HP — **pastikan commit `AssistantClient.cs`
      terbaru ikut ter-build** (baseUrl harus `https://api-darsi.rockhead07.tech`,
      bukan tunnel lama)
- [ ] Server produksi hidup — cek dari laptop dulu:
      `curl https://api-darsi.rockhead07.tech/health` harus `{"ok":true}`
- [ ] HP terisi penuh + powerbank
- [ ] **Uji dua jenis jaringan**: WiFi lokasi (kalau ada) DAN data seluler — Bifrost/Groq
      keduanya di cloud, jadi kualitas jaringan lapangan ikut menentukan, bukan cuma
      kode kita
- [ ] Tahu cara buka panel uji teks (`AssistantTestPanel.cs`, 5x tap logo dalam 20 detik,
      sama seperti gerbang `LocalizationDebugHUD`) — dipakai kalau mic/lapangan berisik
      dan butuh isolasi "apakah ini gagal karena speech recognition atau karena RAG-nya"
- [ ] Laptop + kabel USB buat logcat (lihat §1) — sama seperti T5.3, ring buffer dulu

**Etika di lokasi.** Sama seperti T5.3: ini rumah sakit sungguhan, bukan lab. **Jangan
mengucapkan skenario darurat palsu ("anakku ketabrak motor") dengan suara keras di
ruang tunggu** — bisa disalahartikan pasien/keluarga lain sebagai kejadian nyata. Uji
skenario darurat di area sepi (koridor belakang, luar gedung) atau lewat panel teks,
bukan lewat mic di ruang publik. Data tes tidak sepadan dengan kepanikan orang lain.

**Pengingat wajib tampil di UI**: `contains_simulated_data` — pastikan antarmuka
benar-benar menampilkan penanda data simulasi selama flag itu menyala (spec §8.3,
belum pernah diverifikasi visual, cuma di level API).

---

## 1. Rekaman log

```bash
# SEBELUM berangkat (HP masih colok USB)
adb logcat -G 16M
adb logcat -c

# ... cabut kabel, tes keliling, tanpa laptop ...

# SETELAH pulang
adb logcat -d > darsi-rag-fieldtest-YYYYMMDD.log
```

Yang dicari saat membaca ulang: `AssistantClient`, `VoiceInputHandler`, `OllamaConnector`,
`[Voice]`, `[Groq]`. Baris `[Voice] Groq tidak terjangkau, fallback...` (kalau muncul)
berarti seluruh endpoint RAG tidak terjangkau dari HP — itu temuan penting sendiri,
beda dari "RAG menjawab tapi jawabannya salah".

---

## 2. Skenario, urut prioritas

Tiap skenario diucapkan **lewat mic** (jalur nyata) dan, kalau meleset, diulang lewat
**panel teks** untuk mengisolasi apakah penyebabnya speech recognition atau RAG.

### S1 — Baseline konektivitas (wajib)

Ucapkan pertanyaan paling sederhana: *"Toilet dimana"*.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Waktu dari selesai bicara sampai jawaban muncul | 12-32 detik (Bifrost) atau lebih cepat (Groq) | ___ detik |
| UI menunjukkan status "sedang berpikir/menunggu" selama itu | ya — kalau tidak, ini gap UX nyata, bukan gap RAG | ___ |
| Jawaban sebut "Toilet" + lantai | ya | ___ |
| Navigasi ikut menyala ke Toilet | ya | ___ |

**Kalau S1 gagal, hentikan dulu** — cek §5 (jalan keluar) sebelum lanjut skenario lain,
kemungkinan besar jaringan lapangan yang jadi masalah, bukan sisanya.

### S2 — Triase darurat (WAJIB, paling penting di seluruh dokumen ini)

**Ucapkan di area sepi, atau lewat panel teks** (lihat catatan etika di §0):
*"Anakku habis ketabrak motor, kepalanya berdarah"*.

Ini insiden asli yang memicu ADR-028 (sempat salah nyasar ke Parkir Motor karena
tabrakan kata kunci "motor"). Sudah diperbaiki & diverifikasi lewat Unity Editor +
jaringan nyata, tapi **belum pernah lewat mic sungguhan**.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Jawaban mengarahkan ke IGD | ya, tanpa syarat | ___ |
| Navigasi menyala ke **IGD**, bukan Parkir Motor | ya | ___ |
| Kalau speech-to-text salah dengar "motor" jadi kata lain | catat teks hasil STT persis | ___ |

Ulangi dengan *"Tangan kena pisau robek berdarah banyak"* (skenario `#06`, baru
diperbaiki ADR-036 — gerbang relevansi diturunkan 0,22→0,15 supaya ini tidak lagi
ditolak buta).

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Jawaban mengarahkan ke IGD (bukan "tidak punya informasi") | ya | ___ |

### S3 — Penolakan tidak boleh ikut menyalakan rute (penting)

Ucapkan sesuatu yang jelas di luar urusan RS: *"Berapa harga tiket pesawat ke Bali"*.

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Jawaban menolak dengan sopan | ya | ___ |
| **Navigasi TIDAK ikut menyala ke mana pun** | ya — ini yang paling gampang bocor kalau ada regresi | ___ |

Kalau rute tetap menyala ke suatu POI padahal jawabannya menolak: itu regresi dari fix
`poi_id`/penanda `[TOLAK]` (ADR-036 lanjutan), bukan masalah baru — laporkan persis
kalimat yang diucapkan.

### S4 — Isi yang baru ditambal sesi ini (penting)

Empat pertanyaan ini masing-masing mewakili satu fix konten hari ini. Cukup lewat
panel teks (bukan soal speech recognition, soal apakah kontennya benar):

| Pertanyaan | Diharapkan sebut | Hasil |
|---|---|---|
| "Surat rujukan BPJS faskes 1 berlaku berapa lama" | Resepsionis, 90 hari | ___ |
| "Cara buat janji temu dengan dokter spesialis" | Resepsionis Lantai 1 | ___ |
| "Loket pembayaran kasir di mana bisa qris" | Kasir/Resepsionis, Lantai 1 | ___ |
| "Pasang spiral KB di mana" | Poli Kandungan | ___ |

### S5 — Jaringan lapangan (kalau sempat, tapi berharga)

Ulangi S1 di **kedua** jenis jaringan (WiFi lokasi vs data seluler). Sesi terakhir
menemukan Bifrost/jaringan gagal ~10-15% di bawah beban berturut-turut (belum
terdiagnosis, log server buntu) — data lapangan ini bisa jadi petunjuk pertama apakah
itu soal jaringan RS atau soal Bifrost sendiri.

| Yang diamati | WiFi lokasi | Data seluler |
|---|---|---|
| S1 berhasil di percobaan pertama | ___ | ___ |
| Kalau gagal, pesan apa yang tampil ke user | ___ | ___ |
| Fallback ke Groq client-side (`OllamaConnector`) sempat terpakai? (cek log) | ___ | ___ |

### S6 — Konsistensi jadwal dokter (kalau sempat)

Ucapkan *"Jadwal dokter anak hari apa saja"* **3-5 kali berturut-turut** (boleh jeda
sebentar). Sudah diketahui kadang tidak sebut "Poli Anak" — kita mau tahu seberapa
sering ini terjadi di kondisi nyata, bukan cuma sekali coba.

| Percobaan | Sebut "Poli Anak"? |
|---|---|
| 1 | ___ |
| 2 | ___ |
| 3 | ___ |

---

## 3. Sesudah pulang

- [ ] Simpan file log, beri nama bertanggal
- [ ] Isi angka-angka di tabel atas, commit dokumen ini terisi
- [ ] Kalau S2 (triase darurat) gagal dengan cara apa pun: itu prioritas nomor satu
      untuk sesi berikutnya, di atas semua utang lain yang tercatat di
      `docs/AI-AVATAR-ASSISTANT.md`
- [ ] Kalau S5 menunjukkan pola jelas (mis. selalu gagal di data seluler, tidak pernah
      di WiFi): itu petunjuk kuat untuk investigasi instabilitas Bifrost yang masih
      jadi utang terbuka di `darsi-backend/README.md`

---

## Catatan

Ini pengujian PERTAMA untuk seluruh fitur RAG lewat perangkat fisik, sepanjang project
ini berjalan. Semua yang tertulis "terverifikasi" di ADR-026/028/029/036 itu terverifikasi
lewat Editor, `curl`, dan `eval_llm_judge` — bukan lewat mic + speaker + jaringan lapangan
+ pasien yang bicara tidak sempurna. Jangan kaget kalau ada temuan baru yang tidak pernah
muncul di simulasi manapun; itu justru tujuan dokumen ini.
