// PROBE SEMENTARA - dihapus setelah verifikasi lead-follow selesai.
// Membuktikan avatar benar-benar BERJALAN menyusuri polyline ShowPath, bukan diam
// atau meluncur ke tempat acak (ADR-034 keputusan 2).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GuideWalkProbe
{
    const string FlagKey = "DARSI_GuideWalkProbe_Armed";
    const string OutPath = @"C:\Users\UNKNOWN\AppData\Local\Temp\claude\D--Dev-Projects-UnityProjects-Learning-DARSI-Indoor-Navigation\030b0fb3-635a-48c7-8985-74bf6d16d9c7\scratchpad\guide-walk-probe.txt";

    static int _frames;
    static bool _running;
    static readonly StringBuilder Sb = new StringBuilder();
    static AIAvatarGuideController _guide;
    static Vector3 _startPos;
    static float _travelled;
    static Vector3 _lastPos;
    static float _maxDeviation;

    static GuideWalkProbe() { EditorApplication.playModeStateChanged += OnChange; }

    [MenuItem("DARSI/Avatar/Arm Guide Walk Probe")]
    public static void Arm() => EditorPrefs.SetBool(FlagKey, true);

    static void OnChange(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(FlagKey, false)) return;
        EditorPrefs.SetBool(FlagKey, false);
        _frames = 0; _running = true; _travelled = 0f; _maxDeviation = 0f;
        Sb.Clear();
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!_running) return;
        _frames++;

        if (_frames == 30) { Begin(); return; }
        // Beri ShowPath waktu menghitung rute sungguhan (pathUpdateFrequency 0.5s) sebelum
        // rute pengguna ditangkap, supaya tidak menangkap garis default yang basi.
        if (_frames == 90) { CaptureUserRoute(); return; }
        if (_frames > 90 && _guide != null) { WalkUser(); Sample(); }
        if (_frames < 500) return;

        Finish();
        _running = false;
        EditorApplication.update -= Tick;
    }

    static List<Vector3> RoutePoints()
    {
        var sp = Object.FindFirstObjectByType<ShowPath>();
        var lr = sp != null ? sp.GetComponent<LineRenderer>() : null;
        if (lr == null || lr.positionCount < 2) return null;
        var buf = new Vector3[lr.positionCount];
        lr.GetPositions(buf);
        float lift = sp.pathHeightAboveGround;
        return buf.Select(p => new Vector3(p.x, p.y - lift, p.z)).ToList();
    }

    static void Begin()
    {
        Sb.AppendLine("=== Guide Walk Probe ===");
        _guide = Object.FindFirstObjectByType<AIAvatarGuideController>();
        if (_guide == null) { Sb.AppendLine("AIAvatarGuideController TIDAK ADA"); return; }

        var nc = Object.FindFirstObjectByType<NavigationController>();
        var cam = Camera.main;
        Sb.AppendLine($"kamera(pengguna) = {(cam != null ? cam.transform.position.ToString("F2") : "NULL")}");
        Sb.AppendLine($"avatar awal      = {_guide.transform.position:F2}");

        // Pilih POI di lantai yang sama dengan kamera, yang paling jauh (rute paling panjang).
        var pois = Object.FindObjectsByType<POI>(FindObjectsSortMode.None);
        float camY = cam != null ? cam.transform.position.y : 0f;
        var target = pois
            .OrderBy(p => Mathf.Abs(p.transform.position.y - camY))
            .ThenByDescending(p => Vector3.Distance(p.transform.position, cam.transform.position))
            .FirstOrDefault();

        if (nc != null && target != null)
        {
            nc.SetPOIForNavigation(target);
            Sb.AppendLine($"tujuan           = '{target.gameObject.name}' y={target.transform.position.y:F2}");
            Sb.AppendLine("IsCurrentlyNavigating = " + nc.IsCurrentlyNavigating());
        }

        _guide.StartLeading();
        Sb.AppendLine("StartLeading() dipanggil, state=" + _guide.CurrentState);

        _startPos = _guide.transform.position;
        _lastPos = _startPos;
    }

    // Rute pengguna ditangkap terpisah, setelah ShowPath sempat menghitung rute sungguhan.
    static void CaptureUserRoute()
    {
        var pts = RoutePoints();
        if (pts == null || pts.Count < 2) { Sb.AppendLine("GAGAL tangkap rute: LineRenderer kosong"); return; }
        _userRoute = pts;
        var cam = Camera.main;
        if (cam == null) { Sb.AppendLine("GAGAL tangkap rute: Camera.main null"); return; }
        _camHeight = cam.transform.position.y - pts[0].y;
        Sb.AppendLine($"rute pengguna ditangkap: {pts.Count} corner, {PathPolyline.Length(pts):F2} m, tinggi kamera {_camHeight:F2} m");

        // Walker hidup DI DALAM Play mode supaya Time.deltaTime-nya sama dengan avatar.
        // Kegagalan di sini dulu senyap total (probe jalan, pengguna diam, tanpa jejak apa pun).
        try
        {
            var go = new GameObject("~ProbeUserWalker");
            _walker = go.AddComponent<ProbeUserWalker>();
            _walker.Init(cam.transform, pts, UserWalkSpeed, _camHeight);
            Sb.AppendLine("ProbeUserWalker aktif");
        }
        catch (System.Exception e)
        {
            Sb.AppendLine("GAGAL membuat ProbeUserWalker: " + e.Message);
        }
    }

    // Di Editor tidak ada device, jadi ARCamera diam. Kita simulasikan pengguna berjalan
    // menyusuri rute supaya perilaku MEMIMPIN benar-benar teruji, bukan cuma penempatan awal.
    static float _userS;
    const float UserWalkSpeed = 1.1f;   // kecepatan jalan pengunjung RS, sengaja lebih pelan dari avatar

    // Rute FISIK yang ditempuh pengguna, ditangkap SEKALI di awal.
    // Jangan baca ulang tiap frame: ShowPath selalu menggambar rute dari posisi pengguna
    // saat itu, jadi membaca ulang membuat pengguna dipindahkan ke depan dirinya sendiri.
    static List<Vector3> _userRoute;
    static float _camHeight;

    static float _maxUserS;

    static void WalkUser()
    {
        // Langkah kaki pengguna dijalankan ProbeUserWalker di dalam Play mode, bukan di sini.
        // Menggerakkannya dari EditorApplication.update memakai time-step yang BERBEDA dari
        // Update() avatar, sehingga pengguna simulasi mendapat lebih banyak langkah dan
        // perbandingan "avatar tertinggal berapa" jadi tidak sah.
        if (_walker != null) { _userS = _walker.Travelled; if (_userS > _maxUserS) _maxUserS = _userS; }
    }
    static ProbeUserWalker _walker;

    static void Sample()
    {
        var pos = _guide.transform.position;
        _travelled += Vector3.Distance(new Vector3(_lastPos.x, 0, _lastPos.z), new Vector3(pos.x, 0, pos.z));
        _lastPos = pos;

        // Seberapa jauh avatar menyimpang dari garis yang dilihat pengguna?
        // Diukur terhadap rute FISIK yang ditangkap sekali, bukan polyline yang dihitung
        // ulang tiap 0.5s (yang selalu berpangkal di posisi pengguna, jadi tidak stabil).
        var pts = _userRoute;
        if (pts != null && pts.Count >= 2)
        {
            float s = PathPolyline.Project(pts, pos);
            var on = PathPolyline.PointAt(pts, s);
            float dev = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(on.x, on.z));
            if (dev > _maxDeviation) _maxDeviation = dev;

            // Selisih posisi-sepanjang-rute: positif berarti avatar di DEPAN pengguna.
            var cam = Camera.main;
            if (cam != null)
            {
                float gap = s - PathPolyline.Project(pts, cam.transform.position);
                if (gap < _minGap) _minGap = gap;
                if (gap > _maxGap) _maxGap = gap;
                _gapSum += gap; _gapN++;
                if (gap < 0f) _framesBehind++;
            }
        }
    }
    static float _minGap = float.MaxValue, _maxGap = float.MinValue, _gapSum;
    static int _gapN, _framesBehind;

    static void Finish()
    {
        if (_guide == null) { Write(); return; }

        var pts = _userRoute;
        Sb.AppendLine("\n--- setelah ~8 detik ---");
        Sb.AppendLine($"state akhir      = {_guide.CurrentState}");
        Sb.AppendLine($"avatar akhir     = {_guide.transform.position:F2}");
        Sb.AppendLine($"jarak tempuh     = {_travelled:F2} m  (ekspektasi ~leadDistance=2.0 m, lalu berhenti)");
        Sb.AppendLine($"perpindahan neto = {Vector3.Distance(_startPos, _guide.transform.position):F2} m");
        Sb.AppendLine($"simpangan maks dari garis = {_maxDeviation:F3} m");

        Sb.AppendLine($"\n--- pengguna disimulasikan berjalan {UserWalkSpeed} m/s ---");
        Sb.AppendLine($"pengguna menempuh = {_maxUserS:F2} m sepanjang rute");
        if (_gapN > 0)
        {
            Sb.AppendLine($"selisih avatar-pengguna: min={_minGap:F2} m  rata2={_gapSum / _gapN:F2} m  maks={_maxGap:F2} m");
            Sb.AppendLine($"frame avatar TERTINGGAL di belakang pengguna = {_framesBehind} dari {_gapN}");
            Sb.AppendLine(_framesBehind == 0 ? "-> avatar SELALU di depan (benar untuk pemandu)"
                                             : "-> ADA saat avatar tertinggal, perlu diperiksa");
        }

        if (pts != null)
        {
            Sb.AppendLine($"\npolyline corners = {pts.Count}, panjang = {PathPolyline.Length(pts):F2} m");
            float sUser = Camera.main != null ? PathPolyline.Project(pts, Camera.main.transform.position) : -1f;
            float sAvatar = PathPolyline.Project(pts, _guide.transform.position);
            Sb.AppendLine($"posisi-sepanjang-rute: pengguna={sUser:F2} m, avatar={sAvatar:F2} m, selisih={sAvatar - sUser:F2} m");
        }
        else Sb.AppendLine("\npolyline TIDAK TERBACA (LineRenderer kosong)");

        var anim = _guide.GetComponentInChildren<Animator>();
        if (anim != null && anim.isActiveAndEnabled)
        {
            float speed = anim.GetFloat("Speed");
            string ctrlName = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL";
            Sb.AppendLine($"\nAnimator.Speed = {speed:F2}  controller={ctrlName}");
        }

        Write();
    }

    static void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllText(OutPath, Sb.ToString());
        Debug.Log("[GuideWalkProbe] hasil -> " + OutPath);
    }
}
