# Prompt Riset Lintas-AI — Alternatif Open-Source untuk Viseme Lip-Sync

> Draf untuk direview Bagus. Ditulis PORTABLE untuk ditempel ke berbagai AI
> (ChatGPT, Gemini, Claude, Perplexity, dll) supaya hasilnya bisa dibandingkan.
> TIDAK mengasumsikan AI punya akses ke repo/file/git — semua konteks yang
> dibutuhkan ada di teks ini sendiri.

---

Aku sedang mengembangkan aplikasi AR indoor navigation untuk sebuah rumah sakit
(Unity, target Android). Ada avatar 3D pemandu yang bicara pakai TTS (text-to-speech)
dan gerakan bibirnya (lip-sync) disinkronkan ke suara itu.

**Kondisi sekarang:**
- TTS: `edge-tts` (library Python open-source, gratis, tanpa API key, membungkus
  layanan suara Microsoft Edge). Suara: `id-ID-GadisNeural`, Bahasa Indonesia.
  **edge-tts TIDAK mengembalikan data fonem/viseme sama sekali** — cuma event
  `WordBoundary` (kapan tiap kata mulai/berakhir), tidak ada info bentuk mulut.
- Lip-sync: `uLipSync` (Unity package open-source, MIT, berbasis analisis MFCC dari
  gelombang suara secara real-time) mengklasifikasi audio jadi 5 vokal (A/I/U/E/O)
  dan menggerakkan blendshape wajah avatar sesuai itu.
- **Masalahnya**: `uLipSync` butuh **profil kalibrasi per suara** (rekaman
  referensi tiap vokal dari suara SPESIFIK yang dipakai). Kalau nanti ganti suara
  TTS atau tambah karakter baru, kalibrasi itu harus diulang. Ini SUDAH
  diotomatiskan lewat skrip (bukan lagi kerja manual angka-angka), tapi tetap:
  tiap suara baru = proses kalibrasi baru harus dijalankan.

**Yang aku cari:** pendekatan yang lebih sistematis — viseme yang diturunkan dari
**teks dan aturan fonem bahasa**, bukan dianalisis dari gelombang suara. Idealnya
kalau ganti suara TTS apa pun, viseme-nya tetap akurat tanpa kalibrasi ulang, karena
sumbernya teks+aturan bahasa, bukan sidik jari akustik satu suara tertentu.

**Kenapa BUKAN Azure Speech SDK** (sudah aku pertimbangkan, sudah final): Azure
Speech SDK resmi punya event viseme asli, TAPI bikin akun Azure apa pun (termasuk
tier gratis) wajib kartu kredit/debit non-prepaid untuk verifikasi identitas. Aku
sengaja mau hindari dependensi berbayar/proprietary kalau ada alternatif
open-source yang layak.

**Batasan teknis yang harus dipatuhi rekomendasinya:**
- Server backend TIDAK PUNYA GPU (CPU-only). Solusi yang butuh GPU inference tidak
  layak dipakai di server, kecuali terbukti cukup cepat di CPU murni.
- Server sudah menanggung beban lain (panggilan ke LLM eksternal yang kadang
  lambat), jadi solusi baru jangan menambah beban CPU besar ke proses yang sama,
  dan idealnya menambah latensi respons di bawah ~2 detik per kalimat pendek.
- Target akhirnya Android (mobile) via Unity. Kalau solusinya bisa jalan
  CLIENT-SIDE (langsung di HP, bukan server), itu nilai tambah besar — tapi cek
  dulu apakah memang ada build/binding untuk Android sebelum merekomendasikan,
  jangan asumsikan dari nama proyeknya saja.
- **Lisensi wajib genuinely open-source dan boleh dipakai di aplikasi yang
  di-deploy ke publik** (MIT/Apache/BSD/LGPL aman; kalau GPL, tandai eksplisit
  sebagai catatan karena implikasi copyleft-nya).

**Tolong riset dan bandingkan minimal 3 kandidat berikut** (boleh tambah kandidat
lain kalau memang relevan, tapi jangan lewati ketiga ini tanpa penjelasan kenapa
tidak layak):

1. **Rhubarb Lip Sync** (`github.com/DanielSWolf/rhubarb-lip-sync`, MIT). Tool CLI
   offline: input audio (+opsional teks dialog), output timeline viseme (9 bentuk
   mulut standar animasi). Pertanyaan kunci: seberapa akurat untuk **Bahasa
   Indonesia**? Recognizer fonemnya dilatih untuk Inggris — apakah mode "dialog"
   (butuh model bahasa) masih layak untuk Indonesia, atau cuma mode "sound-only"
   (analisis akustik murni, bahasa-agnostik, lebih kasar) yang realistis? Apakah
   ada build/binding untuk Android?

2. **espeak-ng** (`github.com/espeak-ng/espeak-ng`, **lisensi GPL-3.0**, catat ini
   copyleft). Punya dukungan Bahasa Indonesia untuk grapheme-to-phoneme (G2P) —
   bisa dipakai murni untuk menghasilkan urutan fonem dari teks (tanpa memakai
   suaranya), lalu fonem dipetakan ke viseme lewat tabel tetap. Seberapa akurat
   G2P Indonesia-nya?

3. **Forced alignment** (Montreal Forced Aligner atau sejenis, berbasis
   wav2vec2-CTC). Kalau sudah punya teks jawaban DAN audio hasil TTS-nya, forced
   alignment bisa mencocokkan tiap fonem/kata ke waktu presisi di audio. Apakah
   ada model pretrained untuk Bahasa Indonesia? Seberapa berat dijalankan di CPU
   (bukan GPU) untuk klip pendek (~5-15 detik)?

**Kalau kamu punya kemampuan menjalankan kode/tool** (code execution, sandbox),
tolong benar-benar coba jalankan/verifikasi klaim akurasi, bukan cuma mengutip
dokumentasi/marketing tool-nya — proyek ini sudah beberapa kali kena masalah dari
klaim "seharusnya jalan" yang ternyata salah begitu benar-benar dicoba. Kalau kamu
TIDAK punya kemampuan itu, tolong tulis eksplisit bagian mana yang cuma berdasar
dokumentasi/sumber tertulis vs yang benar-benar kamu uji, jangan disamarkan seolah
sudah diverifikasi.

**Format jawaban yang aku mau:**

```
## Ringkasan Eksekutif
(1 paragraf: rekomendasi utama + alasan singkat)

## Kandidat 1: Rhubarb Lip Sync
- Diverifikasi dengan eksekusi nyata? ya/tidak
- Akurasi untuk Bahasa Indonesia:
- Dukungan Android:
- Lisensi:
- Estimasi biaya integrasi (server-side/client-side, perkiraan latensi):

## Kandidat 2: espeak-ng
(struktur sama)

## Kandidat 3: Forced Alignment
(struktur sama)

## Kandidat tambahan (kalau ada)

## Perbandingan
(tabel: kandidat x akurasi x kelayakan Android x lisensi x beban komputasi)

## Rekomendasi
(pilihan konkret dengan alasan; kalau semua kandidat tidak layak, katakan itu juga
dan jelaskan kenapa tetap pakai kalibrasi per-suara yang sekarang lebih masuk akal)

## Pertanyaan terbuka yang butuh keputusanku
```
