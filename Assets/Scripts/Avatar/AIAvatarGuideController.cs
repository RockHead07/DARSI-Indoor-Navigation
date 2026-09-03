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
///
/// MODEL GERAK -- "lompat-tunggu" (2026-09-03), menggantikan model "tali kekang" lama:
/// avatar dulu diikat SELALU tepat leadDistance di depan pengguna, dihitung ulang tiap
/// frame, dan karena itu WAJIB mengejar secara proporsional (catchUpGain/maxSpeed) begitu
/// pengguna berjalan lebih cepat -- ini yang bikin avatar terlihat "tiba-tiba lari": kecepatan
/// geraknya tidak pernah konstan, selalu fungsi dari jarak yang berubah tiap frame. Model baru
/// membuang kebutuhan itu SAMA SEKALI: avatar berjalan ke SATU titik tetap (legDistance di
/// depan posisi pengguna SAAT dipilih, bukan dihitung ulang tiap frame) dengan kecepatan
/// KONSTAN (moveSpeed), lalu BERHENTI dan menunggu. Begitu pengguna cukup dekat
/// (advanceTriggerDistance), titik berikutnya dipilih dan avatar jalan lagi. Kalau pengguna
/// tidak kunjung mendekat (chaseStallSeconds), avatar balik menjemput (Chasing) dengan
/// kecepatan tetap juga (chaseSpeed) -- bukan proporsional. Tidak ada lagi rumus kecepatan
/// yang bergantung jarak di mana pun dalam file ini.
/// </summary>
[DisallowMultipleComponent]
public class AIAvatarGuideController : MonoBehaviour
{
    public enum GuideState { IdleStand, LeadingPath, WaitingForUser, ArrivalPointing, Chasing }

    [Header("Sumber Rute")]
    [Tooltip("Kosongkan untuk mencari ShowPath otomatis di scene.")]
    [SerializeField] private ShowPath showPath;
    [Tooltip("Dipakai untuk memastikan rute yang dibaca sungguhan, bukan sisa garis lama.")]
    [SerializeField] private NavigationController navigation;
    [Tooltip("Kosongkan untuk mencari otomatis. Avatar MENAMPILKAN fase milik komponen ini, " +
             "tidak punya mesin state lintas lantai sendiri (ADR-034 keputusan 9).")]
    [SerializeField] private FloorTransitionController floorTransition;
    [Tooltip("Bagian visual yang disembunyikan saat transisi lantai. Kosongkan untuk memakai " +
             "GameObject tempat Animator berada.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Jarak (meter)")]
    [Tooltip("Seberapa jauh titik berikutnya dipilih di depan posisi pengguna SAAT avatar " +
             "mulai berjalan ke sana (bukan diikat ulang tiap frame seperti model lama).")]
    [SerializeField] private float legDistance = 3.0f;
    [Tooltip("Seberapa dekat pengguna harus sampai ke titik saat ini sebelum avatar memilih " +
             "titik berikutnya dan jalan lagi.")]
    [SerializeField] private float advanceTriggerDistance = 1.5f;
    [Tooltip("Berapa lama avatar menunggu di titiknya sebelum menyerah dan balik menjemput " +
             "pengguna (Chasing) -- supaya pengguna tidak pernah benar-benar tersesat.")]
    [SerializeField] private float chaseStallSeconds = 6.0f;
    [Tooltip("Sisa rute sependek ini dianggap sudah tiba.")]
    [SerializeField] private float arrivalThreshold = 1.5f;
    [Tooltip("Jarak dari tujuan sesungguhnya ke titik SAMPING tempat avatar berdiri saat " +
             "tiba -- supaya avatar tidak nangkring persis di atas POI. Sisi tetap (kiri " +
             "relatif arah rute terakhir), sengaja tidak dibuat adaptif ke geometri ruangan " +
             "dulu -- keputusan sadar, lihat docs/superpowers/plans kalau ada laporan avatar " +
             "kejeblos dinding di lorong sempit.")]
    [SerializeField] private float sideOffsetDistance = 1.0f;
    [Tooltip("Seberapa dekat dianggap 'sudah sampai' di suatu titik (waypoint/titik samping " +
             "POI/posisi pengguna saat mengejar).")]
    [SerializeField] private float waypointArrivalEpsilon = 0.15f;

    [Header("Gerak")]
    [Tooltip("Kecepatan jalan KONSTAN avatar menuju titiknya. Tidak pernah berubah karena " +
             "jarak -- itulah intinya model ini.")]
    [SerializeField] private float moveSpeed = 1.4f;
    [Tooltip("Kecepatan KONSTAN saat menjemput pengguna (state Chasing). Sengaja tetap, " +
             "bukan proporsional ke jarak -- itu pola yang terbukti bikin gerakan tidak wajar " +
             "di model lama.")]
    [SerializeField] private float chaseSpeed = 1.8f;
    [SerializeField] private float turnSpeed = 6.0f;
    [Tooltip("Waktu meredam PERUBAHAN kecepatan (detik) tiap kali avatar mulai bergerak dari " +
             "diam -- supaya ada fase mempercepat singkat, bukan langsung penuh seketika.")]
    [SerializeField] private float speedSmoothTime = 0.4f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [Tooltip("Kecepatan yang DIPROGRAM di klip Walk (m/s), diukur dari clip.averageSpeed. " +
             "Harus sama dengan threshold Walk di BlendTree. Dipakai untuk menyelaraskan " +
             "kecepatan putar klip dengan kecepatan gerak sesungguhnya supaya kaki tidak selip.")]
    [SerializeField] private float walkClipSpeed = 1.589f;
    [Tooltip("Trigger sapaan saat mulai memimpin, DIPAKAI ULANG juga saat tiba di tujuan " +
             "(melambai menandakan 'kita sudah sampai') -- simetris, bukan dua gestur beda.")]
    [SerializeField] private string waveParam = "Wave";

    private Transform _user;
    private LineRenderer _line;
    private Vector3[] _buffer = new Vector3[0];
    private readonly List<Vector3> _points = new List<Vector3>();
    private GuideState _state = GuideState.IdleStand;
    private bool _leading;
    private int _speedHash, _waveHash;
    private bool _needsWaypoint;      // belum ada titik dipilih, pilih di frame Update() berikutnya
    private Vector3 _waypoint;        // titik yang sedang dituju/ditunggui (LeadingPath/WaitingForUser)
    private Vector3 _arrivalTarget;   // titik SAMPING POI, dihitung sekali saat tiba terdeteksi
    private bool _hasWavedAtArrival;  // supaya trigger Wave di tujuan cuma sekali, bukan tiap frame
    private float _waitingElapsed;    // sudah berapa lama menunggu di waypoint saat ini
    private float _currentSpeed;      // kecepatan REDAM yang sungguhan dipakai gerak
    private float _speedVelocity;     // state internal SmoothDamp untuk _currentSpeed

    public GuideState CurrentState => _state;
    public bool IsLeading => _leading;

    private void Awake()
    {
        if (showPath == null) showPath = FindFirstObjectByType<ShowPath>();
        if (navigation == null) navigation = FindFirstObjectByType<NavigationController>();
        if (showPath != null) _line = showPath.GetComponent<LineRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (floorTransition == null) floorTransition = FindFirstObjectByType<FloorTransitionController>();
        // visualRoot diturunkan dari Animator, BUKAN diasumsikan anak pertama. Bug P0-2 dulu
        // terjadi karena model VRM di-parent di luar visualRoot sehingga Dismiss tidak
        // menyembunyikan apa pun.
        if (visualRoot == null && animator != null) visualRoot = animator.gameObject;
        _speedHash = Animator.StringToHash(speedParam);
        _waveHash = Animator.StringToHash(waveParam);
    }

    /// <summary>Panggil HANYA setelah localize berhasil. Lihat catatan gate di atas.
    ///
    /// DUA pemanggil sah, bukan satu: AvatarAudioClient (alur suara, ADR-034 -- avatar
    /// bicara dulu baru memimpin) memanggil ini LANGSUNG saat ucapan selesai; AvatarGuide-
    /// NavigationBridge memanggilnya REAKTIF begitu navigation.IsCurrentlyNavigating()
    /// berubah jadi true (jalur navigasi non-suara). Pada alur suara keduanya bisa
    /// terpanggil untuk sesi memimpin yang SAMA -- karena itu method ini WAJIB idempoten,
    /// no-op kalau sudah _leading, supaya animasi Wave tidak terpicu dua kali dan kecepatan
    /// tidak ikut ter-reset di tengah gerakan (ditemukan lewat audit independen).</summary>
    public void StartLeading()
    {
        if (_leading) return;
        _leading = true;
        _needsWaypoint = true;
        _hasWavedAtArrival = false;
        _waitingElapsed = 0f;
        _currentSpeed = 0f;   // mulai dari diam tiap sesi memimpin baru, bukan menyambung kecepatan lama
        _speedVelocity = 0f;
        SetState(GuideState.LeadingPath);
        // Menyapa dulu sebelum jalan, seperti pemandu sungguhan.
        if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(_waveHash);
    }

    public void StopLeading()
    {
        _leading = false;
        SetState(GuideState.IdleStand);
        Drive(0f);
        // Wajib: kalau navigasi berhenti selagi avatar disembunyikan karena transisi lantai,
        // tanpa ini dia tersangkut tak terlihat sampai sesi memimpin berikutnya.
        SetVisible(true);
    }

    private void Update()
    {
        if (_user == null && Camera.main != null) _user = Camera.main.transform;
        if (!_leading || _user == null) { Drive(0f); return; }

        // SERAH TERIMA LINTAS LANTAI (ADR-034 keputusan 9, ADR-020 poin 4).
        //
        // Selama AwaitingRelocalize pengguna sedang berpindah lantai: tracking AR putus dan
        // posisinya TIDAK SAH (ADR-007). Avatar disembunyikan, bukan sekadar dihentikan --
        // pemandu yang mengambang di tempat lama sementara penggunanya sudah di lantai lain
        // lebih menyesatkan daripada tidak ada pemandu sama sekali.
        if (floorTransition != null &&
            floorTransition.CurrentPhase == FloorTransitionController.Phase.AwaitingRelocalize)
        {
            SetVisible(false);
            SetState(GuideState.IdleStand);
            Drive(0f);
            // Wajib pilih titik baru saat muncul lagi: rute lantai baru sama sekali berbeda.
            _needsWaypoint = true;
            return;
        }
        SetVisible(true);

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

        // Tiba: sisa rute di depan pengguna sudah pendek. Jalan ke titik SAMPING POI (bukan
        // pusatnya, ADR permintaan pemilik project 2026-09-03), lalu melambai begitu benar-
        // benar sampai di titik itu -- bukan seketika saat kondisi "dekat tujuan" terdeteksi.
        if (pathLength - userS <= arrivalThreshold)
        {
            if (_state != GuideState.ArrivalPointing)
            {
                _arrivalTarget = ComputeArrivalSidePoint();
                _hasWavedAtArrival = false;
                SetState(GuideState.ArrivalPointing);
            }

            bool arrivedAtSide = MoveToward(_arrivalTarget, moveSpeed);
            if (arrivedAtSide)
            {
                FaceTowards(_user.position);
                Drive(0f);
                if (!_hasWavedAtArrival)
                {
                    _hasWavedAtArrival = true;
                    if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(_waveHash);
                }
            }
            return;
        }

        switch (_state)
        {
            case GuideState.WaitingForUser:
            {
                FaceTowards(_user.position);
                Drive(0f);

                Vector3 userFlat = _user.position; userFlat.y = 0f;
                Vector3 waypointFlat = _waypoint; waypointFlat.y = 0f;
                if (Vector3.Distance(userFlat, waypointFlat) <= advanceTriggerDistance)
                {
                    _waypoint = NextWaypoint(userS, pathLength);
                    _waitingElapsed = 0f;
                    SetState(GuideState.LeadingPath);
                    break;
                }

                _waitingElapsed += Time.deltaTime;
                if (_waitingElapsed >= chaseStallSeconds)
                {
                    SetState(GuideState.Chasing);
                }
                break;
            }

            case GuideState.Chasing:
            {
                Vector3 userTarget = _user.position;
                userTarget.y = transform.position.y;
                bool caughtUp = MoveToward(userTarget, chaseSpeed);
                if (caughtUp)
                {
                    _waypoint = NextWaypoint(userS, pathLength);
                    _waitingElapsed = 0f;
                    SetState(GuideState.LeadingPath);
                }
                break;
            }

            default: // IdleStand atau LeadingPath -- termasuk frame pertama setelah StartLeading
            {
                if (_needsWaypoint)
                {
                    _needsWaypoint = false;
                    _waypoint = NextWaypoint(userS, pathLength);
                }

                SetState(GuideState.LeadingPath);
                bool reachedWaypoint = MoveToward(_waypoint, moveSpeed);
                if (reachedWaypoint)
                {
                    _waitingElapsed = 0f;
                    SetState(GuideState.WaitingForUser);
                }
                break;
            }
        }
    }

    /// <summary>Titik legDistance di depan posisi pengguna SAAT INI di sepanjang rute.
    /// Dipanggil hanya saat avatar SELESAI di titik lama (bukan tiap frame) -- itulah yang
    /// membedakan model ini dari model lama yang mengikat ulang tiap frame.</summary>
    private Vector3 NextWaypoint(float userS, float pathLength)
    {
        float s = Mathf.Min(userS + legDistance, pathLength);
        return PathPolyline.PointAt(_points, s);
    }

    /// <summary>Titik samping tujuan akhir, offset tetap ke satu sisi relatif arah rute
    /// terakhir -- supaya avatar berdiri DI SAMPING POI, tidak menghalangi papan nama/pintu
    /// POI itu sendiri.</summary>
    private Vector3 ComputeArrivalSidePoint()
    {
        Vector3 dest = _points[_points.Count - 1];
        Vector3 dir = _points.Count >= 2
            ? dest - _points[_points.Count - 2]
            : dest - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
        dir.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
        return dest + side * sideOffsetDistance;
    }

    /// <summary>Gerakkan avatar menuju target dengan kecepatan KONSTAN yang diredam
    /// SmoothDamp cuma di fase mulai bergerak dari diam -- bukan fungsi jarak seperti model
    /// lama. Dipakai jalur LeadingPath, Chasing, dan jalan ke titik samping POI: satu jalur
    /// gerak, bukan tiga salinan logika yang bisa saling melenceng.
    /// Mengembalikan true begitu sudah cukup dekat target (waypointArrivalEpsilon).</summary>
    private bool MoveToward(Vector3 target, float targetSpeed)
    {
        Vector3 before = transform.position;
        if (Vector3.Distance(before, target) <= waypointArrivalEpsilon)
        {
            Drive(0f);
            return true;
        }

        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);
        transform.position = Vector3.MoveTowards(before, target, _currentSpeed * Time.deltaTime);

        Vector3 moved = transform.position - before;
        if (moved.sqrMagnitude > 1e-8f) FaceTowards(transform.position + moved);
        Drive(moved.magnitude / Mathf.Max(Time.deltaTime, 1e-5f));
        return false;
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
        // sudah terasa sebagai langkah yang "tidak pas". Sekarang kecepatan gerak SELALU
        // salah satu dari dua konstanta (moveSpeed/chaseSpeed), jadi rasio ini juga jauh
        // lebih stabil daripada model lama yang bisa berapa saja.
        //
        // Klip diam (Idle, Wave) averageSpeed-nya 0, jadi saat tidak berjalan kecepatan
        // putar dikembalikan normal supaya sapaan tidak ikut melambat/dipercepat.
        float ratio = (speed > 0.05f && walkClipSpeed > 0.01f) ? speed / walkClipSpeed : 1f;
        animator.speed = Mathf.Clamp(ratio, 0.4f, 1.8f);
    }

    private void SetVisible(bool v)
    {
        if (visualRoot != null && visualRoot.activeSelf != v) visualRoot.SetActive(v);
    }

    private void SetState(GuideState s)
    {
        if (_state == s) return;
        _state = s;
        Debug.Log($"[AvatarGuide] state -> {s}");
    }
}
