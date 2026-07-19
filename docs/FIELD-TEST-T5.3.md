# Protokol tes lapangan — T5.3 navigasi lintas-lantai (ADR-020)

Dokumen bawa-ke-lokasi. Kunjungan ke RSI itu sumber daya langka (butuh izin & jadwal),
jadi urutannya disusun supaya **kalau waktu habis di tengah, yang sudah dikerjakan tetap
menghasilkan data yang berguna.**

Yang sudah terverifikasi di Editor: deteksi beda lantai, pemilihan lift, pengalihan rute,
deteksi sampai di lift, penolakan menyambung saat lantai belum berubah, penyambungan saat
lantai sudah benar, pembatalan.

**Yang HANYA bisa dijawab di sini:** apakah localize sungguhan berhasil setelah keluar lift,
berapa lama, dan apakah agent benar-benar pindah ke pulau NavMesh lantai baru.

---

## 0. Sebelum berangkat

- [ ] Build APK terpasang di HP, sudah dites buka-tutup di rumah
- [ ] HP terisi penuh + powerbank (localize berulang boros baterai & data)
- [ ] Koneksi data aktif — localize MultiSet butuh server, **tidak bisa offline**
- [ ] Laptop + kabel USB untuk logcat (atau siapkan perekaman log di HP)
- [ ] Izin & janji temu sudah dikonfirmasi; tahu harus lapor ke siapa saat tiba
- [ ] Backend Railway hidup (cek `/api/poi/search` dari HP dulu — trial-nya pernah hampir habis)

**Etika di lokasi.** Ini rumah sakit, bukan lab. Jangan menghalangi jalur IGD atau brankar.
Jangan merekam layar/video di area yang menampilkan wajah pasien atau papan nama pasien —
merekam demo AR di koridor ikut merekam orang di sekitarnya. Kalau ada yang bertanya, jelaskan
dan berhenti kalau diminta. Data tes tidak sepadan dengan satu keluhan privasi.

---

## 1. Rekaman log

```bash
adb logcat -c
adb logcat -s Unity:V | tee darsi-fieldtest-$(date +%H%M).log
```

Yang dicari saat membaca ulang: `FloorTransition`, `FloorVisibilityManager`, `MultiSet`,
`AgentPosition`.

Kalau tidak bisa colok laptop: nyalakan `logChanges` di Inspector sebelum build, dan andalkan
toast di layar + catatan tulis tangan. Lebih baik data kasar daripada tidak ada.

---

## 2. Skenario, urut prioritas

### S1 — Baseline di Ground (wajib)

1. Localize di lantai Ground
2. Buka daftar Destinations

**Catat:**

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Lama localize pertama | — | ___ detik |
| POI Ground tampil jarak | angka, mis. "6 m" | ___ |
| POI Lantai 1 tampil jarak + lantai | mis. "24 m · Lantai 1" | ___ |
| Ada POI selantai yang `Unreachable`? | tidak ada | ___ |

`Unreachable` pada POI **selantai** = masalah NavMesh atau `poiCollider` terlalu jauh dari
permukaan (>1 m). Catat POI mana — itu bug data, bukan bug lintas-lantai.

Bandingkan jarak yang tampil dengan langkah kakimu sendiri. Hitungan Editor: Farmasi 24 m,
IGD 27 m, Resepsionis 26 m, Toilet 17 m, Radiology 47 m, Ruang X-Ray 46 m.

### S2 — Jalur utama: Ground → Farmasi (Lantai 1) (wajib)

1. Pilih `Farmasi`
2. Ikuti rute ke lift — **jangan potong jalan**, biarkan rute yang menuntun
3. Masuk lift, naik ke Lantai 1
4. Keluar lift, **arahkan kamera ke sekitar lobi dan diam**

**Catat:**

| Yang diamati | Ekspektasi | Hasil |
|---|---|---|
| Toast saat memilih | "Farmasi ada di Lantai 1. Anda diarahkan ke lift dulu." | ___ |
| Rute mengarah ke lift, bukan menembus plafon | ya | ___ |
| Toast saat sampai lift | "Anda sampai di lift…" | ___ |
| Status "mencari lokasi…" tampil di dalam lift | ya | ___ |
| **Detik dari keluar lift sampai navigasi menyambung** | — | **___ detik** |
| Rute segmen 2 muncul dan menuntun ke Farmasi | ya | ___ |
| Sampai di Farmasi terdeteksi | ya | ___ |

**Angka paling penting di seluruh tes ini adalah baris tebal itu.** Dia menentukan apakah
`relocalizeTimeout` 90 detik masuk akal, dan apakah `LocalizeFrame()` benar-benar
me-restart jendela background localization SDK (60 detik) — satu-satunya asumsi di T5.3
yang belum terbukti.

Kalau **tidak pernah menyambung**: catat berapa lama menunggu sebelum menyerah, lalu coba
`Cari` → pilih Farmasi lagi dari Lantai 1. Kalau cara itu berhasil, artinya localize-nya
sehat dan yang gagal adalah pemicunya — itu temuan yang sangat spesifik dan berguna.

### S3 — User tidak menuruti instruksi (penting)

Pilih POI Lantai 1, sampai di lift, lalu **jangan naik**. Diam di Ground, pindai sekitar.

Ekspektasi: muncul "Anda masih belum di Lantai 1", navigasi **tidak** menyambung.

Ini pengaman yang paling berbahaya kalau bocor — user diarahkan ke lantai atas padahal
masih di bawah. Sudah terbukti di Editor, tapi di sini dengan tracking AR sungguhan.

### S4 — Jalan keluar (penting)

Saat status "mencari lokasi…", tekan tombol **Stop**.

Ekspektasi: toast "Navigasi dibatalkan.", panel hilang, bisa memilih tujuan baru.

### S5 — Arah sebaliknya

Dari Lantai 1, navigasi ke POI Ground (mis. `Parkir Mobil`). Alurnya simetris.

### S6 — Kalau sempat

- Lift penuh orang → apakah localize setelah keluar terganggu?
- Keluar lift lalu masuk lagi turun ke Ground — apakah state-nya kacau?
- Pilih POI lain saat masih `AwaitingRelocalize` → transisi harus dibatalkan otomatis

---

## 3. Sesudah pulang

- [ ] Simpan file log ke repo/notes, beri nama bertanggal
- [ ] Isi angka-angka di tabel atas, commit dokumen ini terisi
- [ ] Setel `relocalizeTimeout` berdasar S2 (bukan tebakan 90 detik)
- [ ] Kalau ada POI `Unreachable` selantai: betulkan `poiCollider`-nya (lihat ADR-020-B)
- [ ] Kalau localize tidak pernah otomatis: naikkan prioritas rencana cadangan —
      pengulangan `LocalizeFrame()` terjadwal, atau tombol "Saya sudah sampai" manual

---

## Catatan

Angka `relocalizeTimeout` (90 dtk), `retryInterval`, dan ambang `minFloorGap` (1.5 m) semuanya
**belum di-tune** — pola yang sama dengan ADR-019. Tes ini yang menggantikan tebakan dengan
pengukuran.
