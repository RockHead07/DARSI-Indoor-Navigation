# Avatar Animation Test Harness — Catatan & Rencana

> Status: disepakati scope 1-3, MENUNGGU persetujuan eksplisit sebelum eksekusi.
> Ditulis 2026-09-03 atas permintaan pemilik project ("note all of this first,
> then we'll execute with my consent").

## Kenapa dokumen ini ada

Model gerak pemandu baru saja diganti total (commit `62b9f3c`, model "lompat-tunggu"
menggantikan "tali kekang") dan animasi Mixamo baru baru saja dipasang. Keduanya
BELUM pernah dijalankan sungguhan. Sebelum menyetel angka apa pun, perlu disepakati
DULU cara mengujinya -- karena cara uji yang salah menghasilkan angka yang terasa
benar tapi tidak berlaku di device.

## Koreksi penting: apa yang SEBENARNYA best practice

Pemilik project sempat bertanya apakah "mode game tester WASD" adalah best practice.
Jawaban jujurnya: **bukan, itu tier observasi, bukan tier keputusan.**

**Kelemahan inti uji manual WASD: tidak reproducible.** Setiap run kita berjalan
sedikit berbeda, jadi begitu satu parameter diubah dan hasilnya "terasa lebih baik",
tidak ada cara membuktikan perbaikannya datang dari parameter itu, bukan dari cara
jalan yang kebetulan berbeda. Semua keputusan tuning yang diambil begitu = perasaan,
bukan bukti.

Urutan sesungguhnya dari fidelitas terendah ke tertinggi:

| Tier | Metode | Kegunaan sebenarnya |
|---|---|---|
| Observasi | Slow-mo, frame-step, HUD, kecepatan free-cam realistis | Melihat & mendiagnosis artefak |
| **Keputusan** | **Replay deterministik (rekam sekali, putar ulang identik)** | **Membuat perbandingan A/B jadi sah** |
| Fidelitas tertinggi | Replay dari pose trace device sungguhan | Gerak & jitter manusia asli, bukan simulasi |
| Objektif | Metrik terukur (foot-slide, speed jerk, clamp hits) | Angka, bukan kesan |

Poin 1-3 di bawah TIDAK menggantikan replay deterministik. Tapi juga tidak sia-sia:
HUD dan kecepatan jalan realistis tetap dibutuhkan saat replay dipakai nanti.

## Disepakati untuk dikerjakan (menunggu persetujuan eksekusi)

1. **Slow-mo + frame-step.** `Time.timeScale = 0.25` plus tombol pause/step bawaan
   Unity. Nol baris kode, cuma cara pakai. Artefak yang tak terlihat di kecepatan
   penuh jadi kelihatan.
2. **HUD status pemandu.** Menampilkan `GuideState` (LeadingPath / WaitingForUser /
   Chasing / ArrivalPointing), kecepatan saat ini, jarak ke pengguna, jarak ke
   waypoint. Pola `OnGUI` yang sama dengan `SimpleSandboxFreeCam`. Tanpa ini,
   "kelihatan aneh" tidak bisa didiagnosis jadi "nyangkut di Chasing karena X".
   Catatan: `AvatarSandboxUI` yang sudah ada menargetkan `AvatarCompanionController`,
   BUKAN `AIAvatarGuideController` -- jadi tidak bisa dipakai apa adanya.
3. **Perbaiki kecepatan free-cam.** `SimpleSandboxFreeCam.moveSpeed` saat ini `3.5`,
   sekitar 2,5x kecepatan jalan manusia (~1,3 m/s). Menguji "avatar terlalu cepat"
   sambil PENGUJINYA bergerak 2,5x kecepatan wajar adalah cacat metodologi: pada
   kecepatan itu jalur catch-up/Chasing hampir selalu aktif dan ritme memimpin
   normal nyaris tidak pernah terlihat. Turunkan ke ~1,3, sprint dipakai untuk kasus
   "pengguna jalan cepat".

## Sengaja DITUNDA (bukan ditolak)

4. **Kunci Y kamera ke tinggi ponsel.** Free-cam sekarang bisa terbang (E/Q/Space/
   Ctrl tanpa batas). Menilai kehalusan animasi dari sudut pandang burung bukan
   representasi ponsel setinggi dada.
5. **Toggle jitter tracking.** Offset acak kecil per frame meniru derau pose AR.
   Kode ini sudah punya catatan eksplisit soal `userS` bergerigi karena derau
   posisi pengguna -- WASD memberi sinyal yang JAUH lebih bersih dari produksi.
6. **Replay deterministik.** Rekam satu trace gerakan, putar ulang identik untuk tiap
   set parameter. INI yang membuat keputusan tuning sah. Bahan dasarnya sudah pernah
   ada di project (`ProbeUserWalker`).
7. **Pose trace dari device.** Rekam pose kamera AR sungguhan di HP, putar di Editor.
   Menghapus masalah "WASD tidak bergerak seperti manusia" sepenuhnya -- termasuk
   jitter asli, bukan tebakan.
8. **Metrik terukur.** Foot-slide (kecepatan tulang telapak kaki saat menapak,
   idealnya ~0), speed jerk (delta `_currentSpeed` antar frame -- inilah artefak
   "tiba-tiba lari" dalam bentuk angka), dan seberapa sering rasio di `Drive()`
   mentok clamp 0,4/1,8 (mentok = ketidakcocokan yang terlihat mata).

## Pekerjaan animasi: selesai vs terbuka

**Selesai & terverifikasi lewat Editor hidup:**
- 4 FBX Mixamo baru: rig humanoid disalin dari `IdleAvatar` (`hasExtraRoot` yang
  tadinya beda sudah cocok), klip dinamai + `loopTime` menyala (`Idle` 0-499,
  `Walk` 0-29, `SlowRun` 0-22, `MediumRun` 0-17).
- `AvatarGuide.controller`: BlendTree `Locomotion` sekarang memakai klip Idle/Walk
  yang baru.

**Masih terbuka:**
- `walkClipSpeed` sesungguhnya. `averageSpeed` klip baru terbaca ~0 lewat refleksi
  (klip lama: `1,59`). Sudah dicoba flag `keepOriginalPositionXZ`, tidak berpengaruh.
  Penyebabnya belum diketahui pasti -- dugaan: artefak "Copy From Other Avatar".
  Threshold BlendTree masih di angka lama `1.588656`; angka ini dan `walkClipSpeed`
  di C# HARUS diubah bersamaan.
- ~~`SlowRun`/`MediumRun` belum disambungkan sama sekali.~~ **SELESAI**:
  `SlowRun` ditambahkan ke BlendTree `Locomotion` di threshold `1.8` (menyamai
  `chaseSpeed` default). Nol perubahan C# -- `Drive()` sudah menulis parameter
  `Speed` yang sama, BlendTree 3-node otomatis blend Idle->Walk->Run. Angka
  threshold `1.8` starting point, tune lewat fake-route rig. `MediumRun` masih
  belum dipakai (tidak ada state yang butuh kecepatan itu saat ini).
  Keterbatasan jujur: rasio penyelarasan kecepatan putar klip di `Drive()`
  (`speed / walkClipSpeed`) cuma dikalibrasi untuk Walk, bukan Run -- kaki bisa
  sedikit selip saat blend penuh ke Run. Belum diperbaiki, dicatat sebagai
  utang kecil.
- State `Point`/`PointDir` + 3 klip Pointing sekarang yatim (rewrite `62b9f3c`
  berhenti memakainya). Hapus atau biarkan?

## Belum di-commit / belum di-push

- `AvatarGuide.controller` termodifikasi; folder FBX baru belum ter-track.
  (Perlu diputuskan juga: apakah biner Mixamo memang layak masuk git?)
- `62b9f3c` (redesign gerak lompat-tunggu) sudah di-commit tapi **belum di-push dan
  belum pernah dijalankan sama sekali** -- baru lolos compile check. Ini item paling
  berisiko di daftar mana pun: satu file inti ditulis ulang total tanpa pernah jalan.

## Terparkir dari sesi sebelumnya

- Rencana implementasi Cari Teman: spec sudah disetujui & di-push, tapi
  `writing-plans` tidak pernah selesai (terpotong oleh bug avatar).
- Uji ulang device untuk 3 perbaikan yang belum terverifikasi: peredaman kecepatan,
  penutupan kebocoran `autoConnect` yang sesungguhnya, idempotensi `StartLeading`.
- Wajah pucat di Editor: belum dikonfirmasi apakah muncul juga di device.
