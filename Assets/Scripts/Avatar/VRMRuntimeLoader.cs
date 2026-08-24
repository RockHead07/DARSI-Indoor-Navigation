using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

/// <summary>
/// Komponen otomatis untuk memuat file .vrm (GLTF 2.0 Binary) langsung ke dalam karakter Avatar.
/// Mengatur orientasi hadap avatar ke arah kamera, menghubungkan tulang leher/kepala ke LookAt,
/// serta merelaksasikan lengan dari T-Pose kaku menjadi pose berdiri santai (Natural Idle).
/// </summary>
[DisallowMultipleComponent]
public class VRMRuntimeLoader : MonoBehaviour
{
    [Header("VRM File Settings")]
    [Tooltip("Path relatif dari file .vrm di dalam Assets.")]
    [SerializeField] private string vrmRelativePath = "Assets/3d-Models-Char-VRM/AvatarSample_A.vrm";

    [Header("Visual & Orientation Settings")]
    [Tooltip("Offset rotasi model VRM agar bagian depan (wajah/dada) menghadap ke kamera.")]
    [SerializeField] private Vector3 vrmRotationOffset = new Vector3(0, 180f, 0);

    [Tooltip("GameObject placeholder/stylized yang akan disembunyikan saat model VRM berhasil dimuat.")]
    [SerializeField] private GameObject fallbackVisual;

    [Header("Natural Pose Settings")]
    [Tooltip("Otomatis menurunkan lengan dari T-Pose ke pose berdiri santai.")]
    [SerializeField] private bool relaxArmTPose = true;
    [SerializeField] private float armDropAngle = 65f;
    [SerializeField] private bool enableBreathingSway = true;

    private GltfImport _gltfImport;
    private GameObject _vrmInstance;
    private Transform _leftUpperArm;
    private Transform _rightUpperArm;
    private Transform _leftLowerArm;
    private Transform _rightLowerArm;
    private Quaternion _leftArmInitRot = Quaternion.identity;
    private Quaternion _rightArmInitRot = Quaternion.identity;
    private Quaternion _leftForearmInitRot = Quaternion.identity;
    private Quaternion _rightForearmInitRot = Quaternion.identity;
    private bool _bonesCached = false;

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
        bool success = await _gltfImport.Load($"file://{fullPath.Replace('\\', '/')}");

        if (!success)
        {
            Debug.LogError("[VRMRuntimeLoader] Gagal mengimpor file VRM via glTFast.");
            return false;
        }

        // Buat GameObject penampung
        _vrmInstance = new GameObject("VRM_Character_Model");
        _vrmInstance.transform.SetParent(transform, false);
        _vrmInstance.transform.localPosition = Vector3.zero;
        _vrmInstance.transform.localRotation = Quaternion.Euler(vrmRotationOffset);

        // Instansiasi hierarki 3D dari GLTF/VRM
        bool instantiated = await _gltfImport.InstantiateMainSceneAsync(_vrmInstance.transform);

        if (instantiated)
        {
            Debug.Log("[VRMRuntimeLoader] 🎉 BERHASIL memuat dan merender model VRM!");

            // Sembunyikan fallback visual (dummy)
            if (fallbackVisual != null)
            {
                fallbackVisual.SetActive(false);
            }

            // Temukan tulang-tulang utama VRM
            CacheBones(_vrmInstance.transform);

            // Hubungkan Head bone & Neck bone ke AvatarLookAtController
            var lookAt = GetComponentInParent<AvatarLookAtController>();
            if (lookAt != null)
            {
                Transform headBone = FindBoneByName(_vrmInstance.transform, "bip_c_head", "head");
                if (headBone != null)
                {
                    lookAt.HeadBone = headBone;
                    Debug.Log($"[VRMRuntimeLoader] Head Bone terhubung ke: {headBone.name}");
                }
            }

            // Perbarui renderer pada AvatarSafetyFade
            var safetyFade = GetComponentInParent<AvatarSafetyFade>();
            if (safetyFade != null)
            {
                safetyFade.CacheRenderers();
            }

            return true;
        }

        return false;
    }

    private void CacheBones(Transform root)
    {
        _leftUpperArm = FindBoneByName(root, "bip_l_upperarm", "leftupperarm", "arm_l");
        _rightUpperArm = FindBoneByName(root, "bip_r_upperarm", "rightupperarm", "arm_r");
        _leftLowerArm = FindBoneByName(root, "bip_l_lowerarm", "leftlowerarm", "forearm_l");
        _rightLowerArm = FindBoneByName(root, "bip_r_lowerarm", "rightlowerarm", "forearm_r");

        if (_leftUpperArm != null) _leftArmInitRot = _leftUpperArm.localRotation;
        if (_rightUpperArm != null) _rightArmInitRot = _rightUpperArm.localRotation;
        if (_leftLowerArm != null) _leftForearmInitRot = _leftLowerArm.localRotation;
        if (_rightLowerArm != null) _rightForearmInitRot = _rightLowerArm.localRotation;

        _bonesCached = true;
        Debug.Log($"[VRMRuntimeLoader] Arm bones cached: Left={_leftUpperArm?.name}, Right={_rightUpperArm?.name}");
    }

    private void LateUpdate()
    {
        if (!_bonesCached || !relaxArmTPose) return;

        // Pose relaksasi lengan (menurunkan tangan dari T-Pose ke samping tubuh)
        float breathe = enableBreathingSway ? Mathf.Sin(Time.time * 2.0f) * 1.5f : 0f;

        if (_leftUpperArm != null)
        {
            Quaternion relaxLeft = Quaternion.Euler(0, 0, -(armDropAngle + breathe));
            _leftUpperArm.localRotation = relaxLeft * _leftArmInitRot;
        }

        if (_rightUpperArm != null)
        {
            Quaternion relaxRight = Quaternion.Euler(0, 0, (armDropAngle + breathe));
            _rightUpperArm.localRotation = relaxRight * _rightArmInitRot;
        }

        if (_leftLowerArm != null)
        {
            Quaternion bendLeft = Quaternion.Euler(0, 10f, -5f);
            _leftLowerArm.localRotation = bendLeft * _leftForearmInitRot;
        }

        if (_rightLowerArm != null)
        {
            Quaternion bendRight = Quaternion.Euler(0, -10f, 5f);
            _rightLowerArm.localRotation = bendRight * _rightForearmInitRot;
        }
    }

    private Transform FindBoneByName(Transform root, params string[] matchPatterns)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            foreach (var pattern in matchPatterns)
            {
                if (n.Contains(pattern.ToLower()))
                {
                    return t;
                }
            }
        }
        return null;
    }
}
