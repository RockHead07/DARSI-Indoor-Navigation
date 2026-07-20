using MultiSet;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// T5.3 / ADR-020 — rute lintas-lantai TERSEGMENTASI dengan handoff eksplisit di lift.
///
///   Idle -> ToConnector -> AwaitingRelocalize -> ToDestination -> Idle
///
/// Saat user memilih POI di lantai lain, navigasi TIDAK diarahkan ke tujuan akhir (jalurnya
/// tidak ada — tiap lantai pulau navmesh sendiri). Yang dilakukan: arahkan dulu ke lift
/// terdekat di lantai user, lalu setelah user naik & memindai ulang, sambung ke tujuan asli.
///
/// CATATAN PENTING soal ADR-020 poin 3 ("penghubung vertikal = NavMeshLink"): NavMeshLink
/// TIDAK dipakai, dan ternyata tidak diperlukan. Alasan poin itu memilih NavMeshLink adalah
/// "terdeteksi di kode sehingga trigger handoff eksplisit" — tapi karena state machine ini
/// yang MENGARAHKAN user ke lift, dia sudah tahu persis kapan handoff terjadi tanpa perlu
/// sinyal dari navmesh. Dan karena rutenya tersegmentasi, jalur kontinu antar-lantai memang
/// tidak pernah dibutuhkan. Menambah NavMeshLink justru berisiko: dia membuat jalur lintas-
/// lantai bisa dihitung, sehingga SDK berpeluang menggambar garis rute menembus plafon —
/// hal yang justru dilarang ADR-020 poin 2.
///
/// Perpindahan agent antar-lantai ditangani SDK sendiri (AgentPosition.Update): kalau agent
/// berakhir >1.5 m di atas / >3.5 m di bawah kamera, dia di-warp ke ketinggian kamera. Jadi
/// setelah user naik lift & re-localize, agent pindah ke pulau navmesh lantai baru otomatis.
/// </summary>
public class FloorTransitionController : MonoBehaviour
{
    private enum Phase { Idle, ToConnector, AwaitingRelocalize, ToDestination }

    [SerializeField] private FloorVisibilityManager floorVisibility;
    [SerializeField] private POIManager poiManager;
    // Tipe yang BENAR-BENAR dipakai scene ini (dicek lewat refleksi, bukan diasumsikan):
    // GameObject-nya bernama "MapLocalizationManager" tapi komponennya SingleFrameLocalizationManager.
    [SerializeField] private SingleFrameLocalizationManager localizationManager;

    [Tooltip("Kategori POI yang dianggap penghubung vertikal (amandemen ADR-020-A).")]
    [SerializeField] private string connectorCategory = "Lift";

    [Header("Auto-relocalize (setelah sampai di lift)")]
    [Tooltip("Kalau lokasi belum dikenali setelah sekian detik, beri tahu user supaya tidak " +
             "mandek diam (ADR-020). Ini HANYA pengawas — pengulangan localize dilakukan SDK " +
             "sendiri lewat backgroundLocalization, bukan oleh script ini.")]
    [SerializeField] private float relocalizeTimeout = 90f;

    [Tooltip("Amandemen 020-C: setelah localize sukses, ComputeInstantFloor() harus konsisten " +
             "menunjuk lantai tujuan selama sekian detik berturut-turut sebelum navigasi " +
             "disambung. Bukan tebakan final — perlu di-tune dari data lapangan asli.")]
    [SerializeField] private float floorConfirmWindow = 1f;

    [SerializeField] private bool logChanges = true;

    private Phase _phase = Phase.Idle;
    private POI _finalDestination;
    private POI _connector;
    private POI _lastSeenDestination;
    private Coroutine _relocalizeLoop;

    // Amandemen 020-C
    private bool _hasRelocalizedSinceWaiting;
    private float _floorConfirmSince = -1f;

    private void Awake()
    {
        if (floorVisibility == null) floorVisibility = FindFirstObjectByType<FloorVisibilityManager>();
        if (poiManager == null) poiManager = FindFirstObjectByType<POIManager>();
        if (localizationManager == null) localizationManager = FindFirstObjectByType<SingleFrameLocalizationManager>();
    }

    private void OnEnable()
    {
        var nav = NavigationController.instance;
        if (nav != null) nav.DestinationArrived.AddListener(OnArrived);
    }

    private void OnDisable()
    {
        var nav = NavigationController.instance;
        if (nav != null) nav.DestinationArrived.RemoveListener(OnArrived);
    }

    private void Start()
    {
        // NavigationController.instance di-set di Awake-nya sendiri; kalau urutan Awake bikin
        // OnEnable terlewat, pasang di sini. AddListener aman dipanggil dua kali? TIDAK —
        // UnityEvent memperbolehkan duplikat, jadi lepas dulu baru pasang.
        var nav = NavigationController.instance;
        if (nav != null)
        {
            nav.DestinationArrived.RemoveListener(OnArrived);
            nav.DestinationArrived.AddListener(OnArrived);
        }

        HookStopButton();
    }

    /// <summary>
    /// ADR-020 mensyaratkan jalan keluar di SETIAP state — navigasi tidak boleh mandek diam.
    /// Tombol Stop bawaan SDK dipakai ulang alih-alih membuat tombol baru: user sudah tahu
    /// tempatnya, dan tidak perlu authoring UI di scene.
    ///
    /// Listener ditambahkan saat runtime, jadi listener persisten milik SDK (ClickedStopButton
    /// -> StopNavigation) tetap jalan lebih dulu; punya kita menyusul membereskan state
    /// transisi. Aman dipanggil untuk navigasi biasa: CancelTransition() langsung keluar
    /// kalau phase == Idle, jadi tidak memunculkan toast "dibatalkan" di navigasi satu lantai.
    /// </summary>
    private void HookStopButton()
    {
        var ui = NavigationUIController.instance;
        if (ui == null || ui.stopButton == null) return;

        var button = ui.stopButton.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning("[FloorTransition] stopButton tanpa komponen Button — " +
                             "pembatalan saat menunggu localize tidak tersedia.");
            return;
        }

        button.onClick.RemoveListener(CancelTransition);
        button.onClick.AddListener(CancelTransition);
    }

    private void LateUpdate()
    {
        var nav = NavigationController.instance;
        if (nav == null || floorVisibility == null) return;

        ShowWaitingStatus();
        ConfirmFloorProgress();

        POI destination = nav.currentDestination;
        if (destination == _lastSeenDestination) return;
        _lastSeenDestination = destination;

        if (destination == null) return;

        // Tujuan berubah ke sesuatu yang BUKAN langkah kita = user memilih ulang secara manual.
        // Batalkan transisi yang sedang berjalan, jangan diam-diam melanjutkan rencana lama.
        if (_phase != Phase.Idle && destination != _connector && destination != _finalDestination)
        {
            if (logChanges) Debug.Log("[FloorTransition] User memilih tujuan lain — transisi dibatalkan.");
            ResetState();
        }

        if (_phase != Phase.Idle) return;

        var data = destination.GetComponent<POIData>();
        if (data == null) return;

        // Ragu = diam. Kalau clustering belum siap, jangan mengarang instruksi lantai.
        if (!floorVisibility.IsOnDifferentFloor(data)) return;

        BeginTransition(destination, data);
    }

    private void BeginTransition(POI destination, POIData data)
    {
        string floor = data.Floor;
        POI connector = FindNearestConnectorOnUserFloor();

        // Tujuannya lift di lantai lain? Jangan jalankan transisi — user tidak sedang menuju
        // "lift lantai atas", dia cuma butuh lift. Arahkan ke lift di lantai ini, selesai.
        // (Amandemen ADR-020-A: konektor bukan destinasi. Selama Lift masih muncul di daftar
        // SDK, ini yang paling masuk akal; begitu konektor disembunyikan, cabang ini mati sendiri.)
        if (IsConnector(data))
        {
            if (connector != null && connector != destination)
            {
                ToastManager.Instance?.ShowAlert("Lift terdekat ada di lantai ini.");
                if (logChanges) Debug.Log($"[FloorTransition] Tujuan konektor beda lantai -> dialihkan ke '{connector.poiName}' selantai.");
                Retarget(connector);
            }
            return;
        }

        if (connector == null)
        {
            // Jujur: jangan pura-pura mengarahkan kalau tidak ada lift yang bisa dipakai.
            ToastManager.Instance?.ShowAlert(
                $"{data.EffectiveName} berada di {floor}, tapi tidak ada lift yang terdeteksi di lantai ini.");
            Debug.LogWarning($"[FloorTransition] Tidak ada POI kategori '{connectorCategory}' di lantai user.");
            return;
        }

        _finalDestination = destination;
        _connector = connector;
        _phase = Phase.ToConnector;

        ToastManager.Instance?.ShowAlert(
            $"{data.EffectiveName} ada di {floor}. Anda diarahkan ke lift dulu.");
        if (logChanges)
            Debug.Log($"[FloorTransition] ToConnector: '{data.EffectiveName}' ({floor}) via '{connector.poiName}'");

        Retarget(connector);
    }

    private void OnArrived()
    {
        switch (_phase)
        {
            case Phase.ToConnector:
                _phase = Phase.AwaitingRelocalize;
                // Amandemen 020-C: gerbang tertutup lagi. Sebelum localize sukses PERTAMA sejak
                // titik ini, ComputeInstantFloor() tidak boleh dibaca — Map Space masih ter-align
                // ke lantai lama, jadi angkanya bisa kebetulan cocok padahal user masih di lift.
                _hasRelocalizedSinceWaiting = false;
                _floorConfirmSince = -1f;
                string floor = _finalDestination != null
                    ? _finalDestination.GetComponent<POIData>()?.Floor
                    : null;
                ToastManager.Instance?.ShowAlert(
                    $"Anda sampai di lift. Naik ke {floor ?? "lantai tujuan"} — " +
                    "navigasi lanjut otomatis begitu lokasi Anda dikenali.");
                if (logChanges) Debug.Log("[FloorTransition] AwaitingRelocalize — mulai auto-relocalize");
                StartRelocalizeLoop();
                break;

            case Phase.ToDestination:
                if (logChanges) Debug.Log("[FloorTransition] Tujuan akhir tercapai.");
                ResetState();
                break;
        }
    }

    /// <summary>
    /// Wire sebagai persistent listener pada UnityEvent LocalizationSuccess milik
    /// MapLocalizationManager — sama seperti FloorVisibilityManager.OnLocalizationSuccess.
    ///
    /// Amandemen 020-C: method ini HANYA membuka gerbang, bukan mengambil keputusan. Alasannya
    /// tidak bisa diverifikasi dari kode: apakah SDK menerapkan koreksi pose Map Space instan
    /// di frame yang sama dengan event ini, atau menghaluskannya beberapa frame (pola umum SDK
    /// VPS agar visual tidak meloncat) — SDK-nya DLL tertutup. Membaca posisi kamera di sini
    /// berarti bertaruh pada timing yang tak diketahui. Keputusan sesungguhnya dilakukan
    /// ConfirmFloorProgress() lewat jendela konsistensi, bersandar pada tracking ARCore yang
    /// memang terbukti kontinu tiap frame.
    /// </summary>
    public void OnLocalizationSuccess()
    {
        if (_phase != Phase.AwaitingRelocalize) return;
        _hasRelocalizedSinceWaiting = true;
        if (logChanges) Debug.Log("[FloorTransition] Localize sukses — mulai konfirmasi lantai.");
    }

    /// <summary>
    /// Amandemen 020-C: sambung segmen 2 hanya kalau lantai user KONSISTEN menunjuk lantai
    /// tujuan selama floorConfirmWindow berturut-turut. Sengaja TIDAK memakai CurrentFloorIndex:
    /// nilai itu sengaja dibuat stabil/lambat (hysteresis marker, Amandemen 018-A) untuk
    /// mencegah kedip visual — konsekuensi salahnya beda, jadi knob-nya juga dipisah.
    /// </summary>
    private void ConfirmFloorProgress()
    {
        if (_phase != Phase.AwaitingRelocalize || !_hasRelocalizedSinceWaiting) return;
        if (_finalDestination == null) return;

        var data = _finalDestination.GetComponent<POIData>();
        if (data == null) { ResetState(); return; }

        int userFloor = floorVisibility.ComputeInstantFloor();
        bool onTargetFloor = userFloor >= 0
                             && floorVisibility.TryGetFloorIndex(data, out int destFloor)
                             && userFloor == destFloor;

        if (!onTargetFloor)
        {
            // Belum sampai (atau sempat meleset) -> hitungan konfirmasi diulang dari nol.
            // ADR-020 risiko residual: JANGAN berasumsi user menuruti instruksi.
            _floorConfirmSince = -1f;
            return;
        }

        if (_floorConfirmSince < 0f)
        {
            _floorConfirmSince = Time.time;
            return;
        }

        if (Time.time - _floorConfirmSince < floorConfirmWindow) return;

        StopRelocalizeLoop();
        _phase = Phase.ToDestination;
        ToastManager.Instance?.ShowAlert($"Melanjutkan navigasi ke {data.EffectiveName}.");
        if (logChanges) Debug.Log($"[FloorTransition] ToDestination: '{data.EffectiveName}'");
        Retarget(_finalDestination);
    }

    // --- Auto-relocalize ---
    //
    // Kenapa BUKAN "deteksi user menyentuh POI lift di lantai atas": collider POI diadu dengan
    // collider ARCamera di ruang koordinat Unity, dan posisi kamera itu berasal dari tracking AR
    // — yang justru PUTUS di dalam lift (kotak tertutup bergerak, tak ada kontinuitas visual).
    // Kamera akan nyangkut di dekat lift lantai bawah, jadi POI lift atas tak pernah tersentuh.
    // Satu-satunya hakim sah untuk "saya di mana" adalah localize MultiSet itu sendiri.
    //
    // Di dalam lift percobaan ini akan GAGAL terus (dinding polos, tanpa fitur visual) — itu
    // wajar dan justru diinginkan. Begitu pintu terbuka dan kamera melihat lobi lantai tujuan,
    // localize berhasil, dan OnLocalizationSuccess menyambung navigasi.

    private void StartRelocalizeLoop()
    {
        StopRelocalizeLoop();
        if (localizationManager == null)
        {
            Debug.LogWarning("[FloorTransition] SingleFrameLocalizationManager tidak ditemukan — " +
                             "user harus memicu pindai ulang manual.");
            return;
        }

        // SATU panggilan saja, untuk MENYALAKAN ULANG jendela background localization SDK
        // (bgLocalizationDuration, default 60 detik) yang mungkin sudah habis sejak localize
        // pertama. Setelah ini SDK yang mengulang sendiri — jangan di-loop dari sini.
        //
        // Di Editor dilewati: SDK tidak bisa localize di sana dan melempar ERROR merah
        // ("No simulation selected"), yang bikin console berisik oleh kegagalan palsu —
        // berbahaya karena melatih mata untuk mengabaikan error saat debug di device.
#if UNITY_EDITOR
        Debug.Log("[FloorTransition] (editor) LocalizeFrame dilewati — pakai " +
                  "'Debug/Simulate relocalize success' untuk menguji sambungan segmen 2.");
#else
        localizationManager.LocalizeFrame();
#endif

        _relocalizeLoop = StartCoroutine(TimeoutWatch());
    }

    private void StopRelocalizeLoop()
    {
        if (_relocalizeLoop != null)
        {
            StopCoroutine(_relocalizeLoop);
            _relocalizeLoop = null;
        }
    }

    /// <summary>
    /// HANYA menjaga jangan sampai mandek diam (ADR-020). Tidak menggerakkan localize:
    /// SDK sudah punya backgroundLocalization + relocalization + firstLocalizationUntilSuccess,
    /// jadi mengulang LocalizeFrame() dari sini berarti dua penggerak jalan bersamaan —
    /// frame terkirim dobel ke server, dan perekaman multi-frame yang belum selesai bisa
    /// terpotong. Biarkan SDK bekerja; kita cuma mendengarkan hasilnya.
    /// </summary>
    private System.Collections.IEnumerator TimeoutWatch()
    {
        yield return new WaitForSeconds(relocalizeTimeout);

        if (_phase == Phase.AwaitingRelocalize)
        {
            ToastManager.Instance?.ShowAlert(
                "Lokasi Anda belum dikenali. Arahkan kamera ke sekitar, atau batalkan navigasi.");
            Debug.LogWarning("[FloorTransition] relocalize belum berhasil sampai timeout.");
        }
        _relocalizeLoop = null;
    }

    /// <summary>
    /// Selama menunggu localize, layar TIDAK boleh kosong. Begitu user sampai di lift,
    /// navigasi berhenti — dan NavigationUIController mengosongkan destinationName serta
    /// remainingDistance karena IsCurrentlyNavigating() jadi false. Akibatnya user berdiri
    /// di dalam lift tanpa tahu aplikasi sedang menunggu, sedang mencari, atau sudah menyerah.
    ///
    /// Dua label itu dipakai ulang (bukan bikin panel baru): keduanya sudah ada di layar dan
    /// sudah di posisi yang dilihat user saat menavigasi. Ditulis di LateUpdate karena
    /// NavigationUIController.Update() mengosongkannya lebih dulu tiap frame.
    ///
    /// Titik beranimasi sengaja dipakai sebagai pengganti spinner: tanpa gerakan, user tidak
    /// bisa membedakan "sedang mencari" dari "hang".
    /// </summary>
    private void ShowWaitingStatus()
    {
        if (_phase != Phase.AwaitingRelocalize) return;

        var ui = NavigationUIController.instance;
        if (ui == null || _finalDestination == null) return;

        // Menulis teks saja TIDAK cukup: saat sampai di lift, ArrivedAtDestination() memanggil
        // ShowArrivedState() -> ShowNavigationUIElements(false), yang mematikan GameObject
        // "Progress Slider" — INDUK dari kedua label ini. Teksnya terisi benar tapi tak terlihat.
        // Jadi kontainernya dihidupkan lagi selama fase menunggu.
        if (ui.navigationProgressSlider != null && !ui.navigationProgressSlider.activeSelf)
            ui.navigationProgressSlider.SetActive(true);

        // Tombol Stop juga disembunyikan oleh ShowArrivedState(). Tanpa ini, user yang batal
        // naik lift atau salah lantai tidak punya jalan keluar selain menutup paksa aplikasi —
        // dan itu justru skenario paling mungkin saat localize gagal di lapangan.
        if (ui.stopButton != null && !ui.stopButton.activeSelf)
            ui.stopButton.SetActive(true);

        var data = _finalDestination.GetComponent<POIData>();
        string floor = data != null ? data.Floor : null;

        if (ui.destinationName != null)
            ui.destinationName.SetText(_finalDestination.poiName);

        if (ui.remainingDistance != null)
        {
            string dots = new string('.', 1 + (int)(Time.time * 2f) % 3);
            ui.remainingDistance.SetText(
                floor != null
                    ? $"Naik ke {floor} — mencari lokasi{dots}"
                    : $"Mencari lokasi{dots}");
        }
    }

    private bool IsConnector(POIData data) =>
        string.Equals(data.kategori, connectorCategory, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Lift terdekat yang SELANTAI dengan user (jarak lurus dari agent).</summary>
    private POI FindNearestConnectorOnUserFloor()
    {
        if (poiManager == null) return null;
        var nav = NavigationController.instance;
        if (nav == null || nav.agent == null) return null;

        Vector3 from = nav.agent.transform.position;
        POI best = null;
        float bestSqr = float.MaxValue;

        foreach (var poi in poiManager.GetAllPOIs())
        {
            if (poi == null) continue;
            if (!string.Equals(poi.kategori, connectorCategory, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (floorVisibility.IsOnDifferentFloor(poi)) continue;

            var sdkPoi = poi.GetComponent<POI>();
            if (sdkPoi == null) continue;

            float sqr = (poi.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = sdkPoi; }
        }

        return best;
    }

    private void Retarget(POI poi)
    {
        // Set _lastSeenDestination DULU supaya LateUpdate tidak menganggap perubahan ini
        // sebagai pilihan manual user dan membatalkan transisinya sendiri.
        _lastSeenDestination = poi;
        NavigationController.instance.SetPOIForNavigation(poi);
    }

    private void ResetState()
    {
        StopRelocalizeLoop();
        _phase = Phase.Idle;
        _finalDestination = null;
        _connector = null;
        _hasRelocalizedSinceWaiting = false;
        _floorConfirmSince = -1f;
    }

    /// <summary>Jalan keluar wajib (ADR-020): navigasi tidak boleh mandek diam.</summary>
    public void CancelTransition()
    {
        if (_phase == Phase.Idle) return;
        ResetState();
        NavigationController.instance?.StopNavigation();

        // Kembalikan panel yang kita hidupkan sendiri selama menunggu — kalau tidak,
        // dia tertinggal di layar berisi status yang sudah tidak berlaku.
        var ui = NavigationUIController.instance;
        if (ui != null)
        {
            if (ui.navigationProgressSlider != null) ui.navigationProgressSlider.SetActive(false);
            if (ui.stopButton != null) ui.stopButton.SetActive(false);
        }

        ToastManager.Instance?.ShowAlert("Navigasi dibatalkan.");
    }

    // --- Debug harness: relocalize tidak bisa dipicu natural di Editor Play ---

    [ContextMenu("Debug/Log state")]
    private void Debug_LogState() => Debug.Log(
        $"[FloorTransition] phase={_phase}, connector='{(_connector != null ? _connector.poiName : "-")}', " +
        $"final='{(_finalDestination != null ? _finalDestination.poiName : "-")}', " +
        $"lantaiUser(stabil)={floorVisibility?.CurrentFloorIndex}, " +
        $"lantaiUser(instan)={floorVisibility?.ComputeInstantFloor()}, " +
        $"gerbangRelocalize={_hasRelocalizedSinceWaiting}, " +
        $"konfirmasiSejak={(_floorConfirmSince < 0f ? "belum" : (Time.time - _floorConfirmSince).ToString("F1") + "s")}");

    [ContextMenu("Debug/Simulate arrival")]
    private void Debug_SimulateArrival() => OnArrived();

    [ContextMenu("Debug/Simulate relocalize success")]
    private void Debug_SimulateRelocalize() => OnLocalizationSuccess();

    [ContextMenu("Debug/Cancel transition")]
    private void Debug_Cancel() => CancelTransition();
}
