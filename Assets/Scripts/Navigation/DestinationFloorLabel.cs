using UnityEngine;

/// <summary>
/// ADR-020 / amandemen 020-A: di daftar Destinations bawaan SDK, POI di lantai LAIN tampil
/// "Unreachable". Itu menyesatkan — POI-nya bisa dicapai, lewat lift; yang tidak bisa cuma
/// menghitungnya sebagai SATU jalur NavMesh kontinu, karena antar-lantai memang sengaja
/// tidak disambung (rute lintas-lantai dipecah, lihat ADR-020).
///
/// Script ini mengganti label itu dengan penunjuk lantai ("Lantai 1"). Bukan kosmetik:
/// karena rutenya tersegmentasi, satu angka jarak kontinu ke lantai lain memang tidak
/// bermakna by design — yang berguna bagi user adalah LANTAI BERAPA. Efek sampingnya,
/// dua POI "Lift" berhenti membingungkan tanpa perlu disembunyikan atau di-rename:
///
///     Lift    6 m          <- lantai user
///     Lift    Lantai 1     <- lantai lain
///
/// SENGAJA TIDAK menyentuh kasus "selantai tapi tetap Unreachable" — itu masalah navmesh
/// asli dan harus tetap terlihat, jangan ditutupi.
///
/// Aditif: package SDK tidak di-fork. ListItemUI.Update() menulis ulang label tiap frame,
/// jadi penimpaan dilakukan di LateUpdate — pola yang sama dengan ToastTranslator.
/// </summary>
public class DestinationFloorLabel : MonoBehaviour
{
    [SerializeField] private FloorVisibilityManager floorVisibility;

    [Tooltip("Seberapa sering memindai ulang item daftar (detik). Item di-spawn/di-destroy " +
             "saat panel dibuka-tutup, tapi tidak sesering per-frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    [SerializeField] private bool logChanges = false;

    private ListItemUI[] _items = System.Array.Empty<ListItemUI>();
    private float _rescanTimer;

    private void Awake()
    {
        if (floorVisibility == null)
            floorVisibility = FindFirstObjectByType<FloorVisibilityManager>();
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

        foreach (var item in _items)
        {
            if (item == null || item.distance == null) continue;

            var poi = item.dataObject as POI;
            if (poi == null) continue;

            var data = poi.GetComponent<POIData>();
            if (data == null) continue;

            // Ragu = jangan klaim apa pun. Kalau clustering belum siap atau POI tak dikenal,
            // biarkan label SDK apa adanya daripada menampilkan lantai yang belum tentu benar.
            if (!floorVisibility.IsOnDifferentFloor(data)) continue;

            string label = data.Floor;
            if (string.IsNullOrEmpty(label)) continue;

            if (item.distance.text != label)
            {
                if (logChanges)
                    Debug.Log($"[DestinationFloorLabel] '{poi.poiName}': '{item.distance.text}' -> '{label}'");
                item.distance.text = label;
            }
        }
    }

    [ContextMenu("Debug/Log status daftar destinasi")]
    private void Debug_LogState()
    {
        var items = FindObjectsByType<ListItemUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var sb = new System.Text.StringBuilder(
            $"[DestinationFloorLabel] lantai user (indeks)={floorVisibility?.CurrentFloorIndex}, item={items.Length}\n");
        foreach (var item in items)
        {
            var poi = item != null ? item.dataObject as POI : null;
            var data = poi != null ? poi.GetComponent<POIData>() : null;
            sb.AppendLine($"  {(poi != null ? poi.poiName : "?"),-24} floor='{data?.Floor ?? "?"}' " +
                          $"bedaLantai={(data != null && floorVisibility != null && floorVisibility.IsOnDifferentFloor(data))} " +
                          $"label='{item?.distance?.text}'");
        }
        Debug.Log(sb.ToString());
    }
}
