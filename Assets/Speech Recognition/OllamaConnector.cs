using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events; // Ditambahkan: untuk UnityEvent agar bisa kirim event ke UI
using UnityEngine.Networking;
using TMPro; // Ditambahkan: untuk referensi txtStatus pre-warm UI

public class OllamaConnector : MonoBehaviour
{
    public static OllamaConnector instance;

    [Header("Ollama Settings")]
    [Tooltip("IP laptop di jaringan WiFi lokal")]
    public string ollamaHost = "192.168.18.150";
    public int ollamaPort = 11434;
    public string modelName = "llama3.2:latest";
    public bool useHttps = false;

    [Header("Groq Fallback (Opsional)")]
    [Tooltip("Dipakai otomatis kalau Ollama lokal tidak terjangkau setelah retry habis. Kosongkan untuk nonaktifkan fallback.")]
    public string groqApiKey = "";
    // llama-3.1-8b-instant dimatikan Groq utk free/developer tier per 2026-08-16 (404 kalau dipakai).
    // openai/gpt-oss-20b = pengganti resmi yang direkomendasikan.
    public string groqModel = "openai/gpt-oss-20b";

    // Event ketika koneksi gagal setelah retry habis, UI bisa tampilkan pesan error
    [Header("Events")]
    [Tooltip("Event dipanggil ketika koneksi Ollama gagal setelah retry habis.")]
    public UnityEvent onConnectionFailed;

    // Property read-only untuk cek apakah sedang memproses request
    public bool IsProcessing { get; private set; }

    [Header("Pre-warm UI (Opsional)")]
    [Tooltip("Referensi ke TextMeshPro untuk menampilkan status pre-warm. Boleh kosong.")]
    public TMP_Text txtStatus;

    // Jumlah maksimal percobaan (1 awal + 1 retry = 2)
    private const int MAX_ATTEMPTS = 2;
    // Jeda sebelum retry, memberi waktu server pulih
    private const float RETRY_DELAY_SECONDS = 2f;
    // Timeout lebih panjang untuk pre-warm karena model besar butuh waktu load ke RAM
    private const int PREWARM_TIMEOUT = 60;

    private string OllamaURL => $"{(useHttps ? "https" : "http")}://{ollamaHost}:{ollamaPort}/api/generate";
    private const string GroqURL = "https://api.groq.com/openai/v1/chat/completions";

    // System prompt khusus ekstrak POI — singkat dan terarah
    private const string SYSTEM_PROMPT = @"
Kamu adalah sistem ekstraksi tujuan navigasi indoor di RS Islam A. Yani.
Tugasmu HANYA mengekstrak nama lokasi tujuan dari kalimat pengguna.

PENTING:
- Input berasal dari speech recognition, mungkin tidak sempurna
- Pengguna berbicara informal, santai, atau campur bahasa
- Jika ada nama lokasi yang mirip, pilih yang paling relevan

Daftar lokasi yang tersedia:
- Ground (lobi, lantai dasar, pintu masuk)
- IGD (Instalasi Gawat Darurat, unit darurat)
- Farmasi (apotek, ambil obat)
- Radiology
- Ruang X-Ray (rontgen)
- Resepsionis (pendaftaran, informasi)
- Toilet
- Lift
- Parkir Mobil
- Parkir Motor Karyawan

Jawab HANYA dengan JSON berikut, tanpa teks lain:
{""poi"": ""nama lokasi sesuai daftar di atas""}

Contoh:
Input: ""mau ambil obat"" → Output: {""poi"": ""Farmasi""}
Input: ""ini darurat, ke IGD"" → Output: {""poi"": ""IGD""}
Input: ""mau daftar dulu"" → Output: {""poi"": ""Resepsionis""}
Input: ""mau rontgen"" → Output: {""poi"": ""Ruang X-Ray""}
Input: ""kamar mandi dimana"" → Output: {""poi"": ""Toilet""}

Jika tidak ada lokasi yang cocok, jawab: {""poi"": """"}
";

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

#if UNITY_EDITOR
        // Baca key dari file lokal (gitignored) kalau field Inspector kosong — supaya key
        // asli tidak pernah tersimpan di scene/prefab yang di-track git. Isi filenya sendiri,
        // satu baris berisi key, di root project: groq-api-key.local.txt
        if (string.IsNullOrEmpty(groqApiKey))
        {
            string path = Path.Combine(Application.dataPath, "..", "groq-api-key.local.txt");
            if (File.Exists(path))
                groqApiKey = File.ReadAllText(path).Trim();
        }
#endif
    }

    void Start()
    {
        StartCoroutine(PreWarmModel());
    }

    /// <summary>
    /// Kirim request dummy ke Ollama saat scene dibuka agar model sudah ter-load ke RAM.
    /// Tanpa pre-warm, request pertama bisa lambat 10-20 detik karena model baru di-load.
    /// Setelah pre-warm, request berikutnya langsung cepat 2-3 detik.
    /// </summary>
    private IEnumerator PreWarmModel()
    {
        Debug.Log("[Ollama] Pre-warming model...");
        if (txtStatus != null) txtStatus.text = "Memuat sistem...";

        // Buat request dummy singkat — cukup untuk trigger loading model ke RAM
        string requestBody = JsonUtility.ToJson(new OllamaRequest
        {
            model = modelName,
            prompt = "hi",
            stream = false,
            think = false
        });

        using (UnityWebRequest request = new UnityWebRequest(OllamaURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = PREWARM_TIMEOUT;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[Ollama] Model siap!");
                if (txtStatus != null) txtStatus.text = "Siap!";
            }
            else
            {
                // Pre-warm gagal bukan fatal — fungsionalitas utama tetap jalan
                Debug.LogWarning($"[Ollama] Pre-warm gagal: {request.error}. Model akan di-load saat request pertama.");
                if (txtStatus != null) txtStatus.text = "Sistem siap (offline mode)";
            }
        }
    }

    // Groq (cloud) jadi utama — Ollama lokal cuma dipakai kalau Groq gagal/kosong,
    // supaya tidak perlu nyala-matikan Ollama terus demi hemat RAM.
    public IEnumerator ExtractPOI(string spokenText, Action<string> onResult)
    {
        IsProcessing = true;
        Debug.Log($"[Voice] Mengirim: {spokenText}");

        bool ok = false;
        string poiName = null;

        if (!string.IsNullOrEmpty(groqApiKey))
        {
            Debug.Log("[Voice] Mencoba Groq (utama)...");
            yield return TryGroq(spokenText, (success, poi) => { ok = success; poiName = poi; });
        }

        if (!ok)
        {
            if (!string.IsNullOrEmpty(groqApiKey))
                Debug.LogWarning("[Voice] Groq tidak terjangkau, fallback ke Ollama lokal...");
            yield return TryOllama(spokenText, (success, poi) => { ok = success; poiName = poi; });
        }

        if (!ok)
        {
            Debug.LogError("[Voice] Groq dan Ollama dua-duanya gagal/tidak tersedia.");
            onConnectionFailed?.Invoke();
            onResult?.Invoke(null);
            IsProcessing = false;
            yield break;
        }

        Debug.Log($"[Voice] POI extracted: {poiName}");
        onResult?.Invoke(poiName);
        IsProcessing = false;
    }

    // ponytail: groqApiKey ikut ter-bundle ke APK (field publik, tersimpan di scene/prefab) —
    // cukup untuk demo/uji lapangan, tapi bisa diekstrak siapapun yang decompile APK-nya.
    // Sebelum rilis produksi, pindahkan panggilan Groq ke backend proxy supaya key tidak
    // pernah ikut ke client.
    private IEnumerator TryGroq(string spokenText, Action<bool, string> onDone)
    {
        string prompt = $"{SYSTEM_PROMPT}\nInput: \"{spokenText}\"\nOutput:";

        string requestBody = JsonUtility.ToJson(new GroqRequest
        {
            model = groqModel,
            messages = new[] { new GroqMessage { role = "user", content = prompt } },
            temperature = 0f
        });

        using (UnityWebRequest request = new UnityWebRequest(GroqURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {groqApiKey}");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Groq] Gagal: {request.error}");
                onDone?.Invoke(false, null);
                yield break;
            }

            Debug.Log($"[Groq] Response raw: {request.downloadHandler.text}");
            GroqResponse groqResponse = JsonUtility.FromJson<GroqResponse>(request.downloadHandler.text);
            string generatedText = groqResponse.choices[0].message.content.Trim();
            Debug.Log($"[Groq] Generated: {generatedText}");

            onDone?.Invoke(true, ParsePOIFromJson(generatedText));
        }
    }

    private IEnumerator TryOllama(string spokenText, Action<bool, string> onDone)
    {
        string prompt = $"{SYSTEM_PROMPT}\nInput: \"{spokenText}\"\nOutput:";
        string requestBody = JsonUtility.ToJson(new OllamaRequest
        {
            model = modelName,
            prompt = prompt,
            stream = false,
            think = false // Mematikan thinking mode untuk qwen3:8b
        });

        // Loop retry: coba MAX_ATTEMPTS kali (percobaan awal + 1 retry)
        for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            Debug.Log($"[Ollama] Percobaan ke-{attempt} dari {MAX_ATTEMPTS}");

            using (UnityWebRequest request = new UnityWebRequest(OllamaURL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                // Timeout diubah 15 -> 30 detik, LLM lokal butuh waktu lebih lama
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Ollama] Berhasil di percobaan ke-{attempt}");
                    OllamaResponse ollamaResponse = JsonUtility.FromJson<OllamaResponse>(request.downloadHandler.text);
                    string generatedText = ollamaResponse.response.Trim();
                    Debug.Log($"[Ollama] Generated: {generatedText}");
                    onDone?.Invoke(true, ParsePOIFromJson(generatedText));
                    yield break;
                }

                Debug.LogWarning($"[Ollama] Gagal percobaan ke-{attempt}: {request.error}");
                if (attempt < MAX_ATTEMPTS)
                {
                    Debug.Log($"[Ollama] Menunggu {RETRY_DELAY_SECONDS}s sebelum retry...");
                    yield return new WaitForSeconds(RETRY_DELAY_SECONDS);
                }
            }
        }

        Debug.LogWarning($"[Ollama] Semua {MAX_ATTEMPTS} percobaan gagal. Server lokal tidak tersedia.");
        onDone?.Invoke(false, null);
    }

    string ParsePOIFromJson(string jsonText)
    {
        try
        {
            // Cari pattern {"poi": "..."}
            int start = jsonText.IndexOf("{");
            int end = jsonText.LastIndexOf("}");
            if (start < 0 || end < 0) return null;

            string cleanJson = jsonText.Substring(start, end - start + 1);
            POIResult result = JsonUtility.FromJson<POIResult>(cleanJson);
            return string.IsNullOrEmpty(result.poi) ? null : result.poi;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ollama] Parse error: {e.Message}");
            return null;
        }
    }

    // ── Data classes untuk serialisasi JSON ──

    [Serializable]
    class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool stream;
        public bool think; // Mematikan thinking mode untuk qwen3:8b
    }

    [Serializable]
    class OllamaResponse
    {
        public string response;
        public bool done;
    }

    [Serializable]
    class POIResult
    {
        public string poi;
    }

    [Serializable]
    class GroqRequest
    {
        public string model;
        public GroqMessage[] messages;
        public float temperature;
    }

    [Serializable]
    class GroqMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    class GroqResponse
    {
        public GroqChoice[] choices;
    }

    [Serializable]
    class GroqChoice
    {
        public GroqMessage message;
    }
}