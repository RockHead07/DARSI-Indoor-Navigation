using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menerjemahkan teks notifikasi Toast MultiSet dari Bahasa Inggris ke Bahasa Indonesia
/// PADA SAAT DITAMPILKAN, bukan dengan mengubah source SDK.
///
/// Kenapa di sini, bukan edit script SDK?
/// - Sebagian besar pesan (mis. "Localization Failed!", "Localization Success")
///   berasal dari MultiSetSDK.dll yang sudah terkompilasi -> tidak bisa diedit.
/// - Script SDK lain ada di Library/PackageCache yang VOLATILE (tertimpa saat
///   package di-resolve ulang).
/// Dengan menerjemahkan di Text komponen Toast, SEMUA pesan tertangkap di satu titik
/// dan aman dari update package.
///
/// Pemasangan: attach ke GameObject Toast Text (Canvas/ToastPanel/Toast/Text).
/// Komponen Text akan diambil otomatis dari GameObject yang sama jika 'target' kosong.
/// </summary>
[DisallowMultipleComponent]
public class ToastTranslator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Komponen Text toast. Jika kosong, diambil dari GameObject ini.")]
    [SerializeField] private Text target;

    [Header("Debug")]
    [Tooltip("Log pesan toast yang BELUM punya terjemahan, agar mudah ditambahkan ke kamus.")]
    [SerializeField] private bool logUntranslated = true;

    // Toast SDK yang menandai user sampai di tujuan POI. Deteksi arrival ada di
    // MultiSetSDK.dll (privat), jadi toast ini satu-satunya titik arrival yang terlihat
    // di kode editable — dipakai UaaLEntryPoint buat men-trigger event bridge (navigationArrived
    // / arrived:true di arSessionClosed). Harus sama persis dengan kunci di ExactMap.
    private const string ArrivalKeyEn = "You arrived at the destination!";

    // Pesan yang dicocokkan persis (sesudah di-trim). Kunci = Inggris, nilai = Indonesia.
    private static readonly Dictionary<string, string> ExactMap = new Dictionary<string, string>
    {
        // Navigasi
        { ArrivalKeyEn, "Anda telah tiba di tujuan!" },
        { "Problem calculating route",       "Gagal menghitung rute" },

        // Lokalisasi (umumnya dari DLL)
        { "Localization Success",            "Lokalisasi berhasil" },
        { "Localization Successful",         "Lokalisasi berhasil" },
        { "Localization Failed!",            "Lokalisasi gagal!" },
        { "Localization failed",             "Lokalisasi gagal" },
        { "Localization Failed! Try Again.", "Lokalisasi gagal! Coba lagi." },
        { "Localization Pose not found!",    "Pose lokalisasi tidak ditemukan!" },

        // Object tracking
        { "ObjectTracking Failed! Try Again.", "Pelacakan objek gagal! Coba lagi." },

        // Autentikasi / lisensi / map
        { "Auth Failed!",                    "Autentikasi gagal!" },
        { "Auth Success.",                   "Autentikasi berhasil." },
        { "Authentication required",         "Autentikasi diperlukan" },
        { "Authentication successful",       "Autentikasi berhasil" },
        { "Upload failed",                   "Unggah gagal" },
        { "Preparing map data, please wait...", "Menyiapkan data peta, mohon tunggu..." },
        { "Preparing Map data, please wait...", "Menyiapkan data peta, mohon tunggu..." },
        { "Validating license, please wait...", "Memvalidasi lisensi, mohon tunggu..." },

        // Training (mode latihan SDK)
        { "Please follow the line.",         "Silakan ikuti garis." },
        { "Training completed!",             "Latihan selesai!" },

        // Default placeholder
        { "MultiSet Alert",                  "Pemberitahuan" },
    };

    // Pesan dinamis: cocokkan berdasarkan AWALAN (prefix) Inggris, ganti dengan
    // awalan Indonesia sambil mempertahankan ekor dinamisnya (kode error, dsb).
    private static readonly (string en, string id)[] PrefixRules =
    {
        ("Localization Failed! responseCode", "Lokalisasi gagal! kode"),
        ("Localization Pose not found! responseCode", "Pose lokalisasi tidak ditemukan! kode"),
        ("Localization error:",  "Kesalahan lokalisasi:"),
        ("Localization Failed!", "Lokalisasi gagal!"),   // fallback bila ada teks setelahnya
        ("Upload failed",        "Unggah gagal"),
        ("Download failed",      "Unduhan gagal"),
    };

    // Mencegah loop: nilai terakhir yang kita tulis sendiri ke Text.
    private string _lastApplied;
    // Agar log "untranslated" tidak spam tiap frame.
    private readonly HashSet<string> _loggedUnknown = new HashSet<string>();

    private void Awake()
    {
        if (target == null) target = GetComponent<Text>();
        if (target == null)
        {
            Debug.LogError("[ToastTranslator] Komponen Text tidak ditemukan. Pasang script ini di GameObject Toast Text.");
            enabled = false;
        }
    }

    // LateUpdate: dijalankan setelah Update (tempat ToastManager meng-set teks),
    // sehingga terjemahan tampil di frame yang sama tanpa kedipan Inggris.
    private void LateUpdate()
    {
        string current = target.text;

        // Sudah sama dengan output kita / tidak berubah -> lewati.
        if (current == _lastApplied) return;

        // Toast berubah = toast baru muncul. Kalau ini toast "sampai tujuan", beri tahu
        // UaaLEntryPoint (guard-nya yang memastikan hanya sesi navigate aktif yang terpicu).
        if (current.Trim() == ArrivalKeyEn)
            UaaLEntryPoint.Instance?.ReportArrivalAtActiveTarget();

        string translated = Translate(current);
        _lastApplied = translated;

        if (translated != current)
        {
            target.text = translated;
        }
    }

    private string Translate(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        string key = raw.Trim();

        // 1) Cocokkan persis
        if (ExactMap.TryGetValue(key, out string exact))
        {
            return exact;
        }

        // 2) Cocokkan awalan (untuk pesan dengan ekor dinamis)
        foreach (var rule in PrefixRules)
        {
            if (key.StartsWith(rule.en, System.StringComparison.OrdinalIgnoreCase))
            {
                string tail = key.Substring(rule.en.Length); // sisa setelah prefix (mis. " 401")
                return rule.id + tail;
            }
        }

        // 3) Tidak ada terjemahan: biarkan apa adanya, tapi catat agar bisa ditambahkan.
        if (logUntranslated && _loggedUnknown.Add(key))
        {
            Debug.Log($"[ToastTranslator] Belum ada terjemahan untuk toast: \"{key}\" " +
                      "(tambahkan ke ExactMap/PrefixRules di ToastTranslator.cs).");
        }
        return raw;
    }
}
