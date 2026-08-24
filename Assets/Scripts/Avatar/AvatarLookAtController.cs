using UnityEngine;

/// <summary>
/// Mengendalikan tatapan kepala dan mata Avatar 3D agar menatap ke Camera.main secara dinamis (look-at tracking).
/// Mendukung Animator IK (Humanoid standard) dan fallback Procedural Bone Rotation dengan batasan sudut (clamping)
/// serta interpolasi halus (damping) agar pergerakan leher terlihat natural.
/// </summary>
[DisallowMultipleComponent]
public class AvatarLookAtController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Target yang ditatap. Jika kosong, otomatis mencari Camera.main.")]
    [SerializeField] private Transform targetOverride;

    [Header("IK Weights")]
    [Range(0f, 1f)] [SerializeField] private float overallWeight = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float bodyWeight = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float headWeight = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float eyesWeight = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float clampWeight = 0.5f;

    [Header("Procedural Fallback (Non-IK)")]
    [Tooltip("Transform tulang leher/kepala (opsional, dipakai jika animator IK tidak aktif).")]
    [SerializeField] private Transform headBone;
    [Tooltip("Sudut rotasi horizontal maksimal (derajat). Mencegah leher berputar terlalu jauh.")]
    [SerializeField] private float maxYawAngle = 70f;
    [Tooltip("Sudut rotasi vertikal maksimal (derajat).")]
    [SerializeField] private float maxPitchAngle = 40f;
    [Tooltip("Kecepatan lerp penghalusan tatapan.")]
    [SerializeField] private float lookSpeed = 5.0f;

    private Animator _animator;
    private Transform _target;
    private float _currentWeight = 0f;
    private bool _lookAtEnabled = true;

    public bool IsLookAtEnabled
    {
        get => _lookAtEnabled;
        set => _lookAtEnabled = value;
    }

    public Transform HeadBone
    {
        get => headBone;
        set => headBone = value;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        ResolveTarget();
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

        // Smooth fade-in/fade-out untuk bobot look-at
        float targetWeight = (_lookAtEnabled && _target != null) ? overallWeight : 0f;
        _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * lookSpeed);

        // Fallback procedural bone rotation jika Animator IK tidak digunakan
        if ((_animator == null || !_animator.isHuman) && headBone != null && _currentWeight > 0.01f)
        {
            ApplyProceduralBoneLookAt();
        }
    }

    /// <summary>
    /// Digunakan oleh Mecanim jika avatar di-rig sebagai Humanoid.
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || _target == null || _currentWeight <= 0.001f)
            return;

        Vector3 targetPos = _target.position;

        // Validasi jarak agar tidak melihat ke belakang secara ekstrem
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, dirToTarget);

        // Jika target berada di belakang avatar, kurangi bobot look-at secara otomatis
        float effectiveWeight = dot > 0 ? _currentWeight : Mathf.Max(0f, _currentWeight * (dot + 1f));

        _animator.SetLookAtWeight(effectiveWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        _animator.SetLookAtPosition(targetPos);
    }

    private void ApplyProceduralBoneLookAt()
    {
        if (_target == null || headBone == null) return;

        Vector3 dirToTarget = _target.position - headBone.position;
        if (dirToTarget.sqrMagnitude < 0.001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(dirToTarget, Vector3.up);
        
        // Batasi sudut relatif terhadap orientasi avatar
        Quaternion localRot = Quaternion.Inverse(transform.rotation) * lookRotation;
        Vector3 euler = localRot.eulerAngles;

        float yaw = NormalizeAngle(euler.y);
        float pitch = NormalizeAngle(euler.x);

        yaw = Mathf.Clamp(yaw, -maxYawAngle, maxYawAngle);
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

        Quaternion clampedLocalRot = Quaternion.Euler(pitch, yaw, 0f);
        Quaternion targetWorldRot = transform.rotation * clampedLocalRot;

        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetWorldRot, Time.deltaTime * lookSpeed * _currentWeight);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
