using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

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
    private const string PrefUrlKey = "Darsi.POISync.BackendUrl";
    private const string PrefTokenKey = "Darsi.POISync.AdminToken";

    private string backendUrl = "";
    private string adminToken = "";
    private string lastResult = "";

    [MenuItem("DARSI/Sync POIs to Backend")]
    private static void Open()
    {
        var w = GetWindow<POISyncWindow>("POI Sync");
        w.minSize = new Vector2(360, 220);
    }

    private void OnEnable()
    {
        backendUrl = EditorPrefs.GetString(PrefUrlKey, "");
        adminToken = EditorPrefs.GetString(PrefTokenKey, "");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Push field statis POI (id, nama, kategori, gedung, lantai, sinonim) ke backend. " +
            "`status` tidak pernah ikut disentuh — itu tetap dikelola backend (ADR-014).",
            MessageType.Info);

        EditorGUILayout.Space();
        backendUrl = EditorGUILayout.TextField("Backend URL", backendUrl);
        adminToken = EditorGUILayout.PasswordField("Admin Token", adminToken);

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

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(backendUrl) || pois.Count == 0))
        {
            if (GUILayout.Button("Sync to Backend"))
            {
                EditorPrefs.SetString(PrefUrlKey, backendUrl);
                EditorPrefs.SetString(PrefTokenKey, adminToken);
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

    // JsonUtility tidak bisa serialize root array langsung -> dibungkus.
    [Serializable]
    private class PoiSyncPayload
    {
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

        var entries = new PoiSyncEntry[pois.Count];
        for (int i = 0; i < pois.Count; i++)
        {
            var p = pois[i];
            entries[i] = new PoiSyncEntry
            {
                id = p.poiId,
                name = p.EffectiveName,
                category = p.kategori,
                building = p.building,
                floor = p.floor,
                synonyms = p.sinonim ?? Array.Empty<string>(),
            };
        }

        string json = JsonUtility.ToJson(new PoiSyncPayload { pois = entries });
        byte[] body = Encoding.UTF8.GetBytes(json);

        string url = backendUrl.TrimEnd('/') + "/api/poi/sync";
        var request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("X-Admin-Token", adminToken);

        EditorUtility.DisplayProgressBar("POI Sync", $"Mengirim {pois.Count} POI...", 0.5f);
        var op = request.SendWebRequest();
        while (!op.isDone) { /* blocking wait — lihat catatan ponytail di atas */ }
        EditorUtility.ClearProgressBar();

        if (request.result != UnityWebRequest.Result.Success)
        {
            lastResult = $"Gagal ({request.responseCode}): {request.error}\n{request.downloadHandler.text}";
            Debug.LogError($"[POISync] {lastResult}");
            return;
        }

        var res = JsonUtility.FromJson<PoiSyncResponse>(request.downloadHandler.text);
        lastResult = $"Sukses. Synced: {res.synced}, created: {res.created}, updated: {res.updated}.";
        Debug.Log($"[POISync] {lastResult}");
    }
}
