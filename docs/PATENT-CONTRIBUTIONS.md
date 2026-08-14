# Catatan Kontribusi — Paten HKI

**Judul draft paten:** METODE PERUBAHAN RUTE DINAMIS PADA NAVIGASI DALAM RUANGAN
BERDASARKAN TINGKAT KEPADATAN MANUSIA (pendaftaran DJKI)

**Tujuan dokumen ini:** catatan bertanggal soal siapa mengerjakan apa di project
**Indoor Navigation**, untuk mendukung penyusunan bagian inventor/kontributor pada
dokumen paten. Ditulis 2026-08-14, berdasarkan roster tim dari Irawan PSDKU.

---

## Tim project Indoor Navigation

| Nama | Peran | Bukti kerja |
|---|---|---|
| **Aris** | UI/FE Designer | — (di luar cakupan repo yang diaudit dari sisi ini) |
| **Hafiz** | AI Engineer — deteksi kerumunan | `Backend/yolo_api.py` (repo ini) — YOLOv8n, endpoint `/api/human` |
| **Bagus** | AR & Indoor Positioning Engineer (Navigation Engineer) | Lihat rincian di bawah |

> Catatan: ini roster **project Indoor Navigation**, berbeda dari tim **Darsi-manager**
> (Andharu-Frontend, Irawan-AI, Yardan-Backend) yang disebut di pesan yang sama — dua
> project terpisah, jangan tertukar.

## Cakupan kerja Bagus (didokumentasikan lewat repo ini + repo WebXR)

Dipetakan ke alur yang disebut draft paten
(SIMRS → Crowd Estimation → AI Dynamic Weight Generator → Modified A* → Visual
Positioning → Continuous Re-routing):

- **Visual Positioning** — integrasi VPS MultiSet SDK (repo ini) dan Immersal SDK
  (evaluasi `DarsiNavigasi0.1`), termasuk kalibrasi lokalisasi dan diagnosis
  ketidakstabilan anchor
- **Modified A* / Continuous Re-routing** — pathfinding navgraph, deteksi lantai
  (`floorOf`/clustering), gerbang navigasi lintas-lantai, desain gerbang konsensus
  anchor localize (repo WebXR, dirancang 2026-08-14)
- **Bukan bagian Bagus:** Crowd Estimation (`Backend/yolo_api.py`) — itu kontribusi
  Hafiz

## Celah arsitektur yang belum ada pemiliknya

**AI Dynamic Weight Generator** — komponen penghubung antara deteksi kerumunan (Hafiz)
dan mesin rute (Bagus). Per audit teknis sebelumnya terhadap kode yang ada:
**TIDAK ADA DI KODE**. Ini titik kerja berikutnya yang relevan untuk klaim paten
"perubahan rute dinamis berdasarkan kepadatan" — tanpa komponen ini, klaim tersebut
belum punya implementasi yang menghubungkan dua ujungnya.

## Catatan

Dokumen ini mencatat pembagian peran untuk keperluan dokumentasi kontribusi/inventor.
Bukan dokumen hukum, bukan draft deskripsi paten itu sendiri — deskripsi teknis paten
disusun terpisah (lihat laporan teknis yang sudah disampaikan sebelumnya di sesi kerja
dengan asisten, berdasarkan audit langsung ke kode).
