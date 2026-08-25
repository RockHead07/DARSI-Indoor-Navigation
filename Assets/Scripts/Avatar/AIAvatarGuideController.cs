using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pemandu Lead-Follow (ADR-034). Avatar berjalan MEMIMPIN pengguna di depan, menyusuri
/// rute yang sudah dihitung MultiSet SDK. Avatar TIDAK punya NavMeshAgent dan tidak pernah
/// melakukan pathfinding sendiri (keputusan 2) — kalau ia menghitung rutenya sendiri, ia bisa
/// menempuh jalur yang berbeda dari garis yang dilihat pengguna di lantai.
///
/// Sumber rute: LineRenderer milik ShowPath. Ini sengaja BUKAN field privat ShowPath.path
/// (yang butuh refleksi) dan BUKAN navController.agent.path (yang terbukti kosong,
/// hasPath=false / 1 corner). LineRenderer adalah secara harfiah garis yang dilihat pengguna,
/// jadi memakainya menjamin avatar dan garis tidak pernah berbeda pendapat.
///
/// Rute hanya berlaku DALAM SATU LANTAI (ADR-020 amandemen 020-B). Transisi lantai adalah
/// serah terima lift milik FloorTransitionController, bukan urusan controller ini
/// (ADR-034 keputusan 9).
///
/// GATE LOKALISASI: controller ini tidak menyalakan dirinya sendiri. StartLeading() wajib
/// dipanggil oleh pihak yang sudah memastikan MultiSet localize berhasil (ADR-034 keputusan 5,
/// ADR-007). Gate sengaja ditaruh di pemanggil, bukan sebagai flag di sini, supaya tidak ada
/// saklar "matikan pengaman" yang bisa ikut ter-build tanpa sengaja.
/// </summary>
[DisallowMultipleComponent]
public class AIAvatarGuideController : MonoBehaviour
{
    public enum GuideState { IdleStand, LeadingPath, WaitingForUser, ArrivalPointing }

    [Header("Sumber Rute")]
    [Tooltip("Kosongkan untuk mencari ShowPath otomatis di scene.")]
    [SerializeField] private ShowPath showPath;
    [Tooltip("Dipakai untuk memastikan rute yang dibaca sungguhan, bukan sisa garis lama.")]
    [SerializeField] private NavigationController navigation;

    [Header("Jarak (meter)")]
    [Tooltip("Seberapa jauh avatar berjalan di DEPAN pengguna sepanjang rute.")]
    [SerializeField] private float leadDistance = 2.0f;
    [Tooltip("Berapa lama pengguna boleh tidak maju sebelum avatar berhenti dan menoleh (detik).")]
    [SerializeField] private float stallSecondsBeforeWaiting = 1.5f;
    [Tooltip("Kemajuan sekecil ini masih dianggap diam. Meredam derau proyeksi rute.")]
    [SerializeField] private float advanceEpsilon = 0.05f;
    [Tooltip("Sisa rute sependek ini dianggap sudah tiba.")]
    [SerializeField] private float arrivalThreshold = 1.5f;

    [Header("Gerak")]
    [Tooltip("Kecepatan jalan santai saat avatar sudah berada di posisinya.")]
    [SerializeField] private float moveSpeed = 1.4f;
    [Tooltip("Tambahan kecepatan per meter ketinggalan dari titik bidik. Tanpa ini avatar " +
             "tidak akan pernah menyusul pengguna yang berjalan lebih cepat dari moveSpeed.")]
    [SerializeField] private float catchUpGain = 1.0f;
    [Tooltip("Batas atas kecepatan mengejar, supaya avatar tidak terlihat meluncur.")]
    [SerializeField] private float maxSpeed = 3.0f;
    [SerializeField] private float turnSpeed = 6.0f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private Transform _user;
    private LineRenderer _line;
    private Vector3[] _buffer = new Vector3[0];
    private readonly List<Vector3> _points = new List<Vector3>();
    private GuideState _state = GuideState.IdleStand;
    private bool _leading;
    private int _speedHash;
    private float _lastUserS;   // kemajuan pengguna terakhir di sepanjang rute
    private float _stalledFor;  // sudah berapa lama pengguna tidak maju

    public GuideState CurrentState => _state;
    public bool IsLeading => _leading;

    private void Awake()
    {
        if (showPath == null) showPath = FindFirstObjectByType<ShowPath>();
        if (navigation == null) navigation = FindFirstObjectByType<NavigationController>();
        if (showPath != null) _line = showPath.GetComponent<LineRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);
    }

    /// <summary>Panggil HANYA setelah localize berhasil. Lihat catatan gate di atas.</summary>
    public void StartLeading()
    {
        _leading = true;
        _lastUserS = 0f;    // reset, kalau tidak sisa sesi lama bikin avatar langsung mengira pengguna mandek
        _stalledFor = 0f;
        SetState(GuideState.LeadingPath);
    }

    public void StopLeading() { _leading = false; SetState(GuideState.IdleStand); Drive(0f); }

    private void Update()
    {
        if (_user == null && Camera.main != null) _user = Camera.main.transform;
        if (!_leading || _user == null) { Drive(0f); return; }

        // LineRenderer menyimpan garis TERAKHIR yang digambar, termasuk sisa dari sesi
        // sebelumnya, sampai ShowPath sempat menghitung ulang (pathUpdateFrequency 0.5s).
        // Tanpa gerbang ini, frame-frame pertama membaca garis basi sepanjang ~1 m dan
        // langsung memicu ArrivalPointing padahal rutenya masih puluhan meter.
        if (navigation != null && !navigation.IsCurrentlyNavigating())
        {
            SetState(GuideState.IdleStand);
            Drive(0f);
            return;
        }

        if (!ReadRoute()) { SetState(GuideState.IdleStand); Drive(0f); return; }

        float pathLength = PathPolyline.Length(_points);
        float userS = PathPolyline.Project(_points, _user.position);

        // Tiba: sisa rute di depan pengguna sudah pendek.
        if (pathLength - userS <= arrivalThreshold)
        {
            SetState(GuideState.ArrivalPointing);
            FaceTowards(_user.position);
            Drive(0f);
            return;
        }

        // Pengguna berhenti berjalan: tunggu dan menoleh, jangan biarkan dia bicara ke punggung.
        //
        // Pemicunya adalah KEMAJUAN pengguna di sepanjang rute, bukan jarak ke avatar.
        // Ambang jarak (lagDistanceThreshold) diwarisi dari draft yang mengasumsikan avatar
        // punya NavMeshAgent sendiri sehingga bisa melesat jauh mendahului. Di desain ini
        // posisi avatar diikat ke userS + leadDistance, jadi jaraknya TIDAK PERNAH melebihi
        // leadDistance dan ambang jarak berapa pun di atas itu mustahil tercapai.
        if (userS > _lastUserS + advanceEpsilon)
        {
            _lastUserS = userS;
            _stalledFor = 0f;
        }
        else
        {
            _stalledFor += Time.deltaTime;
        }

        if (_stalledFor >= stallSecondsBeforeWaiting)
        {
            SetState(GuideState.WaitingForUser);
            FaceTowards(_user.position);
            Drive(0f);
            return;
        }

        // Memimpin: bidik titik pada rute sejauh leadDistance di depan posisi pengguna.
        // State di-set eksplisit di sini. Tanpa ini, sekali masuk ArrivalPointing atau
        // IdleStand, labelnya menyangkut di situ walaupun perilakunya sudah kembali memimpin.
        SetState(GuideState.LeadingPath);

        Vector3 target = PathPolyline.PointAt(_points, Mathf.Min(userS + leadDistance, pathLength));
        Vector3 before = transform.position;

        // Kecepatan naik sebanding jarak ke titik bidik. Dengan kecepatan tetap, pengguna
        // yang berjalan lebih cepat dari moveSpeed tidak akan pernah tersusul, dan avatar
        // berubah dari pemandu menjadi pengekor.
        float gap = Vector3.Distance(before, target);
        float speed = Mathf.Min(moveSpeed + gap * catchUpGain, maxSpeed);
        transform.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);

        Vector3 moved = transform.position - before;
        if (moved.sqrMagnitude > 1e-8f) FaceTowards(transform.position + moved);
        Drive(moved.magnitude / Mathf.Max(Time.deltaTime, 1e-5f));
    }

    /// <summary>Baca polyline dari LineRenderer dan koreksi offset tinggi garis.</summary>
    private bool ReadRoute()
    {
        if (_line == null || !_line.useWorldSpace || _line.positionCount < 2) return false;

        if (_buffer.Length != _line.positionCount) _buffer = new Vector3[_line.positionCount];
        _line.GetPositions(_buffer);

        // ShowPath menggambar garis sedikit DI ATAS lantai agar tidak z-fighting.
        // Avatar harus berdiri di lantainya, bukan melayang di atas garis.
        float lift = showPath != null ? showPath.pathHeightAboveGround : 0f;

        _points.Clear();
        for (int i = 0; i < _buffer.Length; i++)
        {
            var p = _buffer[i];
            p.y -= lift;
            _points.Add(p);
        }
        return PathPolyline.Length(_points) > 0.01f;
    }

    private void FaceTowards(Vector3 worldPoint)
    {
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        // Peredaman bebas-framerate: lerp mentah dengan deltaTime membuat kecepatan
        // menoleh ikut berubah saat FPS naik-turun.
        float t = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), t);
    }

    private void Drive(float speed)
    {
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetFloat(_speedHash, speed, 0.1f, Time.deltaTime);
    }

    private void SetState(GuideState s)
    {
        if (_state == s) return;
        _state = s;
        Debug.Log($"[AvatarGuide] state -> {s}");
    }
}
