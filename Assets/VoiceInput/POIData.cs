using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Komponen data Point of Interest (POI) — metadata DARSI yang tidak dimiliki SDK MultiSet.
/// Tempelkan pada GameObject yang sama dengan komponen <see cref="POI"/> milik SDK.
///
/// ADR-021: komponen ini HANYA menyimpan data yang benar-benar dimilikinya
/// (poiId, kategori, sinonim). Nama / lantai / gedung DITURUNKAN dari pemilik sahnya,
/// tidak disalin — salinan manual terbukti melenceng di lapangan (POI RSI masih menyimpan
/// nama scene kampus lama: "Ruang Dosen", "Perpustakaan", "Lab Teori 202").
/// </summary>
public class POIData : MonoBehaviour
{
    [Tooltip("ID stabil POI (GUID), dibuat sekali dan tidak berubah walau nama di-rename. " +
             "Dipakai sebagai key sync ke backend (ADR-014, T3.4-L1).")]
    public string poiId;

    [Tooltip("Kategori POI (kanonik, ADR-016). Disimpan di sini karena POI.type milik SDK " +
             "cuma enum kasar 15 nilai — tidak bisa membedakan IGD / Farmasi / Radiologi.")]
    public string kategori;

    [Tooltip("Sinonim atau alias untuk POI ini, mempermudah pencarian fuzzy.")]
    public string[] sinonim;

    // ponytail: satu gedung, jadi satu const — bukan field per-POI yang harus diisi 11 kali
    // dan bisa melenceng. Kalau DARSI benar-benar melayani >1 gedung, promosikan ini jadi
    // satu komponen scene-level; JANGAN jadikan field per-POI lagi.
    public const string BuildingName = "RS Islam Ahmad Yani";

    // Konvensi penamaan hierarki: "[Ground] Lift", "[Lantai1] IGD".
    private static readonly Regex FloorPrefix = new Regex(@"^\s*\[([^\]]+)\]\s*");

    private POI _sdkPoi;
    private POI SdkPoi => _sdkPoi != null ? _sdkPoi : (_sdkPoi = GetComponent<POI>());

    /// <summary>
    /// Nama POI, DITURUNKAN dari <c>POI.poiName</c> milik SDK — satu-satunya tempat nama
    /// ini diedit (SDK sendiri menyalinnya ke <c>listTitle</c> saat Awake, jadi listTitle
    /// adalah turunan runtime, bukan pemilik). Fallback ke nama GameObject tanpa prefiks
    /// lantai kalau komponen POI belum terpasang.
    /// </summary>
    public string EffectiveName
    {
        get
        {
            string fromSdk = SdkPoi != null ? SdkPoi.poiName : null;
            if (!string.IsNullOrWhiteSpace(fromSdk))
                return fromSdk.Trim();
            return FloorPrefix.Replace(gameObject.name, "").Trim();
        }
    }

    /// <summary>Gedung — konstanta scene (ADR-021), bukan data per-POI.</summary>
    public string Building => BuildingName;

    /// <summary>
    /// Lantai, DITURUNKAN dari prefiks nama GameObject ("[Lantai1] IGD" -> "Lantai 1").
    /// Sengaja mengembalikan null (bukan tebakan default) kalau konvensinya dilanggar —
    /// POISyncWindow memblokir sync dan menyebut POI-nya, supaya salah lantai ketahuan
    /// di Editor, bukan diam-diam terkirim ke backend.
    /// </summary>
    public string Floor
    {
        get
        {
            var m = FloorPrefix.Match(gameObject.name);
            if (!m.Success) return null;
            // "Lantai1" -> "Lantai 1"; "Ground" tetap "Ground".
            return Regex.Replace(m.Groups[1].Value.Trim(), @"(?<=\D)(?=\d)", " ");
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
