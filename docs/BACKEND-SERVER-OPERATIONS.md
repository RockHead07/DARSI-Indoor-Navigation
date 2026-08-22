# Backend Server Operations & Tunnel Guide — DARSI

Dokumen ini mencatat konfigurasi, prosedur deployment, serta panduan operasional server agar backend DARSI dan Cloudflare Tunnel tetap berjalan terus-menerus (24/7 background service) tanpa mati saat sesi SSH ditutup.

---

## 1. Menjalankan Backend DARSI (Docker Compose)

Backend FastAPI dan PostgreSQL (pgvector) dijalankan menggunakan Docker Compose dalam mode *detached* (`-d`):

```bash
cd ~/bagusProjects/Indoor-Navigation/DARSI-Indoor-Navigation-Backend
docker compose up -d --build
```

### Perintah Penting Docker:
- **Cek status container:** `docker compose ps`
- **Lihat log FastAPI:** `docker compose logs -f api`
- **Restart backend:** `docker compose restart api`
- **Matikan backend:** `docker compose down`

> **Note:** Konfigurasi `restart: unless-stopped` pada `docker-compose.yml` memastikan container otomatis menyala kembali jika server reboot atau crash.

---

## 2. Menjalankan Cloudflare Ingress (Tunnel)

### Metode A: Quick Tunnel via `tmux` (Sementara / Development)

Saat menjalankan `cloudflared tunnel --url http://localhost:8000` secara langsung di terminal SSH biasa, proses akan mati begitu jendela terminal ditutup (**SIGHUP**), menyebabkan error `DNS_PROBE_FINISHED_NXDOMAIN` karena Cloudflare langsung menghapus domain sementara tersebut.

Untuk mengatasinya, gunakan **`tmux`**:

#### 1. Buat / Masuk ke Sesi `tmux`:
```bash
# Jika sesi baru:
tmux new -s darsi-tunnel

# Jika sesi sudah ada sebelumnya:
tmux attach -t darsi-tunnel
```

#### 2. Jalankan Tunnel di dalam `tmux`:
```bash
cloudflared tunnel --url http://localhost:8000
```
*Salin URL yang muncul, contoh: `https://xxxx-xxxx.trycloudflare.com`.*

#### 3. Lepas Terminal (*Detach*):
- Tekan kombinasi tombol: **`Ctrl + B`** (lalu lepas).
- Tekan tombol: **`D`**.
- Output: `[detached (from session darsi-tunnel)]`.

> **Catatan Quick Tunnel:** URL akan berganti setiap kali proses `cloudflared` di-restart. Untuk jangka panjang/produksi, gunakan Metode B.

---

### Metode B: Cloudflare Zero Trust Named Tunnel (Permanen & Auto-Start)

Metode ini memberikan subdomain permanen (misalnya `api-darsi.domain.com`) dan berjalan otomatis sebagai systemd service Linux (tidak perlu `tmux` atau login SSH).

1. Buka [Cloudflare Zero Trust Dashboard](https://one.dash.cloudflare.com/) > **Networks** > **Tunnels**.
2. Klik **Create a Tunnel** > Pilih **Cloudflared**.
3. Beri nama (contoh: `darsi-backend`).
4. Salin perintah instalasi service untuk Linux (Ubuntu/Debian):
   ```bash
   sudo cloudflared service install <TUNNEL_TOKEN>
   sudo systemctl enable --now cloudflared
   ```
5. Pada menu **Public Hostnames**, arahkan domain ke:
   - **Service:** `HTTP`
   - **URL:** `localhost:8000`

---

## 3. Troubleshooting & Cheatsheet

### 🔴 Masalah: `DNS_PROBE_FINISHED_NXDOMAIN`
- **Penyebab:** Proses `cloudflared` mati karena SSH ditutup, atau URL sementara sudah expired.
- **Solusi:** 
  1. Masuk ke server: `tmux attach -t darsi-tunnel`.
  2. Jika proses mati, jalankan ulang: `cloudflared tunnel --url http://localhost:8000`.
  3. Salin URL baru dan lakukan detach (`Ctrl+B` lalu `D`).
  4. Perbarui endpoint URL di Unity client jika URL berubah.

### 📋 Cheatsheet Perintah `tmux`
| Perintah | Fungsi |
|---|---|
| `tmux new -s <nama>` | Buat sesi tmux baru |
| `tmux attach -t <nama>` | Masuk kembali ke sesi tmux |
| `tmux ls` | Melihat daftar semua sesi tmux yang aktif |
| `Ctrl + B` lalu `D` | Detach (keluar ke terminal utama tanpa mematikan proses) |
| `Ctrl + C` lalu `exit` | Menghentikan proses dan menutup sesi tmux |
| `tmux kill-session -t <nama>` | Mematikan paksa sesi tmux dari luar |
