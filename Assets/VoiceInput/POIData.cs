using UnityEngine;

/// <summary>
/// Komponen data Point of Interest (POI).
/// Tempelkan pada setiap GameObject POI di scene.
/// </summary>
public class POIData : MonoBehaviour
{
    [Tooltip("ID stabil POI (GUID), dibuat sekali dan tidak berubah walau nama di-rename. " +
             "Dipakai sebagai key sync ke backend (ADR-014, T3.4-L1). Kosong pada POI lama " +
             "sampai di-generate lewat DARSI > Sync POIs to Backend.")]
    public string poiId;

    [Tooltip("Nama POI. Jika kosong, akan fallback ke gameObject.name.")]
    public string poiName;

    [Tooltip("Kategori POI, misalnya: ruangan, toilet, kantin, dll.")]
    public string kategori;

    [Tooltip("Sinonim atau alias untuk POI ini, mempermudah pencarian fuzzy.")]
    public string[] sinonim;

    [Tooltip("Nama gedung (ADR-014: Unity = sumber kebenaran untuk field statis ini).")]
    public string building;

    [Tooltip("Lantai (ADR-014: Unity = sumber kebenaran untuk field statis ini).")]
    public string floor;

    /// <summary>
    /// Mengembalikan poiName jika diisi, atau gameObject.name sebagai fallback.
    /// </summary>
    public string EffectiveName
    {
        get
        {
            return string.IsNullOrWhiteSpace(poiName) ? gameObject.name : poiName;
        }
    }

    // Auto-assign sekali saat component baru ditambah di Editor (tidak retroaktif untuk
    // POI lama — itu ditangani tombol "Assign Missing IDs" di POISyncWindow).
    private void Reset()
    {
        if (string.IsNullOrEmpty(poiId))
            poiId = System.Guid.NewGuid().ToString();
    }
}
