using System.Reflection; // Ditambahkan: untuk validasi method via reflection sebelum SendMessage
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Adapter navigasi sederhana yang menerima POIData dan meneruskan ke sistem navigasi.
/// Gunakan UnityEvent di Inspector untuk wiring ke NavigationController atau sistem nav lain.
/// </summary>
public class NavigationAdapter : MonoBehaviour
{
    [Header("Navigation Controller")]
    [Tooltip("Komponen NavigationController dari MultiSet SDK. Jika diisi, adapter akan mencoba memanggil SetPOIforNavigation(POI) via SendMessage.")]
    [SerializeField] private Component navigationController;

    [Tooltip("Nama method pada NavigationController yang menerima parameter POI.")]
    [SerializeField] private string setPoiMethodName = "SetPOIForNavigation";

    [Tooltip("Jika true, gunakan SendMessage untuk memanggil method navigasi.")]
    [SerializeField] private bool useSendMessage = true;

    [Header("Navigation UI Controller")]
    [Tooltip("Komponen NavigationUIController untuk menampilkan progress slider UI.")]
    [SerializeField] private Component navigationUIController;

    [Tooltip("Nama method pada NavigationUIController yang menerima parameter POI.")]
    [SerializeField] private string startNavigationUIMethodName = "ClickedStartNavigation";

    [Tooltip("Optional: UI panel daftar destinasi. Akan disembunyikan setelah navigasi dimulai.")]
    [SerializeField] private GameObject destinationSelectUI;

    [Header("Events")]
    [Tooltip("Event dipanggil saat navigasi ke POI diminta. Parameter: Transform tujuan.")]
    [SerializeField] private UnityEvent<Transform> onNavigateToTransform;

    [Tooltip("Event dipanggil saat navigasi ke POI diminta. Parameter: Vector3 posisi tujuan.")]
    [SerializeField] private UnityEvent<Vector3> onNavigateToPosition;

    [Tooltip("Event dipanggil saat navigasi ke POI diminta. Parameter: nama POI.")]
    [SerializeField] private UnityEvent<string> onNavigateToName;

    // Event baru: dipanggil ketika navigasi gagal karena komponen tidak ditemukan
    // atau tidak ada handler yang terhubung. UI bisa menampilkan pesan error.
    [Tooltip("Event dipanggil saat navigasi gagal (komponen POI tidak ditemukan atau tidak ada handler navigasi).")]
    [SerializeField] private UnityEvent onNavigationFailed;

    [Header("Debug")]
    [SerializeField] private bool logNavigation = true;

    /// <summary>
    /// Dipanggil oleh VoiceInputHandler.onPoiMatched via Inspector.
    /// Menerima POIData dan meneruskan informasi navigasi ke event yang terhubung.
    /// </summary>
    public void NavigateToPOI(POIData poi)
    {
        if (poi == null)
        {
            Debug.LogWarning("[NavigationAdapter] POIData null, navigasi dibatalkan.");
            return;
        }

        if (logNavigation)
        {
            Debug.Log($"[NavigationAdapter] Navigasi ke: {poi.EffectiveName} " +
                      $"(pos: {poi.transform.position})");
        }

        // Invoke semua event — sistem navigasi bisa mendengarkan salah satu
        onNavigateToTransform?.Invoke(poi.transform);
        onNavigateToPosition?.Invoke(poi.transform.position);
        onNavigateToName?.Invoke(poi.EffectiveName);

        Component poiComponent = poi.GetComponent("POI");
        if (poiComponent == null)
        {
            Debug.LogWarning("[NavigationAdapter] Komponen POI tidak ditemukan pada target. Pastikan POI SDK terpasang di GameObject.");

            // Cek apakah ada handler navigasi lain yang terhubung (event-based).
            // Jika tidak ada komponen POI DAN tidak ada event handler, navigasi benar-benar gagal.
            bool hasEventHandlers = HasAnyNavigationEventHandler();
            if (!hasEventHandlers)
            {
                // Tidak ada handler sama sekali — panggil onNavigationFailed
                Debug.LogError("[NavigationAdapter] Navigasi gagal: komponen POI tidak ditemukan DAN tidak ada event handler yang terhubung.");
                onNavigationFailed?.Invoke();
            }
            return;
        }

        // Validasi method pada NavigationController via reflection sebelum SendMessage.
        // Ini mencegah error silent jika nama method salah atau tidak ada.
        if (navigationController != null && useSendMessage)
        {
            // Gunakan reflection untuk cek apakah method benar-benar ada di komponen target
            if (ValidateMethodExists(navigationController, setPoiMethodName))
            {
                // Method ditemukan — aman untuk memanggil SendMessage
                navigationController.SendMessage(setPoiMethodName, poiComponent, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                // Method tidak ditemukan — log error yang jelas agar developer tahu apa yang salah
                Debug.LogError(
                    $"[NavigationAdapter] Method '{setPoiMethodName}' TIDAK ditemukan pada komponen " +
                    $"'{navigationController.GetType().Name}' di GameObject '{navigationController.gameObject.name}'. " +
                    $"Pastikan NavigationController memiliki method public dengan nama '{setPoiMethodName}' " +
                    $"yang menerima parameter bertipe POI.");
            }
        }

        // Validasi method pada NavigationUIController via reflection sebelum SendMessage
        if (navigationUIController != null && useSendMessage)
        {
            if (ValidateMethodExists(navigationUIController, startNavigationUIMethodName))
            {
                // Method ditemukan — aman untuk memanggil SendMessage
                navigationUIController.SendMessage(startNavigationUIMethodName, poiComponent, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                // Method tidak ditemukan — log error yang jelas
                Debug.LogError(
                    $"[NavigationAdapter] Method '{startNavigationUIMethodName}' TIDAK ditemukan pada komponen " +
                    $"'{navigationUIController.GetType().Name}' di GameObject '{navigationUIController.gameObject.name}'. " +
                    $"Pastikan NavigationUIController memiliki method public dengan nama '{startNavigationUIMethodName}' " +
                    $"yang menerima parameter bertipe POI.");
            }
        }

        // Cek apakah ada handler navigasi yang terhubung (controller ATAU event)
        bool hasAnyHandler = (navigationController != null) || HasAnyNavigationEventHandler();
        if (!hasAnyHandler)
        {
            // Tidak ada yang menangani navigasi sama sekali
            Debug.LogError("[NavigationAdapter] Navigasi gagal: tidak ada NavigationController maupun event handler yang terhubung. Hubungkan minimal salah satu.");
            onNavigationFailed?.Invoke();
        }

        if (destinationSelectUI != null && destinationSelectUI.activeSelf)
        {
            destinationSelectUI.SetActive(false);
        }
    }

    /// <summary>
    /// Validasi apakah suatu method ada pada komponen target menggunakan reflection.
    /// Mencari method public (instance dan static) dengan nama yang diberikan.
    /// </summary>
    /// <param name="component">Komponen yang akan dicek.</param>
    /// <param name="methodName">Nama method yang dicari.</param>
    /// <returns>True jika method ditemukan, false jika tidak.</returns>
    private bool ValidateMethodExists(Component component, string methodName)
    {
        if (component == null || string.IsNullOrEmpty(methodName)) return false;

        // Gunakan reflection untuk mendapatkan method dari tipe komponen.
        // BindingFlags: Public | Instance | FlattenHierarchy agar mencari
        // di class itu sendiri dan parent class-nya.
        MethodInfo method = component.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy
        );

        return method != null;
    }

    /// <summary>
    /// Cek apakah ada event handler navigasi yang terhubung (onNavigateToTransform, Position, atau Name).
    /// Digunakan untuk menentukan apakah navigasi bisa diproses meskipun tanpa NavigationController.
    /// </summary>
    /// <returns>True jika minimal satu event memiliki listener.</returns>
    private bool HasAnyNavigationEventHandler()
    {
        // Cek apakah salah satu event memiliki persistent listener (yang diset via Inspector)
        // GetPersistentEventCount mengembalikan jumlah listener yang terdaftar di Inspector
        bool hasTransform = onNavigateToTransform != null && onNavigateToTransform.GetPersistentEventCount() > 0;
        bool hasPosition = onNavigateToPosition != null && onNavigateToPosition.GetPersistentEventCount() > 0;
        bool hasName = onNavigateToName != null && onNavigateToName.GetPersistentEventCount() > 0;

        return hasTransform || hasPosition || hasName;
    }

    /// <summary>
    /// Menu konteks di Inspector untuk validasi semua referensi pada edit time.
    /// Klik kanan pada komponen NavigationAdapter di Inspector > "Validate Wiring"
    /// untuk mengecek apakah semua field sudah di-assign dengan benar.
    /// </summary>
    [ContextMenu("Validate Wiring")]
    private void ValidateWiring()
    {
        Debug.Log("=== [NavigationAdapter] Validasi Wiring ===");

        // Cek NavigationController
        if (navigationController == null)
        {
            Debug.LogWarning("[NavigationAdapter] ⚠ navigationController belum di-assign. SendMessage untuk navigasi tidak akan berfungsi.");
        }
        else
        {
            // Validasi method pada NavigationController
            if (!ValidateMethodExists(navigationController, setPoiMethodName))
            {
                Debug.LogWarning($"[NavigationAdapter] ⚠ Method '{setPoiMethodName}' tidak ditemukan pada '{navigationController.GetType().Name}'. Periksa nama method.");
            }
            else
            {
                Debug.Log($"[NavigationAdapter] ✓ NavigationController OK — method '{setPoiMethodName}' ditemukan.");
            }
        }

        // Cek NavigationUIController
        if (navigationUIController == null)
        {
            Debug.LogWarning("[NavigationAdapter] ⚠ navigationUIController belum di-assign. UI progress slider tidak akan muncul.");
        }
        else
        {
            if (!ValidateMethodExists(navigationUIController, startNavigationUIMethodName))
            {
                Debug.LogWarning($"[NavigationAdapter] ⚠ Method '{startNavigationUIMethodName}' tidak ditemukan pada '{navigationUIController.GetType().Name}'. Periksa nama method.");
            }
            else
            {
                Debug.Log($"[NavigationAdapter] ✓ NavigationUIController OK — method '{startNavigationUIMethodName}' ditemukan.");
            }
        }

        // Cek event handlers
        int eventCount = 0;
        if (onNavigateToTransform != null && onNavigateToTransform.GetPersistentEventCount() > 0)
        {
            eventCount += onNavigateToTransform.GetPersistentEventCount();
            Debug.Log($"[NavigationAdapter] ✓ onNavigateToTransform: {onNavigateToTransform.GetPersistentEventCount()} listener(s).");
        }
        if (onNavigateToPosition != null && onNavigateToPosition.GetPersistentEventCount() > 0)
        {
            eventCount += onNavigateToPosition.GetPersistentEventCount();
            Debug.Log($"[NavigationAdapter] ✓ onNavigateToPosition: {onNavigateToPosition.GetPersistentEventCount()} listener(s).");
        }
        if (onNavigateToName != null && onNavigateToName.GetPersistentEventCount() > 0)
        {
            eventCount += onNavigateToName.GetPersistentEventCount();
            Debug.Log($"[NavigationAdapter] ✓ onNavigateToName: {onNavigateToName.GetPersistentEventCount()} listener(s).");
        }

        if (eventCount == 0 && navigationController == null)
        {
            Debug.LogError("[NavigationAdapter] ✗ KRITIS: Tidak ada NavigationController maupun event handler yang terhubung! Navigasi tidak akan berfungsi.");
        }

        // Cek destinationSelectUI
        if (destinationSelectUI == null)
        {
            Debug.LogWarning("[NavigationAdapter] ⚠ destinationSelectUI belum di-assign. Panel destinasi tidak akan disembunyikan otomatis.");
        }
        else
        {
            Debug.Log("[NavigationAdapter] ✓ destinationSelectUI OK.");
        }

        // Cek onNavigationFailed event
        if (onNavigationFailed != null && onNavigationFailed.GetPersistentEventCount() > 0)
        {
            Debug.Log($"[NavigationAdapter] ✓ onNavigationFailed: {onNavigationFailed.GetPersistentEventCount()} listener(s).");
        }
        else
        {
            Debug.LogWarning("[NavigationAdapter] ⚠ onNavigationFailed tidak memiliki listener. Error navigasi tidak akan ditampilkan ke UI.");
        }

        Debug.Log("=== [NavigationAdapter] Validasi selesai ===");
    }
}
