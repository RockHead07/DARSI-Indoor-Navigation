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
    [SerializeField] private float stallSecondsBeforeWaiting = 0.8f;
    [Tooltip("Pergerakan sekecil ini masih dianggap diam. Meredam derau tracking kamera AR.")]
    [SerializeField] private float advanceEpsilon = 0.05f;
    [Tooltip("Sisa rute sependek ini dianggap sudah tiba.")]
    [SerializeField] private float arrivalThreshold = 1.5f;

    [Header("Gerak")]
    [Tooltip("Kecepatan jalan santai saat avatar sudah berada di posisinya.")]
    [SerializeField] private float moveSpeed = 1.4f;
    [Tooltip("Tambahan kecepatan per meter ketinggalan dari titik bidik. Tanpa ini avatar " +
             "tidak akan pernah menyusul pengguna yang berjalan lebih cepat dari moveSpeed.")]
    [SerializeField] private float catchUpGain = 0.8f;
    [Tooltip("Batas atas kecepatan mengejar. Dipatok mendekati kecepatan klip jalan " +
             "(BlendTree Walk @1.4) supaya kaki tidak selip: di atas itu klip tetap diputar " +
             "kecepatan normal sementara badan melesat, dan avatar terlihat mengesot.")]
    [SerializeField] private float maxSpeed = 2.0f;
    [Tooltip("Selisih ke titik bidik di bawah ini diabaikan. Meredam koreksi mikro yang " +
             "membuat avatar terlihat gelisah saat pengguna bergerak sedikit saja.")]
    [SerializeField] private float repositionDeadzone = 0.35f;
    [SerializeField] private float turnSpeed = 6.0f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [Tooltip("Kecepatan yang DIPROGRAM di klip Walk (m/s), diukur dari clip.averageSpeed. " +
             "Harus sama dengan threshold Walk di BlendTree. Dipakai untuk menyelaraskan " +
             "kecepatan putar klip dengan kecepatan gerak sesungguhnya supaya kaki tidak selip.")]
    [SerializeField] private float walkClipSpeed = 1.589f;
    [Tooltip("Trigger sapaan saat mulai memimpin.")]
    [SerializeField] private string waveParam = "Wave";
    [Tooltip("Trigger menunjuk saat tiba di tujuan.")]
    [SerializeField] private string pointParam = "Point";
    [Tooltip("Arah menunjuk: -1 kiri, 0 depan, +1 kanan.")]
    [SerializeField] private string pointDirParam = "PointDir";

    private Transform _user;
    private LineRenderer _line;
    private Vector3[] _buffer = new Vector3[0];
    private readonly List<Vector3> _points = new List<Vector3>();
    private GuideState _state = GuideState.IdleStand;
    private bool _leading;
    private int _speedHash, _waveHash, _pointHash, _pointDirHash;
    private Vector3 _lastUserPos;  // posisi pengguna terakhir (datar), acuan deteksi berhenti
    private float _stalledFor;  // sudah berapa lama pengguna tidak maju
    private bool _needsSnap;    // pindahkan ke posisi memimpin di frame pertama, jangan menyalip

    public GuideState CurrentState => _state;
    public bool IsLeading => _leading;

    private void Awake()
    {
        if (showPath == null) showPath = FindFirstObjectByType<ShowPath>();
        if (navigation == null) navigation = FindFirstObjectByType<NavigationController>();
        if (showPath != null) _line = showPath.GetComponent<LineRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);
        _waveHash = Animator.StringToHash(waveParam);
        _pointHash = Animator.StringToHash(pointParam);
        _pointDirHash = Animator.StringToHash(pointDirParam);
    }

    /// <summary>Panggil HANYA setelah localize berhasil. Lihat catatan gate di atas.</summary>
    public void StartLeading()
    {
        _leading = true;
        var u = _user != null ? _user.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        u.y = 0f;
        _lastUserPos = u;   // reset, kalau tidak sisa sesi lama bikin avatar langsung mengira pengguna mandek
        _stalledFor = 0f;
        _needsSnap = true;
        SetState(GuideState.LeadingPath);
        // Menyapa dulu sebelum jalan, seperti pemandu sungguhan.
        if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(_waveHash);
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
            // Arah menunjuk diturunkan dari ujung rute relatif arah hadap avatar, bukan
            // dipilih manual: -1 kiri, 0 depan, +1 kanan. Memakai ketiga klip Point.
            if (animator != null && animator.isActiveAndEnabled && _points.Count >= 2)
            {
                Vector3 toEnd = _points[_points.Count - 1] - transform.position;
                toEnd.y = 0f;
                if (toEnd.sqrMagnitude > 1e-4f)
                {
                    float signed = Vector3.SignedAngle(transform.forward, toEnd.normalized, Vector3.up);
                    animator.SetFloat(_pointDirHash, Mathf.Clamp(signed / 60f, -1f, 1f));
                }
            }
            SetState(GuideState.ArrivalPointing);
            FaceTowards(_user.position);
            Drive(0f);
            return;
        }

        // Pengguna berhenti berjalan: tunggu dan menoleh, jangan biarkan dia bicara ke punggung.
        //
        // Pemicunya PERGERAKAN POSISI PENGGUNA DI DUNIA. Dua besaran lain sudah dicoba dan
        // dua-duanya salah:
        //
        // 1. Jarak avatar ke pengguna (lagDistanceThreshold, warisan draft Task 6). Draft itu
        //    mengasumsikan avatar punya NavMeshAgent sendiri sehingga bisa melesat jauh
        //    mendahului. Di desain ini posisi avatar diikat ke userS + leadDistance, jadi
        //    jaraknya TIDAK PERNAH melebihi leadDistance dan ambang di atas itu mustahil
        //    tercapai. Kode mati.
        //
        // 2. Kemajuan pengguna di sepanjang rute (userS): ShowPath selalu menghitung ulang rute
        //    dari posisi pengguna (tiap pathUpdateFrequency), jadi userS bergerak seperti gigi
        //    gergaji dengan puncak terbatas oleh jarak yang ditempuh dalam satu interval
        //    (~0,55 m pada 1,1 m/s). Terukur di Play mode: userS berkisar 0,03-0,24 sementara
        //    nilai puncak tercatat beku di 0,306. Begitu puncak tercapai userS tidak pernah
        //    melampauinya lagi, dan penghitung mandek menumpuk terus WALAUPUN pengguna
        //    sedang berjalan.
        Vector3 userFlat = _user.position; userFlat.y = 0f;
        if ((userFlat - _lastUserPos).sqrMagnitude > advanceEpsilon * advanceEpsilon)
        {
            _lastUserPos = userFlat;
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

        // Frame pertama setelah StartLeading: PINDAHKAN langsung ke posisi memimpin.
        // Tanpa ini avatar mulai dari posisi pengguna dengan gap = leadDistance penuh,
        // sehingga rumus kejar langsung mentok maxSpeed dan ia terlihat MENYALIP dari kaki
        // pengguna dengan kecepatan lari. Pemandu memang seharusnya sudah di depan saat mulai.
        if (_needsSnap)
        {
            _needsSnap = false;
            transform.position = target;
            Vector3 ahead = PathPolyline.PointAt(_points, Mathf.Min(userS + leadDistance + 0.5f, pathLength));
            if ((ahead - target).sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(new Vector3(ahead.x - target.x, 0f, ahead.z - target.z));
            Drive(0f);
            return;
        }

        Vector3 before = transform.position;

        // Kecepatan naik sebanding jarak ke titik bidik. Dengan kecepatan tetap, pengguna
        // yang berjalan lebih cepat dari moveSpeed tidak akan pernah tersusul, dan avatar
        // berubah dari pemandu menjadi pengekor.
        float gap = Vector3.Distance(before, target);

        // Zona mati: jangan mengoreksi selisih sekecil ini. Titik bidik dihitung ulang tiap
        // frame dari userS, dan userS bergetar karena ShowPath menghitung ulang rute dari
        // posisi pengguna. Tanpa zona mati avatar ikut bergeser untuk gerakan sekecil apa pun
        // dan terlihat gelisah, bukan seperti orang berjalan. Dilaporkan dari uji manual.
        if (gap < repositionDeadzone)
        {
            Drive(0f);
            return;
        }

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
        if (animator == null || !animator.isActiveAndEnabled) return;
        animator.SetFloat(_speedHash, speed, 0.1f, Time.deltaTime);

        // Selaraskan kecepatan PUTAR klip dengan kecepatan gerak sesungguhnya.
        //
        // Klip Walk dari Mixamo membawa root motion maju 1,589 m/s (terukur lewat
        // clip.averageSpeed; klip lain semuanya 0 karena "in place"). Avatar digerakkan
        // AIAvatarGuideController pada kecepatan berbeda, dan selisihnya membuat telapak
        // kaki menggeser di lantai. Tanpa penyelarasan ini, selisih 1,4 vs 1,589 saja
        // sudah terasa sebagai langkah yang "tidak pas".
        //
        // Klip diam (Idle, Wave, Point) averageSpeed-nya 0, jadi saat tidak berjalan
        // kecepatan putar dikembalikan normal supaya sapaan dan tunjukan tidak ikut melambat.
        float ratio = (speed > 0.05f && walkClipSpeed > 0.01f) ? speed / walkClipSpeed : 1f;
        animator.speed = Mathf.Clamp(ratio, 0.4f, 1.8f);
    }

    private void SetState(GuideState s)
    {
        if (_state == s) return;
        _state = s;
        // Dipicu saat MASUK state saja. Kalau dipanggil tiap frame, klipnya restart terus
        // dan tangannya tidak pernah selesai mengangkat.
        if (s == GuideState.ArrivalPointing && animator != null && animator.isActiveAndEnabled)
            animator.SetTrigger(_pointHash);
        Debug.Log($"[AvatarGuide] state -> {s}");
    }
}
