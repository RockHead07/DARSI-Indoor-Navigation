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
            txtStatus.text = "[EDITOR] Simulasi: 'saya mau ke Lab Teori 201'";
        }
        if (txtResult != null)
        {
            txtResult.text = "saya mau ke Lab Teori 201";
        }
        if (voiceUI != null)
        {
            voiceUI.ShowPanel();
            voiceUI.SetListening(true);
            voiceUI.SetTranscript("saya mau ke Lab Teori 201");
        }
        StartCoroutine(SendToOllama("saya mau ke Lab Teori 201"));
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
        StartCoroutine(SendToOllama(result));
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

    IEnumerator SendToOllama(string spokenText)
    {
        if (txtStatus != null)
        {
            txtStatus.text = "Memproses ke Ollama...";
        }
        if (voiceUI != null) voiceUI.SetProcessing(true);
        yield return OllamaConnector.instance.ExtractPOI(spokenText, OnPOIReceived);
    }

    void OnPOIReceived(string poiName)
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