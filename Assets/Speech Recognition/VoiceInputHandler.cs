using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class VoiceInputHandler : MonoBehaviour
{
    [Header("UI")]
    public Button btnVoice;
    public TMP_Text txtStatus;
    public TMP_Text txtResult;

    private AndroidJavaObject speechRecognizer;
    private AndroidJavaObject currentActivity;
    private AndroidJavaObject recognizerIntent;
    private bool isListening = false;
    private string pendingResult;
    private string pendingError;
    private bool recognizerReady = false;
    private bool pendingStartListening = false;

    [Header("Voice UI")]
    [SerializeField] private VoiceUIController voiceUI;

    [Header("POI")]
    [SerializeField] private POIManager poiManager;
    [SerializeField] private POIDataEvent onPoiMatched;

    [Header("RAG Assistant (Primary AI & Semantic POI)")]
    [Tooltip("Klien RAG Assistant untuk pemahaman intent dan resolusi POI canggih.")]
    [SerializeField] private AssistantClient assistantClient;
    [Tooltip("Gunakan RAG sebagai pipeline utama")]
    [SerializeField] private bool useRAGPrimary = true;

    [Header("Fallback Settings")]
    [Tooltip("Fallback otomatis ke Ollama/Groq lokal jika RAG backend gagal/offline")]
    [SerializeField] private bool enableFallback = true;

    [System.Serializable]
    public class POIDataEvent : UnityEvent<POIData> { }

    void Start()
    {
        if (btnVoice == null)
        {
            Debug.LogError("[VoiceInputHandler] btnVoice belum di-assign.");
            enabled = false;
            return;
        }

        if (txtStatus == null)
        {
            Debug.LogWarning("[VoiceInputHandler] txtStatus belum di-assign.");
        }

        if (txtResult == null)
        {
            Debug.LogWarning("[VoiceInputHandler] txtResult belum di-assign.");
        }

        if (assistantClient == null)
        {
            assistantClient = FindAnyObjectByType<AssistantClient>();
        }

        // Minta permission mic saat start
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }

        btnVoice.onClick.AddListener(OnVoiceButtonClicked);
        if (txtStatus != null)
        {
            txtStatus.text = "Siap mendengarkan...";
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        SetupSpeechRecognizer();
        #endif
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(pendingResult))
        {
            string result = pendingResult;
            pendingResult = null;
            OnSpeechResult(result);
        }

        if (!string.IsNullOrEmpty(pendingError))
        {
            string error = pendingError;
            pendingError = null;
            OnSpeechError(error);
        }
    }

    void OnVoiceButtonClicked()
    {
        if (!isListening)
            StartListening();
    }

    void StartListening()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                if (txtStatus != null)
                {
                    txtStatus.text = "Izin mikrofon dibutuhkan.";
                }
                return;
            }

            isListening = true;
            if (txtStatus != null)
            {
                txtStatus.text = "Mendengarkan...";
            }
            if (voiceUI != null)
            {
                voiceUI.ShowPanel();
                voiceUI.SetListening(true);
            }
            btnVoice.interactable = false;

            // Destroy recognizer lama sebelum buat baru — Android SpeechRecognizer
            // tidak bisa dipakai ulang setelah onResults/onError
            DestroyRecognizer();

            pendingStartListening = true;
            SetupSpeechRecognizer();
        }
        catch (System.Exception e)
        {
            if (txtStatus != null)
            {
                txtStatus.text = "Error: " + e.Message;
            }
            if (voiceUI != null) voiceUI.SetError(e.Message);
            ResetButton();
        }
        #else
        // Mode editor — simulasi input teks untuk testing di PC
        if (txtStatus != null)
        {
            txtStatus.text = "[EDITOR] Simulasi: 'Anakku habis ketabrak motor'";
        }
        if (txtResult != null)
        {
            txtResult.text = "Anakku habis ketabrak motor";
        }
        if (voiceUI != null)
        {
            voiceUI.ShowPanel();
            voiceUI.SetListening(true);
            voiceUI.SetTranscript("Anakku habis ketabrak motor");
        }
        StartCoroutine(ProcessVoiceInput("Anakku habis ketabrak motor"));
        #endif
    }

    // Dipanggil otomatis oleh Android saat speech selesai
    public void OnSpeechResult(string result)
    {
        if (string.IsNullOrEmpty(result))
        {
            if (txtStatus != null)
            {
                txtStatus.text = "Tidak terdeteksi, coba lagi.";
            }
            ResetButton();
            return;
        }

        if (txtStatus != null)
        {
            txtStatus.text = "Teks diterima!";
        }
        if (txtResult != null)
        {
            txtResult.text = result;
        }
        if (voiceUI != null)
        {
            voiceUI.SetListening(false);
            voiceUI.SetTranscript(result);
        }
        StartCoroutine(ProcessVoiceInput(result));
    }

    public void OnSpeechError(string error)
    {
        if (txtStatus != null)
        {
            txtStatus.text = "Error speech: " + error;
        }
        if (voiceUI != null) voiceUI.SetError(error);
        ResetButton();
    }

    private IEnumerator ProcessVoiceInput(string spokenText)
    {
        if (voiceUI != null) voiceUI.SetProcessing(true);

        bool ragResolved = false;

        // ── 1. PRIMARY: RAG Assistant (Backend AI & Intent Analysis) ──
        if (useRAGPrimary && assistantClient != null)
        {
            if (txtStatus != null) txtStatus.text = "Menganalisis tujuan (RAG)...";
            Debug.Log($"[VoiceInputHandler] Mengirim ke RAG Assistant (Utama): '{spokenText}'");

            AssistantAnswer ragAnswer = null;
            yield return assistantClient.Ask(spokenText, (answer) => { ragAnswer = answer; });

            if (ragAnswer != null && !string.IsNullOrEmpty(ragAnswer.answer))
            {
                Debug.Log($"[VoiceInputHandler] RAG response: '{ragAnswer.answer}' (poi_id={ragAnswer.poi_id ?? "null"}, poi_name={ragAnswer.poi_name ?? "null"})");

                POIData matchedPoi = null;

                // A. Match via poi_id (GUID exact match dari metadata RAG)
                if (!string.IsNullOrEmpty(ragAnswer.poi_id) && poiManager != null)
                {
                    matchedPoi = poiManager.FindById(ragAnswer.poi_id);
                }

                // B. Match via poi_name yang diekstrak RAG
                if (matchedPoi == null && !string.IsNullOrEmpty(ragAnswer.poi_name) && poiManager != null)
                {
                    matchedPoi = poiManager.FindBestMatch(ragAnswer.poi_name);
                }

                // C. PRIORITAS UTAMA RAG: Cari POI yang direkomendasikan AI di dalam kalimat jawaban!
                // Contoh: Pengguna bilang "anakku ketabrak motor", RAG menjawab "...segera ke IGD" -> "IGD" ditemukan!
                if (matchedPoi == null && poiManager != null)
                {
                    matchedPoi = poiManager.FindBestMatch(ragAnswer.answer);
                }

                // D. Cek apakah query awal pengguna cocok dengan POI lokal (jika RAG tidak menyebut POI spesifik)
                if (matchedPoi == null && poiManager != null)
                {
                    matchedPoi = poiManager.FindBestMatch(spokenText);
                }

                if (matchedPoi != null)
                {
                    ragResolved = true;
                    if (txtStatus != null)
                    {
                        txtStatus.text = $"Navigasi ke: {matchedPoi.EffectiveName}";
                    }
                    if (voiceUI != null)
                    {
                        voiceUI.SetTranscript($"{matchedPoi.EffectiveName}\n\"{ragAnswer.answer}\"");
                        voiceUI.HidePanel();
                    }
                    onPoiMatched?.Invoke(matchedPoi);
                    FinishProcessing();
                    yield break;
                }
                else
                {
                    // RAG memberi jawaban info/SOP/jadwal dokter tanpa tujuan navigasi fisik
                    ragResolved = true;
                    string prefix = ragAnswer.contains_simulated_data ? "[DATA SIMULASI] " : "";
                    if (txtStatus != null)
                    {
                        txtStatus.text = prefix + ragAnswer.answer;
                    }
                    if (voiceUI != null)
                    {
                        voiceUI.SetTranscript(prefix + ragAnswer.answer);
                    }
                    Debug.Log($"[VoiceInputHandler] RAG menjawab informasi umum (tanpa POI terpetakan): {ragAnswer.answer}");
                    FinishProcessing();
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning("[VoiceInputHandler] RAG Assistant tidak merespons atau gagal. Mencoba fallback...");
            }
        }

        // ── 2. FALLBACK: Direct Match / OllamaConnector (Jika RAG Offline/Timeout) ──
        POIData localDirectMatch = poiManager != null ? poiManager.FindBestMatch(spokenText) : null;
        if (localDirectMatch != null)
        {
            if (txtStatus != null) txtStatus.text = $"Navigasi ke: {localDirectMatch.EffectiveName}";
            if (voiceUI != null) voiceUI.HidePanel();
            onPoiMatched?.Invoke(localDirectMatch);
            FinishProcessing();
            yield break;
        }

        // ── 2. FALLBACK: OllamaConnector / Local Extractor ──
        if (!ragResolved && enableFallback)
        {
            Debug.Log("[VoiceInputHandler] Beralih ke fallback Ollama/Groq lokal...");
            if (txtStatus != null)
            {
                txtStatus.text = "Memproses (Fallback lokal)...";
            }

            if (OllamaConnector.instance != null)
            {
                yield return OllamaConnector.instance.ExtractPOI(spokenText, OnFallbackPOIReceived);
            }
            else if (poiManager != null)
            {
                // Fallback langsung ke POIManager fuzzy match
                POIData localMatch = poiManager.FindBestMatch(spokenText);
                OnFallbackPOIReceived(localMatch != null ? localMatch.EffectiveName : null);
            }
            else
            {
                OnFallbackPOIReceived(null);
            }
        }
        else if (!ragResolved)
        {
            if (txtStatus != null) txtStatus.text = "Gagal memproses input suara.";
            FinishProcessing();
        }
    }

    private void OnFallbackPOIReceived(string poiName)
    {
        if (string.IsNullOrEmpty(poiName))
        {
            if (txtStatus != null)
            {
                txtStatus.text = "POI tidak ditemukan, coba lagi.";
            }
        }
        else
        {
            POIData matchedPoi = null;
            if (poiManager != null)
            {
                matchedPoi = poiManager.FindBestMatch(poiName);
            }

            if (matchedPoi != null)
            {
                if (txtStatus != null)
                {
                    txtStatus.text = $"Navigasi ke: {matchedPoi.EffectiveName}";
                }
                onPoiMatched?.Invoke(matchedPoi);
                if (voiceUI != null)
                {
                    voiceUI.HidePanel();
                }
            }
            else
            {
                if (txtStatus != null)
                {
                    txtStatus.text = $"POI tidak ditemukan untuk: {poiName}";
                }
            }
        }

        FinishProcessing();
    }

    private void FinishProcessing()
    {
        if (voiceUI != null)
        {
            voiceUI.SetProcessing(false);
            voiceUI.SetListening(false);
        }
        ResetButton();
    }

    void ResetButton()
    {
        isListening = false;
        btnVoice.interactable = true;
    }

    public void CancelListening()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (currentActivity != null)
        {
            currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                if (speechRecognizer != null)
                {
                    speechRecognizer.Call("cancel");
                }
            }));
        }
        #endif

        if (txtStatus != null)
        {
            txtStatus.text = "Dibatalkan.";
        }
        if (voiceUI != null)
        {
            voiceUI.SetListening(false);
            voiceUI.SetProcessing(false);
            voiceUI.HidePanel();
        }
        ResetButton();
    }

    private void SetupSpeechRecognizer()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        // Cek apakah device mendukung speech recognition
        AndroidJavaClass recognizerCheckClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
        bool isAvailable = recognizerCheckClass.CallStatic<bool>("isRecognitionAvailable", currentActivity);

        if (!isAvailable)
        {
            Debug.LogError("[VoiceInputHandler] Speech recognition TIDAK tersedia di device ini.");
            if (txtStatus != null)
            {
                txtStatus.text = "Speech recognition tidak didukung di device ini.";
            }
            if (btnVoice != null)
            {
                btnVoice.interactable = false;
            }
            return;
        }

        Debug.Log("[VoiceInputHandler] Speech recognition tersedia, melanjutkan setup...");

        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            AndroidJavaClass recognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            speechRecognizer = recognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", currentActivity);
            speechRecognizer.Call("setRecognitionListener", new RecognitionListenerProxy(this));

            AndroidJavaClass intentClass = new AndroidJavaClass("android.speech.RecognizerIntent");
            recognizerIntent = new AndroidJavaObject("android.content.Intent");
            recognizerIntent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH"));
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_LANGUAGE"), "id-ID");
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"), intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_PROMPT"), "Sebutkan tujuan Anda...");
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_MAX_RESULTS"), 1);

            recognizerReady = true;
            if (pendingStartListening)
            {
                pendingStartListening = false;
                speechRecognizer.Call("startListening", recognizerIntent);
            }
        }));
    }

    /// <summary>
    /// Destroy dan dispose SpeechRecognizer lama.
    /// Harus dipanggil sebelum membuat recognizer baru, dan di callback onResults/onError
    /// agar memory tidak leak.
    /// </summary>
    private void DestroyRecognizer()
    {
        recognizerReady = false;
        if (speechRecognizer != null)
        {
            // Destroy harus di UI thread untuk Android SpeechRecognizer
            if (currentActivity != null)
            {
                AndroidJavaObject recRef = speechRecognizer;
                currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        recRef.Call("destroy");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[VoiceInputHandler] DestroyRecognizer error: " + e.Message);
                    }
                }));
            }
            speechRecognizer.Dispose();
            speechRecognizer = null;
        }
        if (recognizerIntent != null)
        {
            recognizerIntent.Dispose();
            recognizerIntent = null;
        }
    }

    private void OnDestroy()
    {
        DestroyRecognizer();
    }

    private void SetPendingResult(string result)
    {
        pendingResult = result;
    }

    private void SetPendingError(string error)
    {
        pendingError = error;
    }

    private class RecognitionListenerProxy : AndroidJavaProxy
    {
        private readonly VoiceInputHandler handler;

        public RecognitionListenerProxy(VoiceInputHandler handler) : base("android.speech.RecognitionListener")
        {
            this.handler = handler;
        }

        public void onResults(AndroidJavaObject results)
        {
            try
            {
                string key = "results_recognition";
                AndroidJavaObject matches = results.Call<AndroidJavaObject>("getStringArrayList", key);
                if (matches != null && matches.Call<int>("size") > 0)
                {
                    string text = matches.Call<string>("get", 0);
                    handler.SetPendingResult(text);
                }
                else
                {
                    handler.SetPendingError("Hasil kosong");
                }
            }
            catch (System.Exception e)
            {
                handler.SetPendingError(e.Message);
            }

            // Destroy recognizer setelah hasil diterima — tidak bisa dipakai lagi
            handler.DestroyRecognizer();
        }

        public void onError(int error)
        {
            handler.SetPendingError("Kode error: " + error);

            // Destroy recognizer setelah error — tidak bisa dipakai lagi
            handler.DestroyRecognizer();
        }

        public void onReadyForSpeech(AndroidJavaObject @params) { }
        public void onBeginningOfSpeech() { }
        public void onRmsChanged(float rmsdB) { }
        public void onBufferReceived(byte[] buffer) { }
        public void onEndOfSpeech() { }
        public void onPartialResults(AndroidJavaObject partialResults) { }
        public void onEvent(int eventType, AndroidJavaObject @params) { }
    }
}