using UnityEngine;

/// <summary>
/// Mengendalikan tatapan kepala dan leher Avatar 3D agar menatap ke Camera.main secara 100% akurat.
/// Menggunakan World-Space Quaternion.FromToRotation yang independen dari orientasi lokal rig tulang,
/// sehingga tidak akan pernah terbalik, miring, atau terpelintir pada model 3D/VRM apa pun.
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
    [Tooltip("Transform tulang leher (opsional, untuk mendistribusikan rotasi natural).")]
    [SerializeField] private Transform neckBone;

    [Header("Look-At Settings")]
    [Tooltip("Batas sudut maksimal tatapan kepala (derajat) terhadap arah hadap tubuh.")]
    [Range(10f, 90f)] [SerializeField] private float maxLookAngle = 65f;
    [Tooltip("Kecepatan lerp penghalusan tatapan.")]
    [SerializeField] private float lookSpeed = 8.0f;
    [Range(0f, 1f)] [SerializeField] private float overallWeight = 1.0f;

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

    public Transform NeckBone
    {
        get => neckBone;
        set => neckBone = value;
    }

    private void Awake()
    {
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

        // Penghalusan transisi bobot tatapan (fade in / out)
        float targetWeight = (_lookAtEnabled && _target != null) ? overallWeight : 0f;
        _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, Time.deltaTime * lookSpeed);
    }

    /// <summary>
    /// Eksekusi di LateUpdate() setelah seluruh kalkulasi skinning/glTF selesai.
    /// Menggunakan delta rotasi World Space dari transform.forward ke target direction.
    /// </summary>
    private void LateUpdate()
    {
        if (headBone == null || _target == null || _currentWeight < 0.001f) return;

        // Vektor dari kepala ke target kamera
        Vector3 headPos = headBone.position;
        Vector3 targetPos = _target.position;
        Vector3 dirToTarget = (targetPos - headPos).normalized;

        if (dirToTarget.sqrMagnitude < 0.001f) return;

        // Arah hadap tubuh avatar di world space
        Vector3 bodyForward = transform.forward;

        // Batasi sudut maksimal agar leher tidak berputar ke belakang
        float angle = Vector3.Angle(bodyForward, dirToTarget);
        Vector3 clampedDir = dirToTarget;
        if (angle > maxLookAngle)
        {
            clampedDir = Vector3.RotateTowards(bodyForward, dirToTarget, Mathf.Deg2Rad * maxLookAngle, 0f);
        }

        // Hitung delta rotasi world space yang dibutuhkan dari arah hadap tubuh ke arah target
        Quaternion lookDelta = Quaternion.FromToRotation(bodyForward, clampedDir);

        // Interpolasikan delta rotasi dengan bobot aktif
        Quaternion weightedDelta = Quaternion.Slerp(Quaternion.identity, lookDelta, _currentWeight);

        if (neckBone != null)
        {
            // Bagi rotasi: 35% pada leher, 65% pada kepala
            Quaternion neckDelta = Quaternion.Slerp(Quaternion.identity, weightedDelta, 0.35f);
            Quaternion headDelta = Quaternion.Slerp(Quaternion.identity, weightedDelta, 0.65f);

            neckBone.rotation = neckDelta * neckBone.rotation;
            headBone.rotation = headDelta * headBone.rotation;
        }
        else
        {
            // Terapkan seluruh delta pada kepala
            headBone.rotation = weightedDelta * headBone.rotation;
        }
    }
}
