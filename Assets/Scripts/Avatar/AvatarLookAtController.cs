using UnityEngine;

/// <summary>
/// Mengendalikan tatapan kepala dan leher Avatar 3D agar menatap ke Camera.main secara akurat dan natural.
/// Mendukung kalibrasi pitch (atas-bawah) dan yaw (kiri-kanan) secara presisi.
/// </summary>
[DisallowMultipleComponent]
public class AvatarLookAtController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Target yang ditatap. Jika kosong, otomatis mencari Camera.main.")]
    [SerializeField] private Transform targetOverride;

    [Header("Bone References")]
    [Tooltip("Transform tulang kepala.")]
    [SerializeField] private Transform headBone;
    [Tooltip("Transform tulang leher (opsional).")]
    [SerializeField] private Transform neckBone;

    [Header("Angle Limits & Tuning")]
    [Tooltip("Sudut rotasi horizontal maksimal (derajat).")]
    [SerializeField] private float maxYawAngle = 60f;
    [Tooltip("Sudut rotasi vertikal maksimal (derajat).")]
    [SerializeField] private float maxPitchAngle = 35f;
    [Tooltip("Kecepatan lerp penghalusan tatapan.")]
    [SerializeField] private float lookSpeed = 8.0f;
    [Range(0f, 1f)] [SerializeField] private float overallWeight = 1.0f;

    [Header("Inversion / Calibration")]
    [Tooltip("Pembalikan sumbu pitch (atas/bawah).")]
    [SerializeField] private bool invertPitch = true;
    [Tooltip("Pembalikan sumbu yaw (kiri/kanan).")]
    [SerializeField] private bool invertYaw = false;

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

        float targetWeight = (_lookAtEnabled && _target != null) ? overallWeight : 0f;
        _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * lookSpeed);
    }

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

        // Gunakan parent dari head bone sebagai referensi lokal
        Transform refTransform = headBone.parent != null ? headBone.parent : transform;
        Vector3 dirLocal = refTransform.InverseTransformDirection(dirToTargetWorld);

        // Yaw: Kiri / Kanan
        float yaw = Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg;
        // Pitch: Atas / Bawah
        float pitch = Mathf.Asin(Mathf.Clamp(dirLocal.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Terapkan kalibrasi orientasi
        if (invertPitch) pitch = -pitch;
        if (invertYaw) yaw = -yaw;

        // Batasi sudut gerak leher
        yaw = Mathf.Clamp(yaw, -maxYawAngle, maxYawAngle) * _currentWeight;
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle) * _currentWeight;

        if (neckBone != null)
        {
            // Leher menanggung 35% rotasi, kepala menanggung 65% rotasi
            Quaternion neckRotOffset = Quaternion.Euler(pitch * 0.35f, yaw * 0.35f, 0f);
            Quaternion headRotOffset = Quaternion.Euler(pitch * 0.65f, yaw * 0.65f, 0f);

            neckBone.localRotation = Quaternion.Slerp(neckBone.localRotation, neckRotOffset * _initialNeckLocalRot, Time.deltaTime * lookSpeed);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, headRotOffset * _initialHeadLocalRot, Time.deltaTime * lookSpeed);
        }
        else
        {
            Quaternion headRotOffset = Quaternion.Euler(pitch, yaw, 0f);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, headRotOffset * _initialHeadLocalRot, Time.deltaTime * lookSpeed);
        }
    }
}
