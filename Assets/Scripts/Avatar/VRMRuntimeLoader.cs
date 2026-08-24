using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

/// <summary>
/// Komponen otomatis untuk memuat file .vrm (GLTF 2.0 Binary) langsung ke dalam karakter Avatar.
/// Menggunakan glTFast yang sudah terpasang di proyek untuk me-render 3D avatar seutuhnya.
/// </summary>
[DisallowMultipleComponent]
public class VRMRuntimeLoader : MonoBehaviour
{
    [Header("VRM File Settings")]
    [Tooltip("Path relatif dari file .vrm di dalam Assets.")]
    [SerializeField] private string vrmRelativePath = "Assets/3d-Models-Char-VRM/AvatarSample_A.vrm";

    [Tooltip("GameObject placeholder/stylized yang akan disembunyikan saat model VRM berhasil dimuat.")]
    [SerializeField] private GameObject fallbackVisual;

    private GltfImport _gltfImport;
    private GameObject _vrmInstance;

    private async void Awake()
    {
        await LoadVRMModelAsync();
    }

    /// <summary>
    /// Memuat model VRM secara asinkron dan memasangnya ke hierarki avatar.
    /// </summary>
    public async Task<bool> LoadVRMModelAsync()
    {
        string fullPath = Path.Combine(Application.dataPath, "..", vrmRelativePath);
        fullPath = Path.GetFullPath(fullPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[VRMRuntimeLoader] File VRM tidak ditemukan di: {fullPath}. Menggunakan fallback visual.");
            return false;
        }

        Debug.Log($"[VRMRuntimeLoader] Memuat model VRM dari: {fullPath} ...");

        _gltfImport = new GltfImport();
        
        // Muat file GLTF/VRM
        bool success = await _gltfImport.Load($"file://{fullPath.Replace('\\', '/')}");

        if (!success)
        {
            Debug.LogError("[VRMRuntimeLoader] Gagal mengimpor file VRM via glTFast.");
            return false;
        }

        // Buat GameObject penampung di bawah objek ini
        _vrmInstance = new GameObject("VRM_Character_Model");
        _vrmInstance.transform.SetParent(transform, false);
        _vrmInstance.transform.localPosition = Vector3.zero;
        _vrmInstance.transform.localRotation = Quaternion.identity;

        // Instansiasi scene 3D dari GLTF/VRM
        bool instantiated = await _gltfImport.InstantiateMainSceneAsync(_vrmInstance.transform);

        if (instantiated)
        {
            Debug.Log("[VRMRuntimeLoader] 🎉 BERHASIL memuat dan merender model VRM!");

            // Sembunyikan fallback visual (dummy)
            if (fallbackVisual != null)
            {
                fallbackVisual.SetActive(false);
            }

            // Hubungkan Head bone & Look-At Controller
            var lookAt = GetComponentInParent<AvatarLookAtController>();
            if (lookAt != null)
            {
                Transform headBone = FindHeadBone(_vrmInstance.transform);
                if (headBone != null)
                {
                    lookAt.HeadBone = headBone;
                    Debug.Log($"[VRMRuntimeLoader] Head Bone terhubung ke: {headBone.name}");
                }
            }

            // Perbarui target renderer pada AvatarSafetyFade
            var safetyFade = GetComponentInParent<AvatarSafetyFade>();
            if (safetyFade != null)
            {
                safetyFade.CacheRenderers();
            }

            return true;
        }

        return false;
    }

    private Transform FindHeadBone(Transform root)
    {
        // Cari bone dengan nama Head / J_Bip_C_Head (standar VRM)
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n == "head" || n.Contains("bip_c_head") || n.Contains("head_bone") || n == "j_sec_c_head")
            {
                return t;
            }
        }
        return null;
    }
}
