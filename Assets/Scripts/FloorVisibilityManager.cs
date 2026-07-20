using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ADR-018: tampilkan hanya POI di lantai yang sama dengan kamera user (declutter marker
/// AR pada map multi-lantai yang bertumpuk vertikal). Lantai di-derive dari clustering
/// posisi Y POI secara otomatis — BUKAN band ketinggian hardcode — supaya benar untuk
/// gedung RSI berapa pun tinggi lantainya, tanpa perlu tahu angkanya sebelumnya.
///
/// Yang ditoggle cuma Canvas marker tiap POI, BUKAN GameObject POIData-nya sendiri —
/// routing/pencarian (POIManager, NavigationAdapter) tetap utuh untuk semua lantai
/// walau markernya sedang disembunyikan.
/// </summary>
public class FloorVisibilityManager : MonoBehaviour
{
    [SerializeField] private POIManager poiManager;
    [SerializeField] private UaaLEntryPoint uaaLEntryPoint;
    [SerializeField] private Camera arCamera;

    [Header("Clustering")]
    [Tooltip("Jarak Y minimum (meter) antar-POI berurutan untuk dianggap lantai berbeda. " +
             "Harus di antara variasi Y dalam satu lantai dan jarak antar-lantai asli.")]
    [SerializeField] private float minFloorGap = 1.5f;

    [Header("Stabilitas (ADR-018)")]
    [Tooltip("Jendela smoothing posisi Y kamera (detik) — meredam jitter/drift AR sesaat.")]
    [SerializeField] private float smoothingSeconds = 0.5f;
    [Tooltip("Lantai terdekat baru harus konsisten selama durasi ini (detik) sebelum benar-benar " +
             "berpindah — mencegah marker kedip di tangga/batas lantai.")]
    [SerializeField] private float hysteresisSeconds = 1.0f;
    [Tooltip("Seberapa sering evaluasi ulang (detik). Transisi lantai selambat kecepatan jalan " +
             "orang, tidak perlu tiap frame.")]
    [SerializeField] private float evaluateInterval = 0.2f;

    [SerializeField] private bool logChanges = true;

    // Floor MEMBERSHIP (POI -> floor index) di-cache: invariant terhadap transform rigid
    // localize (relatif antar-POI tidak berubah). Tapi nilai centroid dihitung LIVE tiap
    // evaluasi dari posisi POI saat itu — supaya benar walau MultiSet menggeser "Map Space"
    // saat align/relocalize (kalau di-cache, centroid basi vs kamera live = lantai salah).
    private readonly List<List<POIData>> _floorPois = new List<List<POIData>>();
    private readonly Dictionary<POIData, int> _floorOfPoi = new Dictionary<POIData, int>();
    private readonly Dictionary<POIData, Canvas> _markerOf = new Dictionary<POIData, Canvas>();

    private float _smoothedY;
    private bool _hasSmoothedY;
    private float _evalTimer;
    private bool _loggedFirstEval;

    private int _currentFloor = -1;
    private int _pendingFloor = -1;
    private float _pendingSince;
    private POIData _lastActiveTarget;

    private void Awake()
    {
        if (poiManager == null)
            poiManager = FindFirstObjectByType<POIManager>();
        if (uaaLEntryPoint == null)
            uaaLEntryPoint = UaaLEntryPoint.Instance != null ? UaaLEntryPoint.Instance : FindFirstObjectByType<UaaLEntryPoint>();
        if (arCamera == null)
            arCamera = Camera.main;
    }

    /// <summary>
    /// Wire sebagai persistent listener tambahan pada UnityEvent LocalizationSuccess yang
    /// sama dengan PhotonManager/UaaLEntryPoint (MapLocalizationManager) — posisi POI baru
    /// valid & stabil pasca-localize (ADR-007), jadi clustering dibangun di sini.
    /// </summary>
    public void OnLocalizationSuccess()
    {
        BuildFloorClusters();
    }

    private void BuildFloorClusters()
    {
        _floorPois.Clear();
        _floorOfPoi.Clear();
        _markerOf.Clear();
        _pendingFloor = -1;
        _loggedFirstEval = false;
        // _currentFloor dan _hasSmoothedY SENGAJA TIDAK direset di sini — lihat Amandemen 018-A.
        // Method ini dipanggil dari listener LocalizationSuccess, yang menyala berkali-kali
        // sepanjang sesi (backgroundLocalization SDK relocalize periodik di latar belakang),
        // bukan cuma sekali di awal. Mereset _currentFloor tiap kali berarti menebak ulang
        // status lantai dari SATU sampel Y instan setiap relocalize latar belakang — persis
        // noise yang smoothing+hysteresis di Update() memang dirancang untuk meredam, tapi
        // dilewati begitu saja oleh reset ini. Keanggotaan cluster (_floorPois) sendiri aman
        // dihitung ulang tiap saat — ia invariant terhadap transform rigid Map Space.

        if (poiManager == null)
        {
            Debug.LogWarning("[FloorVisibilityManager] poiManager tidak ditemukan, clustering dibatalkan.");
            return;
        }

        var pois = new List<POIData>(poiManager.GetAllPOIs());
        // Grouping berbasis GAP Y berurutan — invariant terhadap offset/transform rigid,
        // jadi tetap benar walau dijalankan sebelum Map Space selesai di-align.
        pois.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        float lastY = float.NegativeInfinity;
        foreach (var poi in pois)
        {
            float y = poi.transform.position.y;
            if (_floorPois.Count == 0 || y - lastY > minFloorGap)
                _floorPois.Add(new List<POIData>());
            int floorIndex = _floorPois.Count - 1;
            _floorPois[floorIndex].Add(poi);
            _floorOfPoi[poi] = floorIndex;
            lastY = y;

            var marker = poi.GetComponentInChildren<Canvas>(true);
            if (marker != null)
                _markerOf[poi] = marker;
        }

        if (logChanges)
            Debug.Log($"[FloorVisibilityManager] {_floorPois.Count} lantai terdeteksi dari {pois.Count} POI.");

        if (_floorPois.Count == 0)
        {
            _currentFloor = -1;
            return;
        }

        // Localize pertama sesi ini (belum pernah ada lantai), atau jumlah cluster berubah
        // sehingga index lama jadi tidak sah -> reset. Selain itu, _currentFloor DIPERTAHANKAN
        // (lihat komentar di awal method) dan Update() yang menuntaskan lewat smoothing+hysteresis.
        if (_currentFloor < 0 || _currentFloor >= _floorPois.Count)
            _currentFloor = -1;
        else
            ApplyVisibility();
    }

    /// <summary>Centroid Y lantai dihitung LIVE dari posisi POI saat ini (bukan cache).</summary>
    private float FloorCentroidY(int floor)
    {
        var group = _floorPois[floor];
        float sum = 0f;
        foreach (var p in group) sum += p.transform.position.y;
        return sum / group.Count;
    }

    private void Update()
    {
        // Lazy-build: clustering tidak bergantung HANYA pada event LocalizationSuccess.
        // Di Editor Play event itu tidak nyala natural (MultiSet tak benar-benar localize;
        // tombol debug "L" memanggil PhotonManager langsung, bukan lewat UnityEvent), dan di
        // device timing listener bisa beda. Grouping gap-based + centroid live = aman dibangun
        // begitu POI sudah ke-scan, tanpa nunggu localize.
        if (_floorPois.Count == 0)
        {
            if (poiManager != null && poiManager.GetAllPOIs().Count > 0)
                BuildFloorClusters();
            if (_floorPois.Count == 0)
                return;
        }

        _evalTimer += Time.deltaTime;
        if (_evalTimer < evaluateInterval)
            return;
        float dt = _evalTimer;
        _evalTimer = 0f;

        var activeTarget = uaaLEntryPoint != null ? uaaLEntryPoint.ActiveNavTarget : null;
        bool targetChanged = activeTarget != _lastActiveTarget;
        _lastActiveTarget = activeTarget;

        if (arCamera == null)
        {
            if (targetChanged) ApplyVisibility();
            return;
        }

        float y = arCamera.transform.position.y;
        if (!_hasSmoothedY)
        {
            _smoothedY = y;
            _hasSmoothedY = true;
        }
        else
        {
            float alpha = smoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / smoothingSeconds);
            _smoothedY = Mathf.Lerp(_smoothedY, y, alpha);
        }

        int nearest = NearestFloor(_smoothedY);

        // Diagnostik device (logcat): satu baris pertama menunjukkan rentang Y nyata pasca-localize
        // vs centroid tiap lantai — biar ketahuan kalau kamera dan POI beda frame koordinat.
        if (logChanges && !_loggedFirstEval)
        {
            _loggedFirstEval = true;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _floorPois.Count; i++) sb.Append($" F{i}={FloorCentroidY(i):F2}");
            Debug.Log($"[FloorVisibilityManager] eval#1: cameraY={y:F2} smoothedY={_smoothedY:F2} nearest={nearest} centroids:{sb}");
        }

        if (_currentFloor < 0)
        {
            // Belum ada lantai aktif (baru localize) -> langsung set, tanpa hysteresis.
            SetCurrentFloor(nearest);
            return;
        }

        if (nearest == _currentFloor)
        {
            _pendingFloor = -1;
            if (targetChanged) ApplyVisibility();
            return;
        }

        if (nearest != _pendingFloor)
        {
            _pendingFloor = nearest;
            _pendingSince = Time.time;
            if (targetChanged) ApplyVisibility();
            return;
        }

        if (Time.time - _pendingSince >= hysteresisSeconds)
        {
            SetCurrentFloor(nearest); // ApplyVisibility() ikut kepanggil di sini
        }
        else if (targetChanged)
        {
            ApplyVisibility();
        }
    }

    private int NearestFloor(float y)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _floorPois.Count; i++)
        {
            float d = Mathf.Abs(FloorCentroidY(i) - y);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private void SetCurrentFloor(int floor)
    {
        bool changed = _currentFloor != floor;
        _currentFloor = floor;
        if (changed && logChanges)
            Debug.Log($"[FloorVisibilityManager] Lantai aktif berubah -> {floor}");
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        POIData activeTarget = uaaLEntryPoint != null ? uaaLEntryPoint.ActiveNavTarget : null;

        foreach (var kvp in _floorOfPoi)
        {
            POIData poi = kvp.Key;
            int floor = kvp.Value;
            bool visible = floor == _currentFloor || poi == activeTarget;

            if (_markerOf.TryGetValue(poi, out var marker) && marker != null)
                marker.gameObject.SetActive(visible);
        }
    }

    // --- API untuk konsumen lain (T5.3 / ADR-020) ---

    /// <summary>
    /// Indeks lantai user saat ini, -1 kalau clustering belum siap. Sengaja indeks, BUKAN
    /// label string: perbandingan lintas-lantai harus memakai hasil clustering geometri yang
    /// sama, bukan pencocokan teks yang bisa meleset karena beda spasi/ejaan.
    /// </summary>
    public int CurrentFloorIndex => _currentFloor;

    /// <summary>Indeks lantai sebuah POI, false kalau POI itu tidak ikut ter-cluster.</summary>
    public bool TryGetFloorIndex(POIData poi, out int floorIndex)
    {
        if (poi != null && _floorOfPoi.TryGetValue(poi, out floorIndex))
            return true;
        floorIndex = -1;
        return false;
    }

    /// <summary>
    /// True kalau POI berada di lantai yang BERBEDA dari user. False juga saat status belum
    /// bisa ditentukan (clustering belum siap / POI tak dikenal) — pemanggil harus memilih
    /// perilaku netral saat ragu, jangan mengklaim beda lantai tanpa dasar.
    /// </summary>
    public bool IsOnDifferentFloor(POIData poi)
    {
        if (_currentFloor < 0) return false;
        return TryGetFloorIndex(poi, out int f) && f != _currentFloor;
    }

    /// <summary>
    /// Amandemen 018-A/020-C: estimasi lantai SAAT INI dari posisi kamera, dihitung fresh —
    /// TANPA smoothing, TANPA hysteresis, dan TIDAK menyentuh _currentFloor (yang menggerakkan
    /// visibility marker dan sengaja dijaga stabil). Dipakai HANYA oleh konsumen yang butuh
    /// jawaban seketika di satu titik keputusan (mis. FloorTransitionController mengonfirmasi
    /// lantai lewat jendela konsistensi setelah relocalize) — bukan untuk decluttering marker.
    /// -1 kalau clustering belum siap.
    /// </summary>
    public int ComputeInstantFloor()
    {
        if (_floorPois.Count == 0 || arCamera == null) return -1;
        return NearestFloor(arCamera.transform.position.y);
    }

    // --- Debug harness (konsisten dengan pola UaaLEntryPoint T1.7) ---

    [ContextMenu("Debug/Rebuild floor clusters")]
    private void Debug_RebuildClusters() => BuildFloorClusters();

    [ContextMenu("Debug/Log current floor state")]
    private void Debug_LogState()
    {
        Debug.Log($"[FloorVisibilityManager] currentFloor={_currentFloor}, lantai terdeteksi={_floorPois.Count}, " +
                  $"smoothedY={_smoothedY:F2}, activeTarget={(uaaLEntryPoint != null ? uaaLEntryPoint.ActiveNavTarget?.EffectiveName : "n/a")}");
    }
}
