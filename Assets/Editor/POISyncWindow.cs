using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DARSI T3.4-L2 — push POI statis (id, nama, kategori, gedung, lantai, sinonim) dari
/// scene Unity ke backend. Satu arah (Unity -> Backend), tidak pernah menyentuh `status`
/// (itu domain backend, ADR-014). Editor-only, tidak ikut ke build player.
///
/// ponytail: blocking wait saat request (bukan async/polling) — cukup untuk ~20 POI dan
/// tombol admin yang dipakai jarang; upgrade ke EditorApplication.update polling kalau
/// jumlah POI/besar payload bikin ini terasa lambat.
/// </summary>
public class POISyncWindow : EditorWindow
{
    private const string PrefUrlKey = "Darsi.POISync.SupabaseUrl";
    private const string PrefKeyKey = "Darsi.POISync.ServiceKey";

    private string supabaseUrl = "";
    private string serviceKey = "";
    private string lastResult = "";

    [MenuItem("DARSI/Sync POIs to Backend")]
    private static void Open()
    {
        var w = GetWindow<POISyncWindow>("POI Sync");
        w.minSize = new Vector2(360, 220);
    }

    private void OnEnable()
    {
        supabaseUrl = EditorPrefs.GetString(PrefUrlKey, "");
        serviceKey = EditorPrefs.GetString(PrefKeyKey, "");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Push field statis POI (id, nama, kategori, gedung, lantai, sinonim) ke Supabase " +
            "(RPC sync_pois). `status`/is_popular/deskripsi/foto tak pernah ikut disentuh — " +
            "tetap dikelola DB (ADR-014).",
            MessageType.Info);

        EditorGUILayout.Space();
        supabaseUrl = EditorGUILayout.TextField("Supabase URL", supabaseUrl);
        serviceKey = EditorGUILayout.PasswordField("Service Role Key", serviceKey);

        EditorGUILayout.Space();
        var pois = FindAllPOIs();
        EditorGUILayout.LabelField($"POI ditemukan di scene aktif: {pois.Count}");

        int missingIds = pois.FindAll(p => string.IsNullOrEmpty(p.poiId)).Count;
        if (missingIds > 0)
            EditorGUILayout.HelpBox($"{missingIds} POI belum punya ID stabil.", MessageType.Warning);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(missingIds == 0))
        {
            if (GUILayout.Button("Assign Missing IDs"))
                AssignMissingIds(pois);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(supabaseUrl) || pois.Count == 0))
        {
            if (GUILayout.Button("Sync to Backend"))
            {
                EditorPrefs.SetString(PrefUrlKey, supabaseUrl);
                EditorPrefs.SetString(PrefKeyKey, serviceKey);
                SyncToBackend(pois);
            }
        }

        if (!string.IsNullOrEmpty(lastResult))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lastResult, MessageType.None);
        }
    }

    private static List<POIData> FindAllPOIs()
    {
#if UNITY_2023_1_OR_NEWER
        return new List<POIData>(FindObjectsByType<POIData>(FindObjectsSortMode.None));
#else
        return new List<POIData>(FindObjectsOfType<POIData>());
#endif
    }

    private void AssignMissingIds(List<POIData> pois)
    {
        int assigned = 0;
        foreach (var poi in pois)
        {
            if (string.IsNullOrEmpty(poi.poiId))
            {
                Undo.RecordObject(poi, "Assign POI Id");
                poi.poiId = Guid.NewGuid().ToString();
                EditorUtility.SetDirty(poi);
                assigned++;
            }
        }

        if (assigned > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        lastResult = $"{assigned} ID baru di-generate. Simpan scene sebelum sync.";
    }

    [Serializable]
    private class PoiSyncEntry
    {
        public string id;
        public string name;
        public string category;
        public string building;
        public string floor;
        public string[] synonyms;
    }

    // JsonUtility tidak bisa serialize root array langsung -> dibungkus. Field HARUS
    // bernama "payload": itu nama argumen RPC sync_pois(payload jsonb); PostgREST
    // memetakan {"payload":[...]} ke argumen fungsi.
    [Serializable]
    private class PoiSyncPayload
    {
        public PoiSyncEntry[] payload;
        public PoiSyncEntry[] pois;
    }

    [Serializable]
    private class PoiSyncResponse
    {
        public int synced;
        public int created;
        public int updated;
    }

    private void SyncToBackend(List<POIData> pois)
    {
        var missing = pois.FindAll(p => string.IsNullOrEmpty(p.poiId));
        if (missing.Count > 0)
        {
            lastResult = "Masih ada POI tanpa ID. Klik 'Assign Missing IDs' dulu.";
            return;
        }

        // ADR-021: `floor` diturunkan dari prefiks nama GameObject. Kalau konvensinya dilanggar
        // hasilnya null — BLOKIR sync dan sebut POI-nya, jangan kirim lantai kosong ke backend.
        var badFloor = pois.FindAll(p => string.IsNullOrEmpty(p.Floor));
        if (badFloor.Count > 0)
        {
            var names = badFloor.ConvertAll(p => p.gameObject.name);
            lastResult = $"{badFloor.Count} POI tanpa prefiks lantai pada namanya — rename jadi " +
                         $"\"[Lantai1] Nama\" atau \"[Ground] Nama\":\n  " + string.Join("\n  ", names);
            Debug.LogError($"[POISync] {lastResult}");
            return;
        }

        var entries = new PoiSyncEntry[pois.Count];
        for (int i = 0; i < pois.Count; i++)
        {
            var p = pois[i];
            entries[i] = new PoiSyncEntry
            {
                id = p.poiId,
                name = p.EffectiveName,
                category = p.kategori,
                building = p.Building,
                floor = p.Floor,
                synonyms = p.sinonim ?? Array.Empty<string>(),
            };
        }

        string json = JsonUtility.ToJson(new PoiSyncPayload { payload = entries, pois = entries });
        byte[] body = Encoding.UTF8.GetBytes(json);

        bool isSupabase = supabaseUrl.Contains("supabase.co");
        string url = isSupabase
            ? supabaseUrl.TrimEnd('/') + "/rest/v1/rpc/sync_pois"
            : supabaseUrl.TrimEnd('/') + "/api/poi/sync";

        EditorUtility.DisplayProgressBar("POI Sync", $"Mengirim {pois.Count} POI...", 0.5f);
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";

            if (isSupabase)
            {
                request.Headers["apikey"] = serviceKey;
                request.Headers["Authorization"] = "Bearer " + serviceKey;
            }
            else
            {
                string token = string.IsNullOrEmpty(serviceKey) ? "darsi-admin-token" : serviceKey;
                request.Headers["x-admin-token"] = token;
            }

            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            request.ContentLength = body.Length;

            using (var stream = request.GetRequestStream())
                stream.Write(body, 0, body.Length);

            using var response = (HttpWebResponse)request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string text = reader.ReadToEnd();

            var res = JsonUtility.FromJson<PoiSyncResponse>(text);
            lastResult = $"Sukses. Synced: {res.synced}, created: {res.created}, updated: {res.updated}.";
            Debug.Log($"[POISync] {lastResult}");
        }
        catch (WebException ex)
        {
            int code = 0;
            string detail = "";
            if (ex.Response is HttpWebResponse errResponse)
            {
                code = (int)errResponse.StatusCode;
                using var reader = new StreamReader(errResponse.GetResponseStream(), Encoding.UTF8);
                detail = reader.ReadToEnd();
            }
            lastResult = $"Gagal ({code}): {ex.Message}\n{detail}";
            Debug.LogError($"[POISync] {lastResult}");
        }
        catch (Exception ex)
        {
            lastResult = $"Gagal: {ex.Message}";
            Debug.LogError($"[POISync] {lastResult}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
