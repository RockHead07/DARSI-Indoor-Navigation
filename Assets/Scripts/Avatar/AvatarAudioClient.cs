using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Klien Audio dan Sintesis Suara TTS untuk Avatar VRM (Fase 2 - Sesi 3, ADR-033 / ADR-037).
/// Menghubungkan respon teks asisten RAG ke endpoint backend TTS (/api/assistant/tts),
/// mengunduh klip audio, memutar suara pada AudioSource avatar,
/// serta menggerakkan bibir (lip-sync) secara tersinkronisasi sebelum pemandu mulai memimpin rute.
///
/// ISOLASI KEGAGALAN (ADR-033 Amandemen 033-A poin 1):
/// Kegagalan sintesis suara atau unduh audio TIDAK BOLEH menjatuhkan respon teks
/// maupun membatalkan rute navigasi. Jika TTS gagal, callback penyelesaian tetap dipanggil
/// agar alur navigasi dapat segera dimulai tanpa suara.
/// </summary>
[DisallowMultipleComponent]
public class AvatarAudioClient : MonoBehaviour
{
    [Header("Backend TTS Endpoint")]
    [Tooltip("Base URL backend. Kosongkan trailing slash.")]
    [SerializeField] private string baseUrl = "http://127.0.0.1:8000";

    [Tooltip("Timeout permintaan sintesis TTS (detik).")]
    [SerializeField] private int timeoutSeconds = 15;

    [Tooltip("Nama model suara yang digunakan (default: id-ID-GadisNeural).")]
    [SerializeField] private string voiceName = "id-ID-GadisNeural";

    [Tooltip("Apakah keluaran suara TTS diaktifkan.")]
    [SerializeField] private bool enableVoiceOutput = true;

    [Header("Komponen Target")]
    [Tooltip("Driver lip-sync avatar. Jika kosong, dicari otomatis.")]
    [SerializeField] private AvatarSpeechLipSync lipSyncDriver;

    [Tooltip("Pengendali pemandu lead-follow. Jika kosong, dicari otomatis.")]
    [SerializeField] private AIAvatarGuideController guideController;

    [Tooltip("AudioSource sumber suara ucapan avatar.")]
    [SerializeField] private AudioSource audioSource;

    public bool IsSpeaking => (audioSource != null && audioSource.isPlaying) || (lipSyncDriver != null && lipSyncDriver.IsSpeaking);
    public bool IsFetchingAudio { get; private set; }
    public string LastEngineUsed { get; private set; }
    public string LastAudioUrl { get; private set; }

    public string BaseUrl
    {
        get => baseUrl;
        set => baseUrl = value;
    }

    public string VoiceName
    {
        get => voiceName;
        set => voiceName = value;
    }

    public bool EnableVoiceOutput
    {
        get => enableVoiceOutput;
        set => enableVoiceOutput = value;
    }

    public AvatarSpeechLipSync LipSyncDriver
    {
        get
        {
            if (lipSyncDriver == null) ResolveComponents();
            return lipSyncDriver;
        }
        set => lipSyncDriver = value;
    }

    public AIAvatarGuideController GuideController
    {
        get
        {
            if (guideController == null) ResolveComponents();
            return guideController;
        }
        set => guideController = value;
    }

    public AudioSource AudioSource
    {
        get
        {
            if (audioSource == null) ResolveComponents();
            return audioSource;
        }
        set => audioSource = value;
    }

    private void Awake()
    {
        ResolveComponents();
    }

    public void ResolveComponents()
    {
        if (lipSyncDriver == null)
        {
            lipSyncDriver = GetComponentInChildren<AvatarSpeechLipSync>(true);
            if (lipSyncDriver == null) lipSyncDriver = GetComponentInParent<AvatarSpeechLipSync>();
        }

        if (guideController == null)
        {
            guideController = GetComponentInChildren<AIAvatarGuideController>(true);
            if (guideController == null) guideController = GetComponentInParent<AIAvatarGuideController>();
        }

        if (audioSource == null)
        {
            if (lipSyncDriver != null && lipSyncDriver.AudioSource != null)
            {
                audioSource = lipSyncDriver.AudioSource;
            }
            else
            {
                audioSource = GetComponentInChildren<AudioSource>(true);
            }
        }
    }

    /// <summary>
    /// Mengirim teks ke backend TTS, mengunduh file audio, memutarnya melalui driver lip-sync,
    /// dan memanggil onFinished ketika audio selesai diputar (atau seketika jika TTS gagal/non-aktif).
    /// </summary>
    public IEnumerator SpeakText(string text, Action onFinished = null)
    {
        if (string.IsNullOrWhiteSpace(text) || !enableVoiceOutput)
        {
            onFinished?.Invoke();
            yield break;
        }

        ResolveComponents();
        IsFetchingAudio = true;

        var payload = new TTSRequestPayload
        {
            text = text,
            voice = voiceName
        };

        string ttsEndpoint = $"{baseUrl.TrimEnd('/')}/api/assistant/tts";
        string jsonBody = JsonUtility.ToJson(payload);
        Debug.Log($"[AvatarAudioClient] POST {ttsEndpoint} : {jsonBody}");

        TTSResponsePayload responsePayload = null;

        using (var webReq = new UnityWebRequest(ttsEndpoint, "POST"))
        {
            webReq.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            webReq.downloadHandler = new DownloadHandlerBuffer();
            webReq.SetRequestHeader("Content-Type", "application/json");
            webReq.timeout = timeoutSeconds;

            yield return webReq.SendWebRequest();

            if (webReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AvatarAudioClient] Endpoint TTS gagal: {webReq.error} (HTTP {webReq.responseCode}). Melanjutkan tanpa suara.");
                IsFetchingAudio = false;
                onFinished?.Invoke();
                yield break;
            }

            try
            {
                responsePayload = JsonUtility.FromJson<TTSResponsePayload>(webReq.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AvatarAudioClient] Gagal mem-parse response TTS JSON: {ex.Message}");
            }
        }

        if (responsePayload == null || string.IsNullOrEmpty(responsePayload.audio_url))
        {
            Debug.LogWarning("[AvatarAudioClient] Payload response TTS kosong atau tidak memiliki audio_url.");
            IsFetchingAudio = false;
            onFinished?.Invoke();
            yield break;
        }

        LastEngineUsed = responsePayload.engine_used;
        LastAudioUrl = responsePayload.audio_url;
        Debug.Log($"[AvatarAudioClient] TTS berhasil diproses oleh engine '{LastEngineUsed}', mengunduh audio dari: {LastAudioUrl}");

        // Resolusi URL audio (jika path relatif terhadap backend)
        string fullAudioUrl = LastAudioUrl;
        if (!fullAudioUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !fullAudioUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            fullAudioUrl = $"{baseUrl.TrimEnd('/')}/{fullAudioUrl.TrimStart('/')}";
        }

        AudioType audioType = AudioType.MPEG;
        if (fullAudioUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            audioType = AudioType.WAV;
        }

        using (var audioReq = UnityWebRequestMultimedia.GetAudioClip(fullAudioUrl, audioType))
        {
            audioReq.timeout = timeoutSeconds;
            yield return audioReq.SendWebRequest();

            if (audioReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AvatarAudioClient] Gagal mengunduh file audio dari {fullAudioUrl}: {audioReq.error}. Melanjutkan tanpa suara.");
                IsFetchingAudio = false;
                onFinished?.Invoke();
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
            IsFetchingAudio = false;

            if (clip == null)
            {
                Debug.LogWarning("[AvatarAudioClient] AudioClip hasil unduhan null.");
                onFinished?.Invoke();
                yield break;
            }

            // Putar audio pada driver lip-sync atau AudioSource
            if (lipSyncDriver != null)
            {
                lipSyncDriver.PlayAudio(clip);
            }
            else if (audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }

            // Tunggu hingga pemutaran audio selesai
            while (IsSpeaking)
            {
                yield return null;
            }

            onFinished?.Invoke();
        }
    }

    /// <summary>
    /// Alur terpadu untuk berbicara lalu memicu lead-follow navigasi.
    /// Avatar berbicara dan melakukan lip-sync terlebih dahulu, baru kemudian
    /// memulai pergerakan memimpin rute jika terdapat target POI valid.
    /// </summary>
    public IEnumerator SpeakAnswerAndGuide(AssistantAnswer answer, Action onNavigationReady = null)
    {
        if (answer == null || string.IsNullOrWhiteSpace(answer.answer))
        {
            onNavigationReady?.Invoke();
            yield break;
        }

        yield return SpeakText(answer.answer, onFinished: () =>
        {
            onNavigationReady?.Invoke();

            if (guideController != null && !string.IsNullOrEmpty(answer.poi_id))
            {
                guideController.StartLeading();
            }
        });
    }

    /// <summary>
    /// Menghentikan pemutaran audio ucapan seketika.
    /// </summary>
    public void StopSpeaking()
    {
        if (lipSyncDriver != null)
        {
            lipSyncDriver.StopAudio();
        }
        else if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    [Serializable]
    public class TTSRequestPayload
    {
        public string text;
        public string voice;
    }

    [Serializable]
    public class TTSResponsePayload
    {
        public string audio_url;
        public string engine_used;
    }
}
