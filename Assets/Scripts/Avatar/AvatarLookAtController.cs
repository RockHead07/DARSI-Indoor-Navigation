using UnityEngine;

/// <summary>
/// Kontroler tatapan kepala Avatar 3D (Look-At Tracking) yang stabil, presisi, dan natural.
/// Menyelaraskan rotasi dasar kepala dengan tulang leher/tubuh avatar (headBone.parent),
/// sehingga wajah selalu menghadap ke depan dan tatapan presisi 100%.
/// </summary>
[DisallowMultipleComponent]
public class AvatarLookAtController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Target yang ditatap. Jika kosong, otomatis mencari Camera.main.")]
    [SerializeField] private Transform targetOverride;

    [Header("Bone References")]
    [Tooltip("Transform tulang kepala avatar (misal: J_Bip_C_Head).")]
    [SerializeField] private Transform headBone;

    [Header("Angle Limits & Tuning")]
    [Tooltip("Batas sudut putar horizontal maksimal (derajat).")]
    [Range(10f, 85f)] [SerializeField] private float maxYawAngle = 55f;
    [Tooltip("Batas sudut tengadah/tunduk vertikal maksimal (derajat).")]
    [Range(10f, 60f)] [SerializeField] private float maxPitchAngle = 35f;
    [Tooltip("Kecepatan lerp interpolasi tatapan.")]
    [SerializeField] private float lookSpeed = 6.0f;
    [Range(0f, 1f)] [SerializeField] private float overallWeight = 1.0f;

    private Transform _target;
    private float _currentWeight = 0f;
    private bool _lookAtEnabled = true;

    private Quaternion _headRestLocalRot = Quaternion.identity;
    private bool _hasRestPose = false;

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
                _headRestLocalRot = headBone.localRotation;
                _hasRestPose = true;
            }
        }
    }

    private void Awake()
    {
        ResolveTarget();
        if (headBone != null && !_hasRestPose)
        {
            _headRestLocalRot = headBone.localRotation;
            _hasRestPose = true;
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

        // Fade in bobot tatapan saat aktif, fade out saat nonaktif
        float targetWeight = (_lookAtEnabled && _target != null) ? overallWeight : 0f;
        _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * lookSpeed);
    }

    private void LateUpdate()
    {
        if (headBone == null || _target == null) return;

        if (!_hasRestPose)
        {
            _headRestLocalRot = headBone.localRotation;
            _hasRestPose = true;
        }

        ApplyLookAt();
    }

    private void ApplyLookAt()
    {
        // 1. Vektor arah dari kepala ke kamera di World Space
        Vector3 headPos = headBone.position;
        Vector3 targetPos = _target.position;
        Vector3 dirToTarget = (targetPos - headPos).normalized;

        if (dirToTarget.sqrMagnitude < 0.001f) return;

        // 2. Sumbu-sumbu orientasi tubuh avatar
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        // 3. Proyeksi arah target ke sistem koordinat avatar
        float x = Vector3.Dot(dirToTarget, right);   // Jarak horizontal (+ kanan, - kiri)
        float y = Vector3.Dot(dirToTarget, up);      // Jarak vertikal (+ atas, - bawah)
        float z = Vector3.Dot(dirToTarget, forward); // Jarak ke depan

        // Jika target berada di belakang avatar (z <= 0.05), jangan paksa memutar leher
        if (z <= 0.05f)
        {
            _currentWeight = Mathf.MoveTowards(_currentWeight, 0f, Time.deltaTime * lookSpeed * 2f);
        }

        // 4. Hitung sudut yaw dan pitch dalam derajat
        float rawYaw = Mathf.Atan2(x, Mathf.Max(0.01f, z)) * Mathf.Rad2Deg;
        float rawPitch = -Mathf.Atan2(y, Mathf.Sqrt(x * x + z * z)) * Mathf.Rad2Deg;

        // 5. Batasi sudut sesuai limit anatomi dan kalikan dengan bobot aktif
        float clampedYaw = Mathf.Clamp(rawYaw, -maxYawAngle, maxYawAngle) * _currentWeight;
        float clampedPitch = Mathf.Clamp(rawPitch, -maxPitchAngle, maxPitchAngle) * _currentWeight;

        // 6. Buat rotasi offset di World Space: Yaw di sekitar sumbu Up, Pitch di sekitar sumbu Right
        Quaternion yawRot = Quaternion.AngleAxis(clampedYaw, up);
        Quaternion pitchRot = Quaternion.AngleAxis(clampedPitch, right);
        Quaternion lookOffset = yawRot * pitchRot;

        // 7. Base rest pose yang selalu sinkron dengan leher & tubuh avatar
        Quaternion baseHeadWorldRot = headBone.parent != null 
            ? (headBone.parent.rotation * _headRestLocalRot) 
            : (transform.rotation * _headRestLocalRot);

        // 8. Rotasi akhir absolut untuk frame ini (Bebas dari efek terbalik / terputar 180)
        Quaternion targetHeadWorldRot = lookOffset * baseHeadWorldRot;

        // Interpolasikan rotasi kepala secara halus
        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetHeadWorldRot, Time.deltaTime * lookSpeed);
    }
}
