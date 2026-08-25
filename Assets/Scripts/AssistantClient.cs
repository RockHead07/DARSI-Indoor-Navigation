using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Klien untuk endpoint asisten RAG di backend (POST /api/assistant/query).
/// Spec & angka evaluasi: repo darsi-backend, docs/RETRIEVAL-EVALUATION.md. ADR-026.
///
/// SENGAJA berdiri sendiri, tidak menyentuh VoiceInputHandler/OllamaConnector.
/// Alur mic yang sudah divalidasi (ADR-024) tetap apa adanya: ucapan -> POI ->
/// navigasi. Asisten ini pintu masuk TAMBAHAN, supaya kalau ia bermasalah,
/// navigasi AR tidak ikut mati.
///
/// Navigasi tidak diimplementasikan ulang di sini. Kalau jawaban membawa poi_id,
/// payload yang sama persis dengan jalur Flutter dioper ke
/// UaaLEntryPoint.ReceiveLaunchPayload, jadi cuma ada SATU jalur navigasi yang
/// sudah teruji, bukan dua yang bisa berbeda perilaku.
/// </summary>
public class AssistantClient : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Base URL backend. Kosongkan trailing slash. Sejak 2026-08-24 pakai " +
             "Cloudflare Named Tunnel permanen (systemd service di server, ADR-027) -- " +
             "https://api-darsi.rockhead07.tech, URL ini TIDAK berubah lagi walau " +
             "server restart. Untuk uji lokal di device pakai " +
             "'adb reverse tcp:8000 tcp:8000' lalu isi http://127.0.0.1:8000")]
    [SerializeField] private string baseUrl = "http://127.0.0.1:8000";

    [Tooltip("Detik. ADR-029: Bifrost (provider primer) terukur butuh 13-32 detik " +
             "(reasoning trace medgemma + overhead tunnel), lebih lama dari Groq yang " +
             "dulu jadi primer. Nilai ini HARUS lebih besar dari skenario terburuk " +
             "backend (Bifrost timeout 30 detik + kemungkinan fallback Groq), kalau " +
             "tidak Unity menyerah duluan padahal backend sebenarnya akan berhasil.")]
    [SerializeField] private int timeoutSeconds = 60;

    [Header("Konteks posisi (opsional, ADR-026)")]
    [Tooltip("Kosongkan untuk cari otomatis. Dipakai mengirim lantai aktif supaya " +
             "retrieval bisa memprioritaskan info di lantai user.")]
    [SerializeField] private FloorVisibilityManager floorManager;

    [Tooltip("Dikirim sebagai 'building'. Kosongkan kalau tidak ingin mem-bias per gedung.")]
    [SerializeField] private string buildingName = "RS Islam Ahmad Yani";

    [Header("Navigasi")]
    [Tooltip("Kosongkan untuk cari otomatis. Dipakai memulai rute saat jawaban membawa poi_id.")]
    [SerializeField] private UaaLEntryPoint entryPoint;

    public bool IsProcessing { get; private set; }

    void Awake()
    {
        if (floorManager == null) floorManager = FindAnyObjectByType<FloorVisibilityManager>();
        if (entryPoint == null) entryPoint = FindAnyObjectByType<UaaLEntryPoint>();
    }

    /// <summary>
    /// Tanya asisten. onResult dipanggil dengan null kalau gagal (jaringan, timeout,
    /// backend mati). Pemanggil WAJIB menangani null: backend ini opsional, dan
    /// matinya tidak boleh terasa seperti app yang rusak.
    /// </summary>
    public IEnumerator Ask(string question, Action<AssistantAnswer> onResult)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            onResult?.Invoke(null);
            yield break;
        }

        IsProcessing = true;

        var req = new AssistantRequest
        {
            user_text = question,
            // Null sebelum localize berhasil, dan itu benar (ADR-007/011). Backend
            // memperlakukan field ini opsional dan hanya mem-BIAS peringkat, tidak
            // pernah mem-filter, jadi kosong tetap memberi jawaban yang benar.
            current_floor = floorManager != null ? floorManager.CurrentFloorLabel : null,
            building = string.IsNullOrEmpty(buildingName) ? null : buildingName,
        };

        string url = $"{baseUrl.TrimEnd('/')}/api/assistant/query";
        string body = JsonUtility.ToJson(req);
        Debug.Log($"[Assistant] -> {url} : {body}");

        using (var www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = timeoutSeconds;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Assistant] Gagal: {www.error} (HTTP {www.responseCode})");
                onResult?.Invoke(null);
                IsProcessing = false;
                yield break;
            }

            AssistantAnswer answer = null;
            try
            {
                answer = JsonUtility.FromJson<AssistantAnswer>(www.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Assistant] Parse error: {e.Message}");
            }

            if (answer == null || string.IsNullOrEmpty(answer.answer))
            {
                Debug.LogWarning("[Assistant] Response kosong atau tidak bisa dibaca.");
                onResult?.Invoke(null);
                IsProcessing = false;
                yield break;
            }

            Debug.Log($"[Assistant] <- \"{answer.answer}\" (poi_id={answer.poi_id ?? "null"}, " +
                      $"simulasi={answer.contains_simulated_data})");
            onResult?.Invoke(answer);
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Mulai navigasi ke POI yang disebut jawaban. Mengembalikan false kalau jawaban
    /// itu memang tidak menyangkut lokasi tertentu (poi_id null) — itu normal, tidak
    /// semua pertanyaan berujung ke navigasi.
    ///
    /// poi_id TIDAK PERNAH dikarang LLM; backend menurunkannya dari metadata chunk
    /// hasil retrieval (ADR-026, pola yang sama dengan ADR-021).
    /// </summary>
    public bool StartNavigationFrom(AssistantAnswer answer)
    {
        if (answer == null || string.IsNullOrEmpty(answer.poi_id)) return false;

        if (entryPoint == null)
        {
            Debug.LogWarning("[Assistant] UaaLEntryPoint tidak ditemukan, navigasi dilewati.");
            return false;
        }

        // Bentuk payload-nya sengaja identik dengan yang dikirim Flutter, supaya
        // jalur navigasinya benar-benar sama dan tidak ada cabang perilaku kedua.
        var payload = new LaunchPayloadOut
        {
            action = "launchAR",
            mode = "navigate",
            poiId = answer.poi_id,
            poiName = answer.poi_name,
        };
        entryPoint.ReceiveLaunchPayload(JsonUtility.ToJson(payload));
        return true;
    }

    // ── Uji cepat tanpa UI, mengikuti pola debug ContextMenu di UaaLEntryPoint ──

    [ContextMenu("Debug/Tanya: farmasi buka jam berapa")]
    private void DebugTanyaFarmasi() =>
        StartCoroutine(Ask("farmasi buka jam berapa", a =>
            Debug.Log(a == null ? "[Assistant] (gagal)" : a.answer)));

    [ContextMenu("Debug/Tanya di luar cakupan: resep rendang")]
    private void DebugTanyaSampah() =>
        StartCoroutine(Ask("resep rendang padang yang enak", a =>
            Debug.Log(a == null ? "[Assistant] (gagal)" : a.answer)));

    // ── Bentuk data. JsonUtility butuh nama field persis sama dengan JSON-nya. ──

    [Serializable]
    private class AssistantRequest
    {
        public string user_text;
        public string current_floor;
        public string building;
    }

    [Serializable]
    private class LaunchPayloadOut
    {
        public string action;
        public string mode;
        public string poiId;
        public string poiName;
    }
}

/// <summary>Jawaban asisten. Bentuknya mengikuti kontrak spec section 8.1.</summary>
[Serializable]
public class AssistantAnswer
{
    public string answer;
    public string poi_id;
    public string poi_name;
    // true kalau ada sumber data SIMULASI dipakai menyusun jawaban. UI WAJIB
    // menampilkan penandanya selama ini true (ADR-026): nama dokter dan jam
    // praktek fiktif yang tampil tanpa penanda bisa menyesatkan pasien.
    public bool contains_simulated_data;
}
