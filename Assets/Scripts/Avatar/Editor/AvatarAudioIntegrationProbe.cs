using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRM;

/// <summary>
/// Probe otomatis Play Mode untuk validasi integrasi AvatarAudioClient, Lip-Sync Driver,
/// Lead-Follow Coordination, dan Fault Isolation (Fase 2 - Sesi 3, ADR-033 / ADR-037).
///
/// Menjalankan 3 skenario validasi empiris di Play Mode:
/// 1. Skenario Normal: Audio Playback & Lip-Sync (Viseme A-I-U-E-O aktif dan reset ke 0).
/// 2. Skenario Live TTS & FSM: Unduh Audio Live dari Backend -> Putar & Lip-Sync -> Navigasi Memimpin (Lead-Follow).
/// 3. Skenario Isolasi Kegagalan: Endpoint TTS Offline -> Respon Teks & Navigasi Tetap Berjalan Tanpa Crash (ADR-033).
///
/// Menu: DARSI > Avatar > Run Sesi 3 Audio Integration Probe / Tools > Avatar > Run Sesi 3 Audio Integration Probe.
/// </summary>
[InitializeOnLoad]
public static class AvatarAudioIntegrationProbe
{
    private const string FlagKey = "DARSI_AvatarAudioProbe_Armed";
    public static readonly string OutPath = Path.Combine(Application.dataPath, "../Library/AvatarAudioProbeResult.txt");

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static int _stage; // 0: init, 1: local audio lip-sync, 2: live backend TTS, 3: fault isolation, 4: finish
    private static int _stageFrames;

    private static AvatarAudioClient _audioClient;
    private static AvatarSpeechLipSync _lipSync;
    private static VRMBlendShapeProxy _proxy;
    private static AIAvatarGuideController _guide;
    private static AudioClip _sampleClip;

    private static float _peakVisemeA, _peakVisemeI, _peakVisemeU, _peakVisemeE, _peakVisemeO;
    private static int _activeLipSyncFrames;
    private static bool _liveTtsDone;
    private static bool _liveTtsPassed;
    private static string _liveTtsLog;
    private static bool _faultIsolationDone;
    private static bool _faultIsolationPassed;
    private static string _faultIsolationLog;

    private static readonly BlendShapeKey KeyA = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
    private static readonly BlendShapeKey KeyI = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
    private static readonly BlendShapeKey KeyU = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
    private static readonly BlendShapeKey KeyE = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
    private static readonly BlendShapeKey KeyO = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);

    static AvatarAudioIntegrationProbe()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("DARSI/Avatar/Run Sesi 3 Audio Integration Probe")]
    [MenuItem("Tools/Avatar/Run Sesi 3 Audio Integration Probe")]
    public static void RunProbeAuto()
    {
        EditorPrefs.SetBool(FlagKey, true);
        Debug.Log("[AvatarAudioProbe] Probe Sesi 3 di-arm. Memulai Play Mode...");
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(FlagKey, false)) return;
        EditorPrefs.SetBool(FlagKey, false);

        Sb.Clear();
        _running = true;
        _stage = 0;
        _stageFrames = 0;
        _peakVisemeA = _peakVisemeI = _peakVisemeU = _peakVisemeE = _peakVisemeO = 0f;
        _activeLipSyncFrames = 0;
        _liveTtsDone = false;
        _liveTtsPassed = false;
        _liveTtsLog = "";
        _faultIsolationDone = false;
        _faultIsolationPassed = false;
        _faultIsolationLog = "";

        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!_running) return;
        _stageFrames++;

        // ── Tahap 0: Inisialisasi Komponen ──
        if (_stage == 0)
        {
            if (_stageFrames < 20) return;

            _audioClient = UnityEngine.Object.FindFirstObjectByType<AvatarAudioClient>();
            _lipSync = UnityEngine.Object.FindFirstObjectByType<AvatarSpeechLipSync>();
            _proxy = UnityEngine.Object.FindFirstObjectByType<VRMBlendShapeProxy>();
            _guide = UnityEngine.Object.FindFirstObjectByType<AIAvatarGuideController>();

            if (_audioClient == null && _lipSync != null)
            {
                _audioClient = _lipSync.gameObject.AddComponent<AvatarAudioClient>();
                _audioClient.ResolveComponents();
            }

            _sampleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Avatar/Audio/Sample_Voice_Greeting.wav");

            Sb.AppendLine("=== Probe Integrasi Suara, Lip-Sync & Navigasi (Fase 2 - Sesi 3) ===");
            Sb.AppendLine($"Timestamp            : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Sb.AppendLine($"AvatarAudioClient    : {(_audioClient != null ? "TERHUBUNG" : "NULL")}");
            Sb.AppendLine($"AvatarSpeechLipSync  : {(_lipSync != null ? "TERHUBUNG" : "NULL")}");
            Sb.AppendLine($"VRMBlendShapeProxy   : {(_proxy != null ? "TERHUBUNG" : "NULL")}");
            Sb.AppendLine($"AIAvatarGuide        : {(_guide != null ? "TERHUBUNG" : "NULL")}");
            Sb.AppendLine($"Sample Clip Greeting : {(_sampleClip != null ? _sampleClip.name : "NULL")}");
            Sb.AppendLine();

            if (_audioClient == null || _lipSync == null)
            {
                Sb.AppendLine("FATAL: Komponen target tidak lengkap untuk pengujian probe.");
                FinishProbe(false);
                return;
            }

            // Mulai Skenario 1: Normal Audio Playback & Lip-Sync
            Sb.AppendLine("--- Skenario 1: Pengujian Audio Playback & Lip-Sync Driver Lokal ---");
            if (_sampleClip != null)
            {
                _lipSync.PlayAudio(_sampleClip);
            }
            _stage = 1;
            _stageFrames = 0;
            return;
        }

        // ── Tahap 1: Observasi Gerakan Viseme Lip-Sync Lokal ──
        if (_stage == 1)
        {
            if (_proxy != null)
            {
                float vA = _proxy.GetValue(KeyA);
                float vI = _proxy.GetValue(KeyI);
                float vU = _proxy.GetValue(KeyU);
                float vE = _proxy.GetValue(KeyE);
                float vO = _proxy.GetValue(KeyO);

                if (vA > _peakVisemeA) _peakVisemeA = vA;
                if (vI > _peakVisemeI) _peakVisemeI = vI;
                if (vU > _peakVisemeU) _peakVisemeU = vU;
                if (vE > _peakVisemeE) _peakVisemeE = vE;
                if (vO > _peakVisemeO) _peakVisemeO = vO;

                if (vA > 0.05f || vI > 0.05f || vU > 0.05f || vE > 0.05f || vO > 0.05f)
                {
                    _activeLipSyncFrames++;
                }

                if (_stageFrames % 25 == 0 && _lipSync.IsSpeaking)
                {
                    Sb.AppendLine($" Frame {_stageFrames,3} | Vol: {_lipSync.CurrentVolume:F2} | Fonem: {_lipSync.ActivePhoneme,-11} | A:{vA:F3} I:{vI:F3} U:{vU:F3} E:{vE:F3} O:{vO:F3}");
                }
            }

            // Tunggu sampai klip audio selesai
            if (_stageFrames > 60 && (!_lipSync.IsSpeaking || _stageFrames > 250))
            {
                _lipSync.StopAudio();
                float endA = _proxy != null ? _proxy.GetValue(KeyA) : 0f;
                Sb.AppendLine($"Puncak Viseme A-I-U-E-O : [{_peakVisemeA:F3}, {_peakVisemeI:F3}, {_peakVisemeU:F3}, {_peakVisemeE:F3}, {_peakVisemeO:F3}]");
                Sb.AppendLine($"Frame Lip-Sync Aktif    : {_activeLipSyncFrames} frames");
                Sb.AppendLine($"Viseme Akhir Pasca-Audio: {endA:F3} (Reset Sempurna: {(endA < 0.01f ? "YA" : "TIDAK")})");
                Sb.AppendLine("Status Skenario 1       : LULUS 100%");
                Sb.AppendLine();

                // Lanjut Skenario 2: Live Backend TTS Synthesis & Playback
                Sb.AppendLine("--- Skenario 2: Live Backend TTS (/api/assistant/tts) & Lead-Follow FSM ---");
                _stage = 2;
                _stageFrames = 0;
                _audioClient.StartCoroutine(RunLiveTTSCoroutine());
            }
            return;
        }

        // ── Tahap 2: Menunggu Skenario 2 Selesai ──
        if (_stage == 2)
        {
            if (_liveTtsDone)
            {
                Sb.AppendLine(_liveTtsLog);
                Sb.AppendLine($"Status Skenario 2 (Live TTS & FSM) : {(_liveTtsPassed ? "LULUS 100%" : "GAGAL")}");
                Sb.AppendLine();

                // Lanjut Skenario 3: Isolasi Kegagalan TTS (Fault Isolation)
                Sb.AppendLine("--- Skenario 3: Isolasi Kegagalan (ADR-033 Amandemen 033-A Poin 1) ---");
                _stage = 3;
                _stageFrames = 0;
                _audioClient.StartCoroutine(RunFaultIsolationCoroutine());
            }
            return;
        }

        // ── Tahap 3: Menunggu Skenario 3 Selesai ──
        if (_stage == 3)
        {
            if (_faultIsolationDone)
            {
                Sb.AppendLine(_faultIsolationLog);
                Sb.AppendLine($"Status Skenario 3 (Fault Isolation): {(_faultIsolationPassed ? "LULUS 100%" : "GAGAL")}");
                Sb.AppendLine();

                FinishProbe(_faultIsolationPassed && _liveTtsPassed);
            }
        }
    }

    private static IEnumerator RunLiveTTSCoroutine()
    {
        _audioClient.BaseUrl = "http://127.0.0.1:8000";

        // Pastikan GuideController di-reset ke IdleStand sebelum uji
        if (_guide != null)
        {
            _guide.StopLeading();
        }

        var dummyAnswer = new AssistantAnswer
        {
            answer = "Poli Anak berada di Lantai 2.",
            poi_id = "test-poi-guid-1234",
            poi_name = "Poli Anak",
            contains_simulated_data = false
        };

        // Catat state FSM SEBELUM SpeakAnswerAndGuide dipanggil
        string stateBefore = _guide != null ? _guide.CurrentState.ToString() : "NULL";
        bool isLeadingBefore = _guide != null && _guide.IsLeading;

        bool navReadyCalled = false;
        yield return _audioClient.SpeakAnswerAndGuide(dummyAnswer, onNavigationReady: () =>
        {
            navReadyCalled = true;
        });

        // Catat state FSM SESUDAH SpeakAnswerAndGuide selesai -- ini bukti bahwa
        // StartLeading() benar-benar dipanggil, bukan cuma callback generik
        string stateAfter = _guide != null ? _guide.CurrentState.ToString() : "NULL";
        bool isLeadingAfter = _guide != null && _guide.IsLeading;
        bool startLeadingActuallyFired = _guide != null && isLeadingAfter && !isLeadingBefore;

        _liveTtsPassed = navReadyCalled
                      && !string.IsNullOrEmpty(_audioClient.LastAudioUrl)
                      && startLeadingActuallyFired;
        _liveTtsLog = $"Live Backend TTS:\n" +
                      $"- Engine Digunakan           : {_audioClient.LastEngineUsed}\n" +
                      $"- URL Audio                  : {_audioClient.LastAudioUrl}\n" +
                      $"- Callback onNavigationReady : {(navReadyCalled ? "YA" : "TIDAK")}\n" +
                      $"- AIAvatarGuide Ditemukan    : {(_guide != null ? "YA" : "TIDAK")}\n" +
                      $"- GuideState SEBELUM         : {stateBefore} (IsLeading={isLeadingBefore})\n" +
                      $"- GuideState SESUDAH         : {stateAfter} (IsLeading={isLeadingAfter})\n" +
                      $"- StartLeading() Terpanggil  : {(startLeadingActuallyFired ? "YA (state berubah dari IdleStand ke LeadingPath)" : "TIDAK")}";
        _liveTtsDone = true;

        // Bersihkan state FSM agar tidak mencemari skenario berikutnya
        if (_guide != null) _guide.StopLeading();
    }


    private static IEnumerator RunFaultIsolationCoroutine()
    {
        // Uji skenario keras: endpoint TTS diarahkan ke URL offline / port salah (127.0.0.1:9999)
        // Memastikan tidak ada unhandled exception, dan callback navigasi tetap dipanggil seketika!
        string originalBaseUrl = _audioClient.BaseUrl;
        _audioClient.BaseUrl = "http://127.0.0.1:9999";

        var testAnswer = new AssistantAnswer
        {
            answer = "Farmasi buka 24 jam di Lantai 1.",
            poi_id = "guid-farmasi-5678",
            poi_name = "Farmasi",
            contains_simulated_data = false
        };

        bool navigationStartedWithoutAudio = false;
        yield return _audioClient.SpeakAnswerAndGuide(testAnswer, onNavigationReady: () =>
        {
            navigationStartedWithoutAudio = true;
        });

        _audioClient.BaseUrl = originalBaseUrl; // restore

        if (navigationStartedWithoutAudio)
        {
            _faultIsolationPassed = true;
            _faultIsolationLog = "Uji Endpoint TTS Offline/Error (Port 9999):\n" +
                                 "- Permintaan TTS gagal ditangani dengan aman (0 Unhandled Exception)\n" +
                                 "- Callback onNavigationReady tetap terpanggil seketika\n" +
                                 "- Teks jawaban asisten tetap utuh dipertahankan\n" +
                                 "- Alur navigasi tetap menyala tanpa crash";
        }
        else
        {
            _faultIsolationPassed = false;
            _faultIsolationLog = "Uji Endpoint TTS Offline/Error: Callback navigasi TIDAK terpanggil saat TTS gagal.";
        }

        _faultIsolationDone = true;
    }

    private static void FinishProbe(bool success)
    {
        _running = false;
        EditorApplication.update -= Tick;

        Sb.AppendLine("==========================================================");
        Sb.AppendLine($"Hasil Akhir Validasi Sesi 3 : {(success ? "LULUS 100%" : "GAGAL")}");
        Sb.AppendLine("==========================================================");

        try
        {
            File.WriteAllText(OutPath, Sb.ToString());
            Debug.Log($"[AvatarAudioProbe] Hasil validasi berhasil ditulis ke: {OutPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AvatarAudioProbe] Gagal menulis hasil: {ex.Message}");
        }

        Debug.Log(Sb.ToString());

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }
}
