using UnityEngine;

/// <summary>
/// Komponen data Point of Interest (POI).
/// Tempelkan pada setiap GameObject POI di scene.
/// </summary>
public class POIData : MonoBehaviour
{
    [Tooltip("Nama POI. Jika kosong, akan fallback ke gameObject.name.")]
    public string poiName;

    [Tooltip("Kategori POI, misalnya: ruangan, toilet, kantin, dll.")]
    public string kategori;

    [Tooltip("Sinonim atau alias untuk POI ini, mempermudah pencarian fuzzy.")]
    public string[] sinonim;

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
}
