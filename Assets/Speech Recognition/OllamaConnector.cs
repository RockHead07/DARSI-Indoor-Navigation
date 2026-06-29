using System;
using System.Collections;
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

    // System prompt khusus ekstrak POI — singkat dan terarah
    private const string SYSTEM_PROMPT = @"
Kamu adalah sistem ekstraksi tujuan navigasi indoor di sebuah gedung kampus.
Tugasmu HANYA mengekstrak nama lokasi tujuan dari kalimat pengguna.

PENTING:
- Input berasal dari speech recognition, mungkin tidak sempurna
- Pengguna berbicara informal, santai, atau campur bahasa
- Normalize variasi ejaan: salat/sholat/solat = tempat ibadah
- Jika ada nama lokasi yang mirip, pilih yang paling relevan

Daftar lokasi yang tersedia:
- MMB Studio (lab multimedia)
- Lab Teori 201, 202, 203
- Lab Mikrotik (lab jaringan)
- Mushola (tempat ibadah, sholat, salat)
- BAAK (administrasi akademik)
- Perpustakaan
- Lab 102, Lab 103

Jawab HANYA dengan JSON berikut, tanpa teks lain:
{""poi"": ""nama lokasi sesuai daftar di atas""}

Contoh:
Input: ""aku mau salat"" → Output: {""poi"": ""Mushola""}
Input: ""perpus mana ya"" → Output: {""poi"": ""Perpustakaan""}
Input: ""mau ngurus surat"" → Output: {""poi"": ""BAAK""}
Input: ""lab dua kosong tiga"" → Output: {""poi"": ""Lab Teori 203""}
Input: ""mau ke studio"" → Output: {""poi"": ""MMB Studio""}

Jika tidak ada lokasi yang cocok, jawab: {""poi"": """"}
";

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
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

    public IEnumerator ExtractPOI(string spokenText, Action<string> onResult)
    {
        // Tandai sedang proses agar UI bisa tampilkan loading
        IsProcessing = true;

        string prompt = $"{SYSTEM_PROMPT}\nInput: \"{spokenText}\"\nOutput:";

        // Buat request body
        string requestBody = JsonUtility.ToJson(new OllamaRequest
        {
            model = modelName,
            prompt = prompt,
            stream = false,
            think = false // Mematikan thinking mode untuk qwen3:8b
        });

        Debug.Log($"[Ollama] Mengirim: {spokenText}");

        string responseText = null; // Simpan response jika berhasil
        bool requestSucceeded = false; // Flag keberhasilan

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
                    // Berhasil — simpan response, keluar loop
                    responseText = request.downloadHandler.text;
                    requestSucceeded = true;
                    Debug.Log($"[Ollama] Berhasil di percobaan ke-{attempt}");
                    break;
                }
                else
                {
                    Debug.LogWarning($"[Ollama] Gagal percobaan ke-{attempt}: {request.error}");
                    // Jika belum percobaan terakhir, tunggu sebelum retry
                    if (attempt < MAX_ATTEMPTS)
                    {
                        Debug.Log($"[Ollama] Menunggu {RETRY_DELAY_SECONDS}s sebelum retry...");
                        yield return new WaitForSeconds(RETRY_DELAY_SECONDS);
                    }
                }
            }
        }

        // Semua percobaan gagal
        if (!requestSucceeded)
        {
            Debug.LogError($"[Ollama] Semua {MAX_ATTEMPTS} percobaan gagal. Server tidak tersedia.");
            // Panggil event agar UI bisa tampilkan "Server tidak tersedia"
            onConnectionFailed?.Invoke();
            onResult?.Invoke(null);
            IsProcessing = false; // Selesai proses
            yield break;
        }

        // Berhasil — parse response
        Debug.Log($"[Ollama] Response raw: {responseText}");

        // Parse response Ollama
        OllamaResponse ollamaResponse = JsonUtility.FromJson<OllamaResponse>(responseText);
        string generatedText = ollamaResponse.response.Trim();
        Debug.Log($"[Ollama] Generated: {generatedText}");

        // Parse JSON poi dari generated text
        string poiName = ParsePOIFromJson(generatedText);
        Debug.Log($"[Ollama] POI extracted: {poiName}");

        onResult?.Invoke(poiName);
        IsProcessing = false; // Selesai proses
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
}