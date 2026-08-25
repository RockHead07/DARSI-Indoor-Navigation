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
    [Tooltip("Bobot tatapan saat pengguna berada di BELAKANG avatar (kondisi normal lead-follow). " +
             "Bukan 0, supaya kepala tetap melirik lewat bahu; bukan 1, supaya leher tidak " +
             "terlihat terkunci maksimal sepanjang perjalanan.")]
    [Range(0f, 1f)] [SerializeField] private float behindWeight = 0.6f;

    private Transform _target;
    private float _currentWeight = 0f;
    private bool _lookAtEnabled = true;

    private float _yaw;    // sudut tatapan yang diredam, disimpan antar frame
    private float _pitch;  // (tulang tidak bisa dipakai menyimpan: Animator menimpanya tiap frame)
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

        // Target di belakang avatar. DULU look-at dimatikan total di sini, dan itu keliru untuk
        // pola lead-follow (ADR-034): pemandu berjalan di DEPAN, jadi penggunanya hampir selalu
        // berada di belakang. Akibatnya kepala tidak pernah menoleh sepanjang perjalanan dan
        // avatar terasa mengabaikan penggunanya. Dilaporkan langsung dari uji manual.
        //
        // Sekarang look-at TETAP aktif dan dibiarkan dibatasi maxYawAngle di bawah, sehingga
        // menghasilkan lirikan lewat bahu sejauh anatomi mengizinkan, bukan kepala yang
        // menghadap lurus ke depan seolah tidak peduli. Bobotnya diturunkan sebagian saja
        // supaya tidak terlihat seperti leher terkunci maksimal terus-menerus.
        if (z <= 0.05f)
        {
            _currentWeight = Mathf.MoveTowards(_currentWeight, overallWeight * behindWeight,
                                               Time.deltaTime * lookSpeed);
        }

        // 4. Hitung sudut yaw dan pitch dalam derajat
        float rawYaw = Mathf.Atan2(x, Mathf.Max(0.01f, z)) * Mathf.Rad2Deg;
        float rawPitch = -Mathf.Atan2(y, Mathf.Sqrt(x * x + z * z)) * Mathf.Rad2Deg;

        // 5. Batasi sudut sesuai limit anatomi dan kalikan dengan bobot aktif
        float clampedYaw = Mathf.Clamp(rawYaw, -maxYawAngle, maxYawAngle) * _currentWeight;
        float clampedPitch = Mathf.Clamp(rawPitch, -maxPitchAngle, maxPitchAngle) * _currentWeight;

        // 5b. Peredaman disimpan pada SUDUTNYA, bukan pada rotasi tulang.
        //
        // Dulu barisnya: headBone.rotation = Slerp(headBone.rotation, target, deltaTime * lookSpeed).
        // Itu keliru karena mengandaikan hasil slerp menumpuk antar frame, padahal Animator
        // MENULIS ULANG tulang kepala ke pose animasi setiap frame sebelum LateUpdate. Jadi
        // slerp selalu mulai dari nol lagi dan kepala mentok di satu langkah peredaman saja.
        // Terukur: butuh yaw 55 derajat (setelah clamp), yang terjadi cuma 6,5 derajat --
        // persis sekitar 10% (deltaTime*lookSpeed = 0,0167*6 = 0,1). Gejalanya: avatar
        // terlihat "tidak benar-benar menengok".
        //
        // Sekarang sudutnya sendiri yang diredam dan disimpan antar frame, lalu rotasi ditulis
        // ABSOLUT. Hasil akhirnya tidak lagi bergantung pada nilai tulang di frame sebelumnya.
        float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);   // bebas framerate
        _yaw = Mathf.Lerp(_yaw, clampedYaw, k);
        _pitch = Mathf.Lerp(_pitch, clampedPitch, k);

        // 6. Buat rotasi offset di World Space: Yaw di sekitar sumbu Up, Pitch di sekitar sumbu Right
        Quaternion yawRot = Quaternion.AngleAxis(_yaw, up);
        Quaternion pitchRot = Quaternion.AngleAxis(_pitch, right);
        Quaternion lookOffset = yawRot * pitchRot;

        // 7. Base rest pose yang selalu sinkron dengan leher & tubuh avatar
        Quaternion baseHeadWorldRot = headBone.parent != null 
            ? (headBone.parent.rotation * _headRestLocalRot) 
            : (transform.rotation * _headRestLocalRot);

        // 8. Rotasi akhir absolut untuk frame ini (Bebas dari efek terbalik / terputar 180).
        // Ditulis ABSOLUT, bukan di-slerp dari nilai tulang saat ini. Lihat catatan di 5b:
        // nilai tulang sudah ditimpa Animator, jadi memakainya sebagai titik awal peredaman
        // membuat kepala tidak pernah sampai ke sudut yang diminta.
        headBone.rotation = lookOffset * baseHeadWorldRot;
    }
}
