using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector untuk POIData (ADR-021).
///
/// Dua hal:
/// 1. `kategori` jadi dropdown kanonik, bukan ketikan bebas — mencegah typo di hulu
///    (ADR-016 sudah mengantisipasi ini). Validasi backend TETAP jaring pengaman terakhir.
/// 2. Nama / lantai / gedung ditampilkan READ-ONLY beserta asal turunannya, supaya jelas
///    bahwa itu bukan input — dan supaya lantai yang gagal diturunkan kelihatan mencolok.
/// </summary>
[CustomEditor(typeof(POIData))]
[CanEditMultipleObjects]
public class POIDataEditor : Editor
{
    // HARUS mirror POI_CATEGORIES di darsi-backend/app/main.py (ADR-016).
    // Menambah/rename kategori = edit TIGA tempat: backend, WebView categoryIcon(), dan sini.
    static readonly string[] Categories =
    {
        // Klinis / instalasi medis
        "IGD", "Poliklinik", "Farmasi", "Laboratorium", "Radiologi",
        "Rawat Inap", "Kamar Operasi", "ICU", "Ruang Bersalin", "Fisioterapi",
        // Administrasi / layanan
        "Pendaftaran", "Kasir", "Informasi", "BPJS", "Rekam Medis",
        // Fasilitas umum
        "Musholla", "Toilet", "Kantin", "ATM", "Parkir", "Ruang Tunggu",
        // Sirkulasi / wayfinding
        "Lift", "Tangga", "Pintu Masuk",
        // Umum
        "Umum", "Administrasi",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("poiId"));
        DrawCategoryDropdown(serializedObject.FindProperty("kategori"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sinonim"), true);

        serializedObject.ApplyModifiedProperties();

        if (targets.Length == 1)
            DrawDerived((POIData)target);
    }

    private static void DrawDerived(POIData poi)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Diturunkan (read-only, ADR-021)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(
                new GUIContent("Nama", "Dari POI.poiName (komponen SDK MultiSet di GameObject ini)."),
                poi.EffectiveName);
            EditorGUILayout.TextField(
                new GUIContent("Gedung", "Konstanta scene POIData.BuildingName."),
                poi.Building);
            EditorGUILayout.TextField(
                new GUIContent("Lantai", "Dari prefiks nama GameObject, mis. \"[Lantai1] IGD\"."),
                poi.Floor ?? "—");
        }

        if (poi.GetComponent<POI>() == null)
            EditorGUILayout.HelpBox(
                "Komponen POI (SDK MultiSet) tidak ada di GameObject ini — nama terpaksa " +
                "fallback ke nama GameObject.", MessageType.Warning);

        if (string.IsNullOrEmpty(poi.Floor))
            EditorGUILayout.HelpBox(
                $"Lantai tidak bisa diturunkan dari nama '{poi.gameObject.name}'. Rename jadi " +
                "\"[Lantai1] Nama\" atau \"[Ground] Nama\" — sync ke backend diblokir sampai ini benar.",
                MessageType.Error);
    }

    private static void DrawCategoryDropdown(SerializedProperty prop)
    {
        string current = prop.stringValue;
        int idx = Array.IndexOf(Categories, current);

        if (idx >= 0)
        {
            int picked = EditorGUILayout.Popup("Kategori", idx, Categories);
            if (picked != idx) prop.stringValue = Categories[picked];
            return;
        }

        // Nilai sekarang TIDAK kanonik (mis. "Room" warisan lama, atau kosong).
        // Jangan diam-diam diganti — tampilkan apa adanya + peringatan, biar user yang memilih.
        string shown = string.IsNullOrWhiteSpace(current) ? "(kosong)" : current;
        EditorGUILayout.HelpBox(
            $"Kategori '{shown}' TIDAK kanonik — sync ke backend akan ditolak (422). Pilih dari daftar.",
            MessageType.Error);

        var options = new List<string> { $"◆ {shown} (tidak valid)" };
        options.AddRange(Categories);

        int choice = EditorGUILayout.Popup("Kategori", 0, options.ToArray());
        if (choice > 0) prop.stringValue = Categories[choice - 1];
    }

    /// <summary>Self-check turunan: cetak nilai untuk semua POI di scene aktif.</summary>
    [MenuItem("DARSI/Debug/Log derived POI fields")]
    private static void LogDerived()
    {
        var sb = new System.Text.StringBuilder("[POIData] nilai turunan (ADR-021):\n");
        foreach (var poi in FindObjectsByType<POIData>(FindObjectsSortMode.None))
            sb.AppendLine($"  {poi.gameObject.name,-34} nama='{poi.EffectiveName}' " +
                          $"lantai='{poi.Floor ?? "GAGAL"}' kategori='{poi.kategori}'");
        Debug.Log(sb.ToString());
    }
}
