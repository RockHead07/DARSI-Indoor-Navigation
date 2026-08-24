using UnityEngine;

/// <summary>
/// Mengendalikan tatapan kepala dan leher Avatar 3D agar menatap ke Camera.main secara akurat dan natural.
/// Dijalankan pada LateUpdate() agar tidak ditimpa oleh evaluasi rig/skinning glTFast atau Animator.
/// </summary>
[DisallowMultipleComponent]
public class AvatarLookAtController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Target yang ditatap. Jika kosong, otomatis mencari Camera.main.")]
    [SerializeField] private Transform targetOverride;

    [Header("Bone References")]
    [Tooltip("Transform tulang leher/kepala.")]
    [SerializeField] private Transform headBone;
    [Tooltip("Transform tulang leher (opsional, untuk distribusi rotasi lebih natural).")]
    [SerializeField] private Transform neckBone;

    [Header("Angle Limits & Tuning")]
    [Tooltip("Sudut rotasi horizontal maksimal (derajat). Mencegah leher berputar ke belakang.")]
    [SerializeField] private float maxYawAngle = 60f;
    [Tooltip("Sudut rotasi vertikal maksimal (derajat).")]
    [SerializeField] private float maxPitchAngle = 35f;
    [Tooltip("Kecepatan lerp penghalusan tatapan.")]
    [SerializeField] private float lookSpeed = 6.0f;
    [Range(0f, 1f)] [SerializeField] private float overallWeight = 1.0f;

    [Header("Forward Axis Offset")]
    [Tooltip("Arah forward lokal tulang kepala (biasanya Vector3.forward atau Vector3.up tergantung rig).")]
    [SerializeField] private Vector3 headForwardLocalAxis = Vector3.forward;

    private Transform _target;
    private float _currentWeight = 0f;
    private bool _lookAtEnabled = true;
    private Quaternion _initialHeadLocalRot = Quaternion.identity;
    private Quaternion _initialNeckLocalRot = Quaternion.identity;
    private bool _hasInitialRot = false;

    public bool IsLookAtEnabled
    {
        get => _lookAtEnabled;
        set => _lookAtEnabled = value;
    }

    public Transform HeadBone
    {
        get => headBone;
        set
        {
            headBone = value;
            if (headBone != null)
            {
                _initialHeadLocalRot = headBone.localRotation;
                _hasInitialRot = true;
            }
        }
    }

    public Transform NeckBone
    {
        get => neckBone;
        set
        {
            neckBone = value;
            if (neckBone != null)
            {
                _initialNeckLocalRot = neckBone.localRotation;
            }
        }
    }

    private void Awake()
    {
        ResolveTarget();
        if (headBone != null && !_hasInitialRot)
        {
            _initialHeadLocalRot = headBone.localRotation;
            _hasInitialRot = true;
        }
        if (neckBone != null)
        {
            _initialNeckLocalRot = neckBone.localRotation;
        }
    }

    private void OnEnable()
    {
        ResolveTarget();
    }

    private void ResolveTarget()
    {
        if (targetOverride != null)
        {
            _target = targetOverride;
        }
        else if (Camera.main != null)
        {
            _target = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (_target == null)
        {
            ResolveTarget();
        }

        // Smooth fade-in/fade-out bobot tatapan
        float targetWeight = (_lookAtEnabled && _target != null) ? overallWeight : 0f;
        _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * lookSpeed);
    }

    /// <summary>
    /// Eksekusi di LateUpdate() WAJIB untuk manipulasi tulang setelah update skinning/glTF.
    /// </summary>
    private void LateUpdate()
    {
        if (headBone == null || _target == null || _currentWeight < 0.01f) return;

        if (!_hasInitialRot)
        {
            _initialHeadLocalRot = headBone.localRotation;
            if (neckBone != null) _initialNeckLocalRot = neckBone.localRotation;
            _hasInitialRot = true;
        }

        ApplyNaturalHeadLookAt();
    }

    private void ApplyNaturalHeadLookAt()
    {
        Vector3 headWorldPos = headBone.position;
        Vector3 targetWorldPos = _target.position;
        Vector3 dirToTargetWorld = (targetWorldPos - headWorldPos).normalized;

        if (dirToTargetWorld.sqrMagnitude < 0.001f) return;

        // Vektor arah relatif terhadap koordinat avatar root
        Vector3 dirLocalToAvatar = transform.InverseTransformDirection(dirToTargetWorld);

        // Hitung sudut yaw (horizontal) dan pitch (vertikal) dalam derajat
        float yaw = Mathf.Atan2(dirLocalToAvatar.x, dirLocalToAvatar.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(Mathf.Clamp(dirLocalToAvatar.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Batasi sudut agar kepala tidak berputar ke belakang atau terpelintir
        yaw = Mathf.Clamp(yaw, -maxYawAngle, maxYawAngle);
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

        // Terapkan bobot tatapan (fade halus saat menjauh/mendekat)
        yaw *= _currentWeight;
        pitch *= _currentWeight;

        // Rotasi tambahan offset tatapan
        Quaternion lookOffset = Quaternion.Euler(pitch, yaw, 0f);

        // Jika ada tulang leher, bagi separuh rotasi ke leher dan separuh ke kepala
        if (neckBone != null)
        {
            Quaternion neckOffset = Quaternion.Euler(pitch * 0.4f, yaw * 0.4f, 0f);
            Quaternion headOnlyOffset = Quaternion.Euler(pitch * 0.6f, yaw * 0.6f, 0f);

            neckBone.localRotation = Quaternion.Slerp(neckBone.localRotation, neckOffset * _initialNeckLocalRot, Time.deltaTime * lookSpeed);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, headOnlyOffset * _initialHeadLocalRot, Time.deltaTime * lookSpeed);
        }
        else
        {
            // Terapkan seluruh rotasi ke tulang kepala
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, lookOffset * _initialHeadLocalRot, Time.deltaTime * lookSpeed);
        }
    }
}
