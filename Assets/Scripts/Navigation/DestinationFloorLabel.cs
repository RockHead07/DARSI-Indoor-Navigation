using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ADR-020 / amandemen 020-A dan 020-B: di daftar Destinations bawaan SDK, POI di lantai LAIN
/// tampil "Unreachable" karena tiap lantai adalah pulau NavMesh terpisah. Itu menyesatkan —
/// POI-nya bisa dicapai, lewat lift.
///
/// Script ini menggantinya dengan jarak + lantai, misal "45 m · Lantai 1".
///
/// Jaraknya DIJUMLAHKAN dari dua segmen yang masing-masing berada di dalam satu lantai:
///
///     user -> lift lantai user        (PathComplete, sudah bisa dihitung)
///     lift lantai tujuan -> POI       (PathComplete, sudah bisa dihitung)
///
/// SENGAJA TIDAK memasang NavMeshLink antar-lantai, walaupun itu akan membuat SDK menghitung
/// jaraknya sendiri. Alasannya: NavMeshLink mengubah pathfinding secara GLOBAL — setiap
/// NavMesh.CalculatePath di proyek, termasuk kode SDK yang belum diaudit, mendadak menganggap
/// dua lantai sebagai satu ruang berjalan. Konektivitas NavMesh sebaiknya tetap mencerminkan
/// kenyataan fisik (tidak ada lantai yang menyambung), dan penjumlahan segmen justru lebih
/// jujur: angkanya persis perjalanan yang akan dilalui user.
///
/// POI selantai TIDAK disentuh — label SDK dipakai apa adanya, termasuk "Unreachable" yang
/// menandakan masalah NavMesh asli. Justru karena semua POI lintas-lantai kini punya angka,
/// sisa "Unreachable" jadi menonjol dan tidak bisa bersembunyi.
///
/// Aditif: package SDK tidak di-fork. ListItemUI.Update() menulis ulang label tiap frame,
/// jadi penimpaan dilakukan di LateUpdate — pola yang sama dengan ToastTranslator.
/// </summary>
public class DestinationFloorLabel : MonoBehaviour
{
    [SerializeField] private FloorVisibilityManager floorVisibility;
    [SerializeField] private POIManager poiManager;

    [Tooltip("Kategori POI yang dianggap penghubung vertikal (amandemen ADR-020-A).")]
    [SerializeField] private string connectorCategory = "Lift";

    [Tooltip("Seberapa sering memindai ulang item daftar (detik).")]
    [SerializeField] private float rescanInterval = 0.5f;

    [Tooltip("Seberapa sering jarak lintas-lantai dihitung ulang (detik). Tiap POI butuh DUA " +
             "CalculatePath, jadi jangan per-frame — user berjalan jauh lebih lambat dari itu.")]
    [SerializeField] private float distanceRefresh = 0.5f;

    [SerializeField] private bool logChanges = false;

    private ListItemUI[] _items = System.Array.Empty<ListItemUI>();
    private readonly Dictionary<POIData, string> _labelOf = new Dictionary<POIData, string>();
    private float _rescanTimer;
    private float _distanceTimer;

    private void Awake()
    {
        if (floorVisibility == null) floorVisibility = FindFirstObjectByType<FloorVisibilityManager>();
        if (poiManager == null) poiManager = FindFirstObjectByType<POIManager>();
    }

    private void LateUpdate()
    {
        if (floorVisibility == null) return;

        _rescanTimer -= Time.deltaTime;
        if (_rescanTimer <= 0f)
        {
            _rescanTimer = rescanInterval;
            _items = FindObjectsByType<ListItemUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        _distanceTimer -= Time.deltaTime;
        if (_distanceTimer <= 0f)
        {
            _distanceTimer = distanceRefresh;
            RecomputeLabels();
        }

        foreach (var item in _items)
        {
            if (item == null || item.distance == null) continue;

            var poi = item.dataObject as POI;
            if (poi == null) continue;

            var data = poi.GetComponent<POIData>();
            if (data == null) continue;

            if (_labelOf.TryGetValue(data, out string label) && item.distance.text != label)
            {
                if (logChanges)
                    Debug.Log($"[DestinationFloorLabel] '{poi.poiName}': '{item.distance.text}' -> '{label}'");
                item.distance.text = label;
            }
        }
    }

    private void RecomputeLabels()
    {
        _labelOf.Clear();
        if (poiManager == null) return;

        var nav = NavigationController.instance;
        if (nav == null || nav.agent == null || !nav.agent.isOnNavMesh) return;

        // Segmen 1 dihitung SEKALI: sama untuk semua POI lintas-lantai.
        POIData userLift = FindNearestConnector(floorVisibility.CurrentFloorIndex, nav.agent.transform.position);
        if (userLift == null) return;
        if (!TryPathLength(nav.agent.transform.position, ColliderPoint(userLift), out float toLift)) return;

        foreach (var data in poiManager.GetAllPOIs())
        {
            if (data == null) continue;
            // Ragu = diam. Biarkan label SDK apa adanya daripada mengarang lantai.
            if (!floorVisibility.IsOnDifferentFloor(data)) continue;

            string floor = data.Floor;
            if (string.IsNullOrEmpty(floor)) continue;

            // Lift di lantai TUJUAN — titik keluar user setelah naik lift.
            if (!floorVisibility.TryGetFloorIndex(data, out int targetFloor)) continue;
            POIData exitLift = FindNearestConnector(targetFloor, ColliderPoint(data));

            if (exitLift != null &&
                TryPathLength(ColliderPoint(exitLift), ColliderPoint(data), out float fromLift))
            {
                _labelOf[data] = $"{(int)(toLift + fromLift)} m · {floor}";
            }
            else
            {
                // Tidak ada lift di lantai tujuan, atau segmen 2 tak terhitung —
                // jangan mengarang angka, cukup sebut lantainya.
                _labelOf[data] = floor;
            }
        }
    }

    /// <summary>Titik yang dipakai SDK untuk menghitung rute, di-snap ke NavMesh dulu.</summary>
    private static Vector3 ColliderPoint(POIData data)
    {
        var sdk = data.GetComponent<POI>();
        Vector3 raw = (sdk != null && sdk.poiCollider != null)
            ? sdk.poiCollider.transform.position
            : data.transform.position;
        // CalculatePath hanya men-snap ~1 m secara vertikal; snap eksplisit dengan radius
        // lebih longgar supaya collider yang agak melenceng tidak diam-diam bikin jarak hilang.
        return NavMesh.SamplePosition(raw, out var hit, 3f, NavMesh.AllAreas) ? hit.position : raw;
    }

    private POIData FindNearestConnector(int floorIndex, Vector3 from)
    {
        if (floorIndex < 0) return null;

        POIData best = null;
        float bestSqr = float.MaxValue;
        foreach (var poi in poiManager.GetAllPOIs())
        {
            if (poi == null) continue;
            if (!string.Equals(poi.kategori, connectorCategory, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!floorVisibility.TryGetFloorIndex(poi, out int f) || f != floorIndex) continue;

            float sqr = (poi.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = poi; }
        }
        return best;
    }

    private static bool TryPathLength(Vector3 from, Vector3 to, out float length)
    {
        length = 0f;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)) return false;
        if (path.status != NavMeshPathStatus.PathComplete) return false;

        for (int i = 0; i < path.corners.Length - 1; i++)
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        return true;
    }

    [ContextMenu("Debug/Log status daftar destinasi")]
    private void Debug_LogState()
    {
        RecomputeLabels();
        var sb = new System.Text.StringBuilder(
            $"[DestinationFloorLabel] lantai user={floorVisibility?.CurrentFloorIndex}, " +
            $"label terhitung={_labelOf.Count}\n");
        foreach (var kv in _labelOf)
            sb.AppendLine($"  {kv.Key.gameObject.name,-34} -> '{kv.Value}'");
        Debug.Log(sb.ToString());
    }
}
