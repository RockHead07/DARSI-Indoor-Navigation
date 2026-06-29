using UnityEngine;
using UnityEngine.AI; // Namespace untuk NavMeshObstacle dan komponen navigasi AI Unity

/// <summary>
/// Helper script untuk NavMeshObstacle sebagai pola obstacle dinamis.
/// Script ini mendemonstrasikan pendekatan NavMeshObstacle carving untuk menghindari
/// area yang terblokir (misalnya kerumunan orang yang terdeteksi oleh YOLO).
///
/// === Pendekatan NavMeshObstacle vs Full NavMesh Rebaking ===
///
/// NavMeshObstacle dengan carving:
/// - Pro: Ringan, real-time, tidak perlu bake ulang NavMesh
/// - Pro: Cukup aktifkan/nonaktifkan komponen dan atur ukuran
/// - Pro: NavMesh otomatis "dipotong" di area obstacle saat carving aktif
/// - Con: Hanya berbentuk box atau capsule, tidak bisa bentuk kompleks
///
/// Full NavMesh Rebaking:
/// - Pro: Bisa bentuk obstacle apapun (mesh-based)
/// - Pro: NavMesh lebih akurat untuk obstacle kompleks
/// - Con: Berat secara komputasi, tidak cocok untuk real-time
/// - Con: Memerlukan NavMeshSurface dan runtime rebake
///
/// Untuk kasus crowd detection dari YOLO, NavMeshObstacle carving sudah cukup
/// karena bounding box kerumunan bisa direpresentasikan sebagai box obstacle.
/// </summary>
[RequireComponent(typeof(NavMeshObstacle))] // Wajibkan NavMeshObstacle ada di GameObject ini
public class NavMeshObstacleHelper : MonoBehaviour
{
    // Referensi ke komponen NavMeshObstacle yang akan dikontrol
    private NavMeshObstacle obstacle;

    // Section di Inspector untuk menandai bahwa field di bawah ini
    // akan digunakan untuk integrasi YOLO di masa depan
    [Header("Future YOLO Integration")]
    [Tooltip("Ukuran obstacle akan diatur dari data crowd bounding box endpoint /api/human. " +
             "Saat ini digunakan sebagai placeholder untuk integrasi YOLO detection di masa depan.")]
    [SerializeField] private Vector3 defaultSize = new Vector3(1f, 1f, 1f); // Ukuran default obstacle

    /// <summary>
    /// Awake dipanggil saat script pertama kali diinisialisasi.
    /// Mengambil referensi NavMeshObstacle dan mengkonfigurasi carving.
    /// </summary>
    void Awake()
    {
        // Ambil komponen NavMeshObstacle yang terpasang di GameObject ini
        obstacle = GetComponent<NavMeshObstacle>();

        // Aktifkan carving agar NavMeshObstacle memotong NavMesh secara real-time.
        // Carving membuat "lubang" pada NavMesh di area obstacle,
        // sehingga agen navigasi akan menghindari area tersebut.
        obstacle.carving = true;

        // Atur threshold perpindahan minimum sebelum carving diperbarui.
        // Nilai 0.1f berarti obstacle harus bergerak minimal 0.1 unit
        // sebelum NavMesh dipotong ulang. Nilai kecil = lebih responsif tapi lebih berat.
        obstacle.carvingMoveThreshold = 0.1f;

        // Terapkan ukuran default dari Inspector
        obstacle.size = defaultSize;
    }

    /// <summary>
    /// Aktifkan atau nonaktifkan NavMeshObstacle.
    /// Saat dinonaktifkan, NavMesh kembali utuh di area obstacle ini.
    /// Saat diaktifkan, NavMesh akan dipotong sesuai ukuran obstacle.
    ///
    /// Contoh penggunaan masa depan:
    /// - Aktifkan saat YOLO mendeteksi kerumunan di area ini
    /// - Nonaktifkan saat kerumunan sudah bubar
    /// </summary>
    /// <param name="active">True untuk mengaktifkan obstacle, false untuk menonaktifkan.</param>
    public void SetObstacleActive(bool active)
    {
        // Aktifkan/nonaktifkan komponen NavMeshObstacle
        // Saat false, obstacle tidak mempengaruhi NavMesh sama sekali
        obstacle.enabled = active;
        Debug.Log($"[NavMeshObstacleHelper] Obstacle '{gameObject.name}' diset {(active ? "AKTIF" : "NONAKTIF")}");
    }

    /// <summary>
    /// Atur ukuran NavMeshObstacle sesuai data bounding box.
    /// Method ini akan dipanggil dengan data dari YOLO backend (/api/human endpoint)
    /// untuk menyesuaikan ukuran obstacle dengan area kerumunan yang terdeteksi.
    ///
    /// Contoh penggunaan masa depan:
    /// - Terima bounding box dari YOLO: {x, y, w, h}
    /// - Konversi ke Vector3 dan panggil method ini
    /// </summary>
    /// <param name="size">Ukuran obstacle dalam Vector3 (lebar, tinggi, kedalaman).</param>
    public void SetObstacleSize(Vector3 size)
    {
        // Atur ukuran NavMeshObstacle sesuai parameter yang diberikan
        // Size menentukan seberapa besar area NavMesh yang akan dipotong
        obstacle.size = size;
        Debug.Log($"[NavMeshObstacleHelper] Obstacle '{gameObject.name}' ukuran diubah ke: {size}");
    }
}
