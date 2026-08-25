using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events; // Ditambahkan: untuk UnityEvent agar bisa kirim event ke UI
using UnityEngine.Networking;

public class OllamaConnector : MonoBehaviour
{
    public static OllamaConnector instance;

    [Header("Groq (satu-satunya provider fallback klien)")]
    [Tooltip("Dipakai kalau RAG Assistant (jalur primer) tidak terjangkau. Kosongkan untuk nonaktifkan fallback ini sepenuhnya.")]
    public string groqApiKey = "";
    // llama-3.1-8b-instant dimatikan Groq utk free/developer tier per 2026-08-16 (404 kalau dipakai).
    // openai/gpt-oss-20b = pengganti resmi yang direkomendasikan.
    public string groqModel = "openai/gpt-oss-20b";

    // Event ketika koneksi gagal setelah retry habis, UI bisa tampilkan pesan error
    [Header("Events")]
    [Tooltip("Event dipanggil ketika Groq gagal/tidak tersedia.")]
    public UnityEvent onConnectionFailed;

    // Property read-only untuk cek apakah sedang memproses request
    public bool IsProcessing { get; private set; }

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

    // Fallback klien terakhir kalau RAG Assistant (jalur primer, lihat AssistantClient/
    // darsi-backend) tidak terjangkau. Ollama-LAN yang dulu ada di sini sudah dihapus
    // (amandemen ADR-024, 2026-08-25): IP LAN hardcoded cuma kejangkau kalau HP dan laptop
    // satu WiFi, dan itu sudah terbukti tidak realistis di lapangan (RS/kampus) sejak
    // sebelum RAG ada. Pre-warm-nya pun tiap sesi buang sampai 60 detik nyoba nyambung ke
    // server yang tidak akan pernah ada di lokasi.
    public IEnumerator ExtractPOI(string spokenText, Action<string> onResult)
    {
        IsProcessing = true;
        Debug.Log($"[Voice] Mengirim: {spokenText}");

        bool ok = false;
        string poiName = null;

        if (!string.IsNullOrEmpty(groqApiKey))
        {
            yield return TryGroq(spokenText, (success, poi) => { ok = success; poiName = poi; });
        }

        if (!ok)
        {
            Debug.LogError("[Voice] Groq gagal atau groqApiKey kosong — tidak ada fallback lain.");
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
