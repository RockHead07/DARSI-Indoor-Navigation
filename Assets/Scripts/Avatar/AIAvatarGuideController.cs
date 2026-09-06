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
///
/// CHASING TIBA BUKAN LANGSUNG JALAN LAGI (2026-09-04): begitu Chasing berhasil menyusul
/// pengguna (chaseArrivalDistance), avatar BERHENTI dan melambai dulu (menarik perhatian,
/// bukan diam-diam lanjut seolah tidak terjadi apa-apa) sebelum memilih waypoint berikutnya
/// -- pakai gerbang hold yang sama dengan sapaan awal StartLeading(), lihat greetingHoldSeconds.
///
/// DUA JALUR MASUK CHASING, BUKAN SATU (2026-09-04): chaseStallSeconds (timer) HANYA jalan
/// selama WaitingForUser -- LeadingPath sebelumnya tidak punya pengaman jarak sama sekali.
/// chaseDistanceTrigger menutup celah itu: dari state APA PUN, begitu pengguna sungguhan
/// jauh dari avatar, langsung Chasing seketika tanpa menunggu timer.
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
    [Tooltip("Tambahan jarak leg (meter) tiap kali pengguna BERHASIL mendekat sendiri " +
             "(advanceTriggerDistance) -- avatar jalan makin jauh tiap etape, bukan selalu " +
             "legDistance yang sama (permintaan pemilik project 2026-09-04). SENGAJA hanya " +
             "aktif saat navigation != null, yaitu sesi pathfinding POI sungguhan (MultiSet " +
             "NavigationController) -- rig uji sandbox (tanpa NavigationController) TIDAK " +
             "ikut tumbuh, supaya perilaku uji tetap dapat diprediksi. Reset ke 0 tiap sesi " +
             "baru (StartLeading()) dan tiap kali Chasing terpaksa menjemput -- pengguna yang " +
             "harus dijemput bukan 'berhasil mendekat sendiri', jadi tidak layak dapat bonus.")]
    [SerializeField] private float legDistanceGrowth = 1.0f;
    [Tooltip("Batas atas legDistance + legDistanceGrowth terakumulasi, supaya avatar tidak " +
             "akhirnya menghilang jauh di depan di rute yang sangat panjang. Sekitar 2x " +
             "legDistance dasar adalah titik awal yang wajar.")]
    [SerializeField] private float maxLegDistance = 6.0f;
    [Tooltip("Seberapa dekat pengguna harus sampai ke titik saat ini sebelum avatar memilih " +
             "titik berikutnya dan jalan lagi.")]
    [SerializeField] private float advanceTriggerDistance = 1.5f;
    [Tooltip("Berapa lama avatar menunggu di titiknya sebelum menyerah dan balik menjemput " +
             "pengguna (Chasing) -- supaya pengguna tidak pernah benar-benar tersesat. " +
             "Dinaikkan dari 6 ke 8 detik (2026-09-04) supaya pasien yang berhenti sejenak " +
             "membaca papan nama tidak langsung memicu Chasing. HANYA berlaku selama " +
             "WaitingForUser -- lihat chaseDistanceTrigger untuk celah yang timer ini " +
             "TIDAK tutup (pengguna jauh SELAGI avatar masih LeadingPath).")]
    [SerializeField] private float chaseStallSeconds = 8.0f;
    [Tooltip("Jarak darurat (meter) antara pengguna dan avatar -- begitu tercapai, avatar " +
             "LANGSUNG Chasing dari state APA PUN (LeadingPath maupun WaitingForUser), tanpa " +
             "menunggu chaseStallSeconds. Menutup celah nyata (2026-09-04, dilaporkan pemilik " +
             "project): sebelumnya avatar SAMA SEKALI tidak memantau jarak pengguna selama " +
             "LeadingPath -- hanya WaitingForUser yang punya pengaman waktu. Kalau pengguna " +
             "jauh tertinggal atau salah arah SELAGI avatar masih jalan ke waypoint-nya " +
             "sendiri, sebelumnya tidak ada reaksi sampai avatar sempat tiba dan mulai " +
             "menunggu -- bisa terlambat kalau pengguna sungguhan tersesat.")]
    [SerializeField] private float chaseDistanceTrigger = 15.0f;
    [Tooltip("Jarak henti KHUSUS saat menjemput (Chasing) -- SENGAJA terpisah dari " +
             "waypointArrivalEpsilon, bukan berbagi nilai yang sama. waypointArrivalEpsilon " +
             "(0,15 m) pas untuk berhenti di titik rute kosong, tapi kalau dipakai juga untuk " +
             "berhenti tepat di depan PENGGUNA, avatar wajib menembus fadeStartDistance " +
             "(0,9 m) lalu fadeEndDistance (0,5 m) milik AvatarSafetyFade dulu -- itu " +
             "penyebab avatar 'hilang mendadak lalu muncul lagi entah di mana' yang " +
             "dilaporkan pengguna, dikonfirmasi lewat telemetri Play Mode 2026-09-04 (avatar " +
             "tetap bergerak SELAMA renderer padam, jadi terlihat teleport begitu muncul " +
             "lagi). Nilai ini WAJIB lebih besar dari AvatarSafetyFade.fadeStartDistance " +
             "supaya Chasing tidak pernah masuk zona itu.")]
    [SerializeField] private float chaseArrivalDistance = 1.5f;
    [Tooltip("Sisa rute sependek ini dianggap sudah tiba.")]
    [SerializeField] private float arrivalThreshold = 1.5f;
    [Tooltip("Jarak dari tujuan sesungguhnya ke titik SAMPING tempat avatar berdiri saat " +
             "tiba -- supaya avatar tidak nangkring persis di atas POI. Sisi tetap (kiri " +
             "relatif arah rute terakhir), sengaja tidak dibuat adaptif ke geometri ruangan " +
             "dulu -- keputusan sadar, lihat docs/superpowers/plans kalau ada laporan avatar " +
             "kejeblos dinding di lorong sempit.")]
    [SerializeField] private float sideOffsetDistance = 1.0f;
    [Tooltip("Seberapa dekat dianggap 'sudah sampai' di suatu titik RUTE (waypoint/titik " +
             "samping POI). BUKAN dipakai untuk Chasing lagi (2026-09-04) -- itu sekarang " +
             "pakai chaseArrivalDistance sendiri, lihat tooltip-nya kenapa dipisah.")]
    [SerializeField] private float waypointArrivalEpsilon = 0.15f;

    [Header("Gerak")]
    [Tooltip("Kecepatan jalan KONSTAN avatar menuju titiknya. Tidak pernah berubah karena " +
             "jarak -- itulah intinya model ini. Diturunkan dari 1,4 ke 1,1 (2026-09-04, " +
             "permintaan pemilik project) supaya jalan normal terasa lebih santai, tidak " +
             "buru-buru -- animasi Walk TETAP main di walkAnimSpeed (lihat field itu), " +
             "keduanya sekarang SENGAJA lepas satu sama lain, bukan diikat rasio seperti " +
             "sebelumnya.")]
    [SerializeField] private float moveSpeed = 1.1f;
    [Tooltip("Kecepatan KONSTAN saat menjemput pengguna (state Chasing). Sengaja tetap, " +
             "bukan proporsional ke jarak -- itu pola yang terbukti bikin gerakan tidak wajar " +
             "di model lama. Dinaikkan ke 2,8 (2026-09-04, permintaan pemilik project) supaya " +
             "menjemput terasa sungguhan berlari -- dipasangkan dengan klip MediumRun di " +
             "BlendTree Locomotion (threshold-nya harus sama persis dengan angka ini, itu " +
             "murni menentukan pose mana yang di-blend, BUKAN kecepatan putar animasi). " +
             "Kecepatan putar animasi saat berlari main di runAnimSpeed sendiri, lepas dari " +
             "angka ini -- lihat field itu.")]
    [SerializeField] private float chaseSpeed = 2.8f;
    [SerializeField] private float turnSpeed = 6.0f;
    [Tooltip("Waktu meredam PERUBAHAN kecepatan (detik) tiap kali avatar mulai bergerak dari " +
             "diam -- supaya ada fase mempercepat singkat, bukan langsung penuh seketika.")]
    [SerializeField] private float speedSmoothTime = 0.4f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [Tooltip("Kecepatan PUTAR (animator.speed) klip Walk saat berjalan normal -- LEPAS dari " +
             "moveSpeed (2026-09-04, permintaan pemilik project). Sebelumnya dua-duanya " +
             "diikat lewat rasio speed/walkClipSpeed supaya kaki tidak selip; itu sekarang " +
             "SENGAJA dilepas -- klip Walk Mixamo yang dipakai IN-PLACE (root motion nol, " +
             "dikonfirmasi lewat AnimationUtility ke curve RootT.x/y/z) jadi tidak ada " +
             "kecepatan 'sungguhan' untuk diselaraskan sejak awal. 1.0 = kecepatan alami " +
             "hasil mocap, kurangi sedikit (mis. 0,9) kalau ingin terlihat sedikit lebih " +
             "lambat/tenang.")]
    [SerializeField] private float walkAnimSpeed = 1.0f;
    [Tooltip("Kecepatan PUTAR (animator.speed) klip MediumRun saat menjemput (Chasing) -- " +
             "LEPAS dari chaseSpeed, alasan sama dengan walkAnimSpeed. Default 1.0 = klip " +
             "main di kecepatan alami mocap-nya.")]
    [SerializeField] private float runAnimSpeed = 1.0f;
    [Tooltip("Trigger sapaan. DIPAKAI ULANG di TIGA tempat, bukan tiga gestur beda: (1) awal " +
             "StartLeading(), (2) begitu Chasing berhasil menyusul pengguna -- menarik " +
             "perhatian sebelum jalan lagi (2026-09-04), (3) begitu tiba di tujuan akhir " +
             "(ArrivalPointing) -- 'kita sudah sampai'. Semuanya melambai, bukan gestur " +
             "berbeda per konteks.")]
    [SerializeField] private string waveParam = "Wave";
    [Tooltip("Berapa lama avatar WAJIB diam (Drive(0), tidak jalan) begitu Wave dipicu -- " +
             "dipakai gerbang yang sama untuk sapaan awal StartLeading() MAUPUN begitu " +
             "Chasing berhasil menyusul pengguna (2026-09-04) -- supaya tidak terlihat mulai " +
             "jalan sambil masih melambai di kedua kasus. Diambil dari klip Wave sungguhan: " +
             "length 3,0 s, transisi Wave -> Locomotion exitTime 0,95 (AvatarGuide.controller) " +
             "= 2,85 s -- dibulatkan sedikit ke atas jadi 2,9 s supaya jalan baru mulai TEPAT " +
             "setelah gestur benar-benar selesai blending, bukan di tengah crossfade. Wave di " +
             "tujuan (arrival) TIDAK butuh field ini -- di sana avatar sudah berhenti permanen " +
             "sebelum melambai, tidak akan jalan lagi sampai StartLeading() berikutnya.")]
    [SerializeField] private float greetingHoldSeconds = 2.9f;

    private Transform _user;
    private LineRenderer _line;
    private Vector3[] _buffer = new Vector3[0];
    private readonly List<Vector3> _points = new List<Vector3>();
    private GuideState _state = GuideState.IdleStand;
    private bool _leading;
    private int _speedHash, _waveHash;
    private bool _holdingForGreeting; // diam wajib selama Wave awal masih diputar (greetingHoldSeconds)
    private float _greetingElapsed;   // sudah berapa lama menahan diam untuk Wave awal
    private bool _needsWaypoint;      // belum ada titik dipilih, pilih di frame Update() berikutnya
    private float _legDistanceBonus;  // akumulasi legDistanceGrowth, direset per sesi/Chasing
    private Vector3 _waypoint;        // titik yang sedang dituju/ditunggui (LeadingPath/WaitingForUser)
    private Vector3 _arrivalTarget;   // titik SAMPING POI, dihitung sekali saat tiba terdeteksi
    private bool _hasWavedAtArrival;  // supaya trigger Wave di tujuan cuma sekali, bukan tiap frame
    private float _waitingElapsed;    // sudah berapa lama menunggu di waypoint saat ini
    private float _currentSpeed;      // kecepatan REDAM yang sungguhan dipakai gerak
    private float _speedVelocity;     // state internal SmoothDamp untuk _currentSpeed

    public GuideState CurrentState => _state;
    public bool IsLeading => _leading;

    // Diagnostik, dibaca HUD pengujian. Bukan untuk dipakai logika.
    public float DiagCurrentSpeed => _currentSpeed;
    public Vector3 DiagWaypoint => _waypoint;
    public bool DiagHoldingForGreeting => _holdingForGreeting;

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
        _legDistanceBonus = 0f;   // sesi baru mulai dari legDistance dasar, bukan menyambung sesi lama
        _currentSpeed = 0f;   // mulai dari diam tiap sesi memimpin baru, bukan menyambung kecepatan lama
        _speedVelocity = 0f;
        // Diam WAJIB selama Wave diputar (lihat tooltip greetingHoldSeconds) -- avatar TIDAK
        // boleh mulai jalan sambil masih melambai. _needsWaypoint tetap true, waypoint baru
        // dipilih begitu hold ini berakhir (lihat gerbang di Update()).
        _holdingForGreeting = true;
        _greetingElapsed = 0f;
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

        // Diam wajib selama Wave awal masih diputar -- lihat tooltip greetingHoldSeconds dan
        // StartLeading(). Gerbang ini WAJIB sebelum pembacaan rute, supaya _needsWaypoint
        // (masih true dari StartLeading()) tidak sempat dipakai memilih waypoint dan jalan
        // sebelum gestur sapaan selesai.
        if (_holdingForGreeting)
        {
            FaceTowards(_user.position);
            Drive(0f);
            _greetingElapsed += Time.deltaTime;
            if (_greetingElapsed >= greetingHoldSeconds) _holdingForGreeting = false;
            return;
        }

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

            bool arrivedAtSide = MoveToward(_arrivalTarget, moveSpeed, waypointArrivalEpsilon, walkAnimSpeed);
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

        // JEMPUT DARURAT berbasis jarak (2026-09-04, celah dilaporkan pemilik project): timer
        // chaseStallSeconds HANYA jalan selama WaitingForUser -- selama LeadingPath (avatar
        // masih jalan ke waypoint-nya sendiri), sebelumnya TIDAK ADA pengaman sama sekali
        // kalau pengguna jauh tertinggal atau salah arah. Cek ini jalan dari state APA PUN
        // sebelum switch, supaya Chasing bisa terpicu seketika begitu jaraknya sungguhan
        // jauh (chaseDistanceTrigger), bukan cuma lewat timer WaitingForUser.
        if (_state != GuideState.Chasing)
        {
            Vector3 userFlatEmergency = _user.position; userFlatEmergency.y = 0f;
            Vector3 avatarFlatEmergency = transform.position; avatarFlatEmergency.y = 0f;
            if (Vector3.Distance(userFlatEmergency, avatarFlatEmergency) >= chaseDistanceTrigger)
            {
                _waitingElapsed = 0f;
                SetState(GuideState.Chasing);
            }
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
                    // Pengguna BERHASIL mendekat sendiri -- hanya dalam sesi pathfinding POI
                    // sungguhan (navigation != null) leg berikutnya jadi sedikit lebih jauh
                    // (legDistanceGrowth), lihat tooltip-nya. Rig uji sandbox tidak punya
                    // NavigationController jadi tidak pernah tumbuh, sengaja.
                    if (navigation != null) _legDistanceBonus += legDistanceGrowth;
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
                bool caughtUp = MoveToward(userTarget, chaseSpeed, chaseArrivalDistance, runAnimSpeed);
                if (caughtUp)
                {
                    // Sungguhan sudah di depan pengguna (chaseArrivalDistance) -- BERHENTI
                    // dan lambai dulu untuk menarik perhatian (permintaan pemilik project
                    // 2026-09-04), jangan langsung jalan lagi seolah tidak terjadi apa-apa.
                    // Pakai gerbang hold yang SAMA dengan sapaan awal StartLeading() --
                    // Wave yang dipicu literally sama, jadi durasi tahannya juga harus sama
                    // (greetingHoldSeconds, sudah diselaraskan ke transisi Wave->Locomotion
                    // sungguhan). _needsWaypoint dipasang true supaya waypoint BARU dipilih
                    // begitu hold berakhir, bukan dihitung sekarang pakai userS yang mungkin
                    // sudah basi begitu pengguna akhirnya mulai jalan lagi.
                    FaceTowards(_user.position);
                    Drive(0f);
                    if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(_waveHash);
                    _holdingForGreeting = true;
                    _greetingElapsed = 0f;
                    _needsWaypoint = true;
                    _waitingElapsed = 0f;
                    // Pengguna harus DIJEMPUT, bukan mendekat sendiri -- bukan pertumbuhan yang
                    // layak dipertahankan, reset supaya leg berikutnya kembali ke legDistance dasar.
                    _legDistanceBonus = 0f;
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
                bool reachedWaypoint = MoveToward(_waypoint, moveSpeed, waypointArrivalEpsilon, walkAnimSpeed);
                if (reachedWaypoint)
                {
                    _waitingElapsed = 0f;
                    SetState(GuideState.WaitingForUser);
                }
                break;
            }
        }
    }

    /// <summary>Titik legDistance (+ _legDistanceBonus terakumulasi, di sesi POI sungguhan)
    /// di depan posisi pengguna SAAT INI di sepanjang rute. Dipanggil hanya saat avatar
    /// SELESAI di titik lama (bukan tiap frame) -- itulah yang membedakan model ini dari
    /// model lama yang mengikat ulang tiap frame.</summary>
    private Vector3 NextWaypoint(float userS, float pathLength)
    {
        float effectiveLeg = Mathf.Min(legDistance + _legDistanceBonus, maxLegDistance);
        float s = Mathf.Min(userS + effectiveLeg, pathLength);
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
    /// gerak, bukan tiga salinan logika yang bisa saling melenceng. Jarak henti (arrivalEpsilon)
    /// diberikan pemanggil, BUKAN satu nilai tetap milik method ini -- Chasing butuh jarak
    /// henti lebih jauh daripada berhenti di waypoint kosong (lihat chaseArrivalDistance).
    /// animSpeed juga diberikan pemanggil -- kecepatan gerak (targetSpeed) dan kecepatan
    /// putar animasi (animSpeed) SENGAJA lepas satu sama lain (2026-09-04).
    /// Mengembalikan true begitu jarak ke target &lt;= arrivalEpsilon.</summary>
    private bool MoveToward(Vector3 target, float targetSpeed, float arrivalEpsilon, float animSpeed)
    {
        Vector3 before = transform.position;
        if (Vector3.Distance(before, target) <= arrivalEpsilon)
        {
            Drive(0f);
            return true;
        }

        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);
        transform.position = Vector3.MoveTowards(before, target, _currentSpeed * Time.deltaTime);

        Vector3 moved = transform.position - before;
        if (moved.sqrMagnitude > 1e-8f) FaceTowards(transform.position + moved);
        Drive(moved.magnitude / Mathf.Max(Time.deltaTime, 1e-5f), animSpeed);
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

    /// <summary>animSpeed default 1f supaya semua pemanggil lama (Drive(0f) saat diam/
    /// menunggu) tidak perlu diubah -- animator.speed cuma relevan saat sungguhan bergerak.
    ///
    /// KEPUTUSAN 2026-09-04 (permintaan pemilik project): kecepatan gerak (speed, m/s,
    /// dipakai untuk BlendTree Speed parameter + menentukan pose mana yang di-blend) dan
    /// kecepatan putar animasi (animSpeed, dipakai untuk animator.speed) SENGAJA dilepas
    /// satu sama lain di sini -- sebelumnya diikat lewat rasio speed/walkClipSpeed supaya
    /// kaki tidak selip, tapi klip Walk/MediumRun Mixamo yang dipakai IN-PLACE (root motion
    /// nol) sejak awal tidak punya kecepatan "sungguhan" untuk diselaraskan itu. Sekarang
    /// tiap pemanggil MoveToward() memberi animSpeed-nya sendiri (walkAnimSpeed/
    /// runAnimSpeed) -- biasanya 1.0 (klip main di kecepatan alami mocap-nya) lepas dari
    /// seberapa cepat moveSpeed/chaseSpeed sungguhan.</summary>
    private void Drive(float speed, float animSpeed = 1f)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        animator.SetFloat(_speedHash, speed, 0.1f, Time.deltaTime);
        animator.speed = speed > 0.05f ? animSpeed : 1f;
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
