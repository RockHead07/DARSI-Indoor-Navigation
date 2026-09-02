using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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

    [Tooltip("KOSONGKAN kecuali memang mau memaksa suara tertentu. Kosong berarti " +
             "backend yang menentukan (tts.py DEFAULT_VOICE). Nama suara sengaja TIDAK " +
             "diduplikasi di sini: pernah kejadian backend sudah pindah ke Thalita tapi " +
             "Unity masih mengirim id-ID-GadisNeural yang ter-serialize di scene, " +
             "sehingga suara produksi diam-diam tidak berubah (pola yang dilarang ADR-021).")]
    [SerializeField] private string voiceName = "";

    [Tooltip("Apakah keluaran suara TTS diaktifkan.")]
    [SerializeField] private bool enableVoiceOutput = true;

    [Header("Komponen Target")]
    [Tooltip("Driver lip-sync berbasis analisis audio (MFCC). Dipakai sebagai CADANGAN " +
             "saat backend tidak mengirim batas waktu kata, mis. Tier 2 sherpa-onnx offline.")]
    [SerializeField] private AvatarSpeechLipSync lipSyncDriver;

    [Tooltip("Driver lip-sync berbasis timeline teks (Amandemen 033-B). Dipakai UTAMA " +
             "saat backend mengirim `words`. Kosongkan untuk mencari otomatis.")]
    [SerializeField] private VisemeTimelineLipSync timelineDriver;

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

        if (timelineDriver == null)
        {
            timelineDriver = GetComponentInChildren<VisemeTimelineLipSync>(true);
            if (timelineDriver == null) timelineDriver = GetComponentInParent<VisemeTimelineLipSync>();
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

    /// <summary>Satu potongan audio siap putar, hasil dari FetchOne. Null di pemanggil
    /// berarti potongan itu gagal disintesis/diunduh -- ditangani sebagai lewati diam-diam,
    /// bukan menjatuhkan keseluruhan ucapan (ADR-033 Amandemen 033-A poin 1).</summary>
    private class KlipSiap
    {
        public AudioClip klip;
        public VisemeTimelineLipSync.KataJson[] words;
        public string engineUsed;
        public string audioUrl;
    }

    /// <summary>Ambil SATU klip audio dari backend TANPA memutarnya. Dipisah dari
    /// pemutaran supaya bisa di-prefetch di latar belakang (lihat SpeakTextChunked)
    /// sementara potongan sebelumnya masih diputar.</summary>
    private IEnumerator FetchOne(string text, Action<KlipSiap> onDone)
    {
        var payload = new TTSRequestPayload { text = text, voice = voiceName };
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
                Debug.LogWarning($"[AvatarAudioClient] Endpoint TTS gagal: {webReq.error} (HTTP {webReq.responseCode}). Melanjutkan tanpa suara untuk potongan ini.");
                onDone?.Invoke(null);
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
            onDone?.Invoke(null);
            yield break;
        }

        Debug.Log($"[AvatarAudioClient] TTS berhasil diproses oleh engine '{responsePayload.engine_used}', mengunduh audio dari: {responsePayload.audio_url}");

        // Resolusi URL audio (jika path relatif terhadap backend)
        string fullAudioUrl = responsePayload.audio_url;
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
                Debug.LogWarning($"[AvatarAudioClient] Gagal mengunduh file audio dari {fullAudioUrl}: {audioReq.error}. Melanjutkan tanpa suara untuk potongan ini.");
                onDone?.Invoke(null);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
            if (clip == null)
            {
                Debug.LogWarning("[AvatarAudioClient] AudioClip hasil unduhan null.");
                onDone?.Invoke(null);
                yield break;
            }

            onDone?.Invoke(new KlipSiap
            {
                klip = clip,
                words = responsePayload.words,
                engineUsed = responsePayload.engine_used,
                audioUrl = fullAudioUrl,
            });
        }
    }

    /// <summary>Pilih driver lip-sync untuk klip ini lalu mulai memutarnya. Tidak menunggu
    /// selesai -- pemanggil yang menunggu lewat IsSpeaking.</summary>
    private void Mainkan(KlipSiap hasil)
    {
        LastEngineUsed = hasil.engineUsed;
        LastAudioUrl = hasil.audioUrl;

        // Pilih driver lip-sync menurut data yang BENAR-BENAR dikirim backend,
        // bukan menurut asumsi (Amandemen 033-B). Tier 1 edge-tts mengirim batas
        // kata; Tier 2 sherpa-onnx offline tidak mengirim apa pun, dan di sana
        // analisis audio adalah satu-satunya yang masih bisa bekerja.
        PilihDriver(hasil.words);

        if (lipSyncDriver != null && lipSyncDriver.enabled)
        {
            lipSyncDriver.PlayAudio(hasil.klip);
        }
        else if (audioSource != null)
        {
            audioSource.clip = hasil.klip;
            audioSource.Play();
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

        IsFetchingAudio = true;
        KlipSiap hasil = null;
        yield return FetchOne(text, r => hasil = r);
        IsFetchingAudio = false;

        if (hasil == null)
        {
            onFinished?.Invoke();
            yield break;
        }

        Mainkan(hasil);
        while (IsSpeaking) yield return null;
        onFinished?.Invoke();
    }

    // Pisah kalimat pada tanda baca akhir (.!?) yang diikuti spasi/akhir teks. Titik di
    // tengah angka ("07.00-15.00") TIDAK ikut kepisah karena syaratnya harus diikuti
    // spasi -- di jadwal dokter tidak ada spasi setelah titik jam. Heuristik ini cukup
    // untuk jawaban asisten yang sudah dibatasi sistem prompt maksimal 3 kalimat wajar;
    // bukan tokenizer kalimat umum untuk teks bebas.
    private static readonly Regex PemisahKalimat = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);

    private static List<string> PecahKalimat(string teks)
    {
        var hasil = new List<string>();
        foreach (var potongan in PemisahKalimat.Split(teks.Trim()))
        {
            var t = potongan.Trim();
            if (!string.IsNullOrEmpty(t)) hasil.Add(t);
        }
        return hasil;
    }

    /// <summary>
    /// Seperti SpeakText, tapi teks dipecah per kalimat dan disintesis terpisah supaya
    /// avatar mulai bicara begitu kalimat PERTAMA siap -- bukan menunggu seluruh jawaban
    /// disintesis dulu. Kalimat berikutnya di-prefetch di latar belakang sambil kalimat
    /// sekarang diputar.
    ///
    /// TIDAK memangkas waktu tunggu LLM (teks ini baru ada setelah itu selesai) -- cuma
    /// memangkas ekor sintesis+unduh TTS setelah jawaban lengkap tersedia.
    /// </summary>
    public IEnumerator SpeakTextChunked(string fullText, Action onFinished = null)
    {
        if (string.IsNullOrWhiteSpace(fullText) || !enableVoiceOutput)
        {
            onFinished?.Invoke();
            yield break;
        }

        var kalimat = PecahKalimat(fullText);
        if (kalimat.Count <= 1)
        {
            // Satu kalimat saja: tidak ada yang diuntungkan dari prefetch, langsung
            // pakai jalur biasa daripada menambah mesin bertahap untuk nol manfaat.
            yield return SpeakText(fullText, onFinished);
            yield break;
        }

        var siap = new KlipSiap[kalimat.Count];

        IsFetchingAudio = true;
        yield return FetchOne(kalimat[0], r => siap[0] = r);
        IsFetchingAudio = false;

        for (int i = 0; i < kalimat.Count; i++)
        {
            Coroutine prefetch = null;
            if (i + 1 < kalimat.Count)
            {
                prefetch = StartCoroutine(FetchOne(kalimat[i + 1], r => siap[i + 1] = r));
            }

            if (siap[i] != null)
            {
                Mainkan(siap[i]);
                while (IsSpeaking) yield return null;
            }
            // siap[i] == null: potongan ini gagal disintesis/diunduh. Lewati diam-diam,
            // lanjut ke kalimat berikutnya -- isolasi kegagalan per-potongan, bukan
            // menjatuhkan seluruh ucapan (ADR-033 Amandemen 033-A poin 1).

            if (prefetch != null) yield return prefetch;
        }

        onFinished?.Invoke();
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

        yield return SpeakTextChunked(answer.answer, onFinished: () =>
        {
            onNavigationReady?.Invoke();

            if (guideController != null && !string.IsNullOrEmpty(answer.poi_id))
            {
                guideController.StartLeading();
            }
        });
    }

    /// <summary>
    /// Menyalakan tepat SATU driver lip-sync sesuai ketersediaan batas waktu kata.
    ///
    /// Wajib eksklusif: keduanya menulis ke blendshape VRM yang sama, dan
    /// AvatarSpeechLipSync menulis di LateUpdate (menang atas Update milik driver
    /// timeline). Kalau dua-duanya hidup, yang terlihat selalu punya MFCC dan
    /// timeline-nya tidak berpengaruh apa-apa.
    /// </summary>
    private void PilihDriver(VisemeTimelineLipSync.KataJson[] words)
    {
        bool adaTiming = words != null && words.Length > 0;

        if (adaTiming && timelineDriver != null)
        {
            if (timelineDriver.BangunDariKata(words))
            {
                timelineDriver.enabled = true;
                if (lipSyncDriver != null) lipSyncDriver.enabled = false;
                LipSyncAktif = "timeline";
                return;
            }
        }

        // Tidak ada timing yang bisa dipakai: kembali ke analisis audio.
        if (timelineDriver != null)
        {
            timelineDriver.HentikanTimeline();
            timelineDriver.enabled = false;
        }
        if (lipSyncDriver != null) lipSyncDriver.enabled = true;
        LipSyncAktif = adaTiming ? "mfcc (timeline gagal dibangun)" : "mfcc (tanpa timing)";
    }

    /// <summary>Driver yang dipakai pada ucapan terakhir. Untuk diagnostik.</summary>
    public string LipSyncAktif { get; private set; } = "-";

    /// <summary>
    /// Pemicu manual untuk uji lip-sync di Play Mode: klik kanan komponen ini di Inspector
    /// (atau ikon gerigi) lalu pilih menu ini. Tidak dipanggil di alur produksi mana pun.
    /// </summary>
    [ContextMenu("Debug/Uji Bicara (kalimat sampel)")]
    private void DebugUjiBicara()
    {
        StartCoroutine(SpeakText("Selamat datang di Rumah Sakit Islam Ahmad Yani. Silakan ikuti saya menuju ruangan yang Anda tuju."));
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
        // KOSONG saat engine_used == "sherpa-onnx" (Tier 2 offline tidak menghasilkan
        // timing). Itu kondisi normal yang WAJIB ditangani, bukan error.
        // Tipe sama persis dengan VisemeTimelineLipSync.KataJson -- satu bentuk kata
        // {text,start,end}, bukan didefinisikan dua kali lalu disalin manual.
        public VisemeTimelineLipSync.KataJson[] words;
    }
}
