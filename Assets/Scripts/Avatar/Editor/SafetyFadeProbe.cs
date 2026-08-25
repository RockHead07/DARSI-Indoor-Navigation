// PROBE SEMENTARA - gate ADR-034 keputusan 4.
// Menaruh kamera pada serangkaian jarak horizontal dari avatar, menunggu alpha stabil,
// lalu mencatat alpha DAN status renderer. Fungsi keselamatan yang sesungguhnya ada di
// renderer.enabled (avatar benar-benar hilang), bukan di nilai alpha, karena material
// MToon hasil impor VRM ber-BlendMode Cutout.
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SafetyFadeProbe
{
    const string FlagKey = "DARSI_SafetyFadeProbe_Armed";
    const string OutPath = @"C:\Users\UNKNOWN\AppData\Local\Temp\claude\D--Dev-Projects-UnityProjects-Learning-DARSI-Indoor-Navigation\030b0fb3-635a-48c7-8985-74bf6d16d9c7\scratchpad\safety-fade-probe.txt";

    // Jarak uji: dari aman, melewati fadeStart (0.9) dan fadeEnd (0.5), sampai menempel.
    static readonly float[] Distances = { 2.0f, 1.2f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.2f, 0.0f };
    const int SettleFrames = 45;   // cukup untuk MoveTowards alpha (fadeSpeed 8) mencapai target

    static readonly StringBuilder Sb = new StringBuilder();
    static bool _running;
    static int _frames, _step;
    static AvatarSafetyFade _fade;
    static Transform _avatar, _cam;

    static SafetyFadeProbe() { EditorApplication.playModeStateChanged += OnChange; }

    [MenuItem("DARSI/Avatar/Arm Safety Fade Probe")]
    public static void Arm() => EditorPrefs.SetBool(FlagKey, true);

    static void OnChange(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(FlagKey, false)) return;
        EditorPrefs.SetBool(FlagKey, false);
        Sb.Clear(); _frames = 0; _step = -1; _running = true;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!_running) return;
        _frames++;

        if (_step < 0)
        {
            if (_frames < 30) return;
            if (!Begin()) { Finish(); return; }
            _step = 0; _frames = 0;
            Place(Distances[0]);
            return;
        }

        if (_frames < SettleFrames) return;

        Record(Distances[_step]);
        _step++;
        _frames = 0;

        if (_step >= Distances.Length) { Finish(); return; }
        Place(Distances[_step]);
    }

    static bool Begin()
    {
        _fade = Object.FindFirstObjectByType<AvatarSafetyFade>();
        if (_fade == null) { Sb.AppendLine("AvatarSafetyFade TIDAK ADA di scene"); return false; }
        _avatar = _fade.transform;
        _cam = Camera.main != null ? Camera.main.transform : null;
        if (_cam == null) { Sb.AppendLine("Camera.main NULL"); return false; }

        // Hentikan pemandu supaya avatar diam, kalau tidak dia akan menjauh dari kamera.
        var guide = Object.FindFirstObjectByType<AIAvatarGuideController>();
        if (guide != null) guide.StopLeading();

        var f = typeof(AvatarSafetyFade).GetField("targetRenderers", BindingFlags.NonPublic | BindingFlags.Instance);
        var rs = f?.GetValue(_fade) as Renderer[];

        Sb.AppendLine("=== Safety Fade Probe (ADR-034 keputusan 4) ===");
        Sb.AppendLine("avatar  : " + _avatar.position.ToString("F2"));
        Sb.AppendLine("renderer terdaftar : " + (rs != null ? rs.Length.ToString() : "NULL"));
        if (rs != null)
            foreach (var r in rs.Take(5))
                Sb.AppendLine("   - " + (r != null ? r.gameObject.name : "NULL"));
        Sb.AppendLine("\n jarak_H | alpha | IsFadedOut | renderer_aktif | terlihat?");
        Sb.AppendLine("---------|-------|------------|----------------|----------");
        return true;
    }

    // Kamera ditaruh pada jarak HORIZONTAL tertentu, dengan tinggi kamera dipertahankan.
    static void Place(float horizontal)
    {
        var a = _avatar.position;
        _cam.position = new Vector3(a.x + horizontal, a.y + 1.54f, a.z);
    }

    static void Record(float requested)
    {
        var f = typeof(AvatarSafetyFade).GetField("targetRenderers", BindingFlags.NonPublic | BindingFlags.Instance);
        var rs = f?.GetValue(_fade) as Renderer[];
        int on = rs?.Count(r => r != null && r.enabled) ?? -1;
        int total = rs?.Length ?? 0;

        var d = _cam.position - _avatar.position; d.y = 0f;
        string visible = on == 0 ? "TIDAK (aman)" : (on < total ? "sebagian" : "ya");

        Sb.AppendLine($"  {d.magnitude,5:F2}  | {_fade.CurrentAlpha,5:F2} |    {_fade.IsFadedOut,-5}   |     {on}/{total}        | {visible}");
    }

    static void Finish()
    {
        _running = false;
        EditorApplication.update -= Tick;
        Sb.AppendLine("\nCatatan: kolom alpha hanya bermakna kalau material transparan.");
        Sb.AppendLine("Material MToon impor VRM ber-BlendMode 1 (Cutout), jadi yang benar-benar");
        Sb.AppendLine("melindungi pandangan pengguna adalah kolom renderer_aktif menjadi 0/3.");
        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllText(OutPath, Sb.ToString());
        Debug.Log("[SafetyFadeProbe] hasil -> " + OutPath);
    }
}
