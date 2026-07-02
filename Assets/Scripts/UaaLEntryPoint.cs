using UnityEngine;
using MultiSet;

/// <summary>
/// Single entry point for data coming from the Flutter host (MyRSIy) via UnitySendMessage.
/// Contract: docs/INTEGRATION.md. Do not let any other script receive Flutter payloads directly.
/// </summary>
[DisallowMultipleComponent]
public class UaaLEntryPoint : MonoBehaviour
{
    public static UaaLEntryPoint Instance { get; private set; }

    [SerializeField] private POIManager poiManager;
    [SerializeField] private NavigationAdapter navigationAdapter;
    [SerializeField] private NavigationUIController navigationUIController;

    [System.Serializable]
    private class LaunchPayload
    {
        public string action;
        public string mode; // navigate | freeExplore | findFriend
        public string poiId;
        public string poiName;
        public string floor;
        public string building;
        public string connectionId;
    }

    [System.Serializable]
    private class LocalizationSuccessPayload
    {
        public string building;
        public string floor;
    }

    [System.Serializable]
    private class NavigationArrivedPayload
    {
        public string poiId;
    }

    [System.Serializable]
    private class ArSessionClosedPayload
    {
        public bool arrived;
        public string poiId;
    }

    private LaunchPayload _pendingPayload;
    private bool _isLocalized;

    // Tracks state needed to build arSessionClosed's payload per docs/INTEGRATION.md
    // ("poiId dari tujuan aktif + arrived dari flag internal") since NavigationAdapter
    // itself has no arrival/active-destination concept.
    private string _activePoiId;
    private bool _arrived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (poiManager == null)
            poiManager = FindFirstObjectByType<POIManager>();
        if (navigationAdapter == null)
            navigationAdapter = FindFirstObjectByType<NavigationAdapter>();
        if (navigationUIController == null)
            navigationUIController = FindFirstObjectByType<NavigationUIController>();
    }

    private void Start()
    {
        // AR Canvas is active immediately on launch (ADR-003) — no splash/login gate before this.
        SendEventToFlutter("arSessionReady", "{}");
    }

    /// <summary>Called by Flutter via UnitySendMessage(gameObjectName, "ReceiveLaunchPayload", json).</summary>
    public void ReceiveLaunchPayload(string json)
    {
        LaunchPayload payload;
        try
        {
            payload = JsonUtility.FromJson<LaunchPayload>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UaaLEntryPoint] Invalid launch payload JSON ({e.Message}): {json}");
            return;
        }

        if (payload == null || string.IsNullOrEmpty(payload.action))
        {
            Debug.LogError($"[UaaLEntryPoint] Launch payload missing 'action': {json}");
            return;
        }

        if (_isLocalized)
            ApplyPayload(payload);
        else
            _pendingPayload = payload; // flushed from OnLocalizationSuccess once localize succeeds (ADR-007)
    }

    /// <summary>
    /// Wire as a persistent listener on the MultiSet LocalizationSuccess UnityEvent in the scene
    /// (same GameObject/component that already calls PhotonManager.OnLocalizationSuccess).
    /// </summary>
    public void OnLocalizationSuccess()
    {
        _isLocalized = true;

        var payload = new LocalizationSuccessPayload
        {
            building = _pendingPayload?.building,
            floor = _pendingPayload?.floor,
        };
        SendEventToFlutter("localizationSuccess", JsonUtility.ToJson(payload));

        if (_pendingPayload != null)
        {
            ApplyPayload(_pendingPayload);
            _pendingPayload = null;
        }
    }

    private void ApplyPayload(LaunchPayload payload)
    {
        switch (payload.mode)
        {
            case "navigate":
                RouteNavigate(payload);
                break;
            case "freeExplore":
                RouteFreeExplore();
                break;
            case "findFriend":
                // Fase 2 (blocked on ROADMAP.md T0.8) — not implemented yet.
                Debug.Log($"[UaaLEntryPoint] mode:findFriend not implemented yet, connectionId={payload.connectionId}");
                break;
            default:
                Debug.LogError($"[UaaLEntryPoint] Unknown mode: {payload.mode}");
                break;
        }
    }

    private void RouteNavigate(LaunchPayload payload)
    {
        if (string.IsNullOrEmpty(payload.poiId))
        {
            Debug.LogError("[UaaLEntryPoint] mode:navigate requires poiId");
            return;
        }

        var poi = poiManager != null ? poiManager.FindBestMatchWithContext(payload.poiId, null) : null;
        if (poi == null)
        {
            ToastManager.Instance?.ShowAlert($"Lokasi \"{payload.poiName ?? payload.poiId}\" tidak ditemukan.");
            Debug.LogWarning($"[UaaLEntryPoint] poiId '{payload.poiId}' did not resolve to a POIData");
            return;
        }

        _activePoiId = payload.poiId;
        _arrived = false;
        navigationAdapter.NavigateToPOI(poi);
    }

    private void RouteFreeExplore()
    {
        _activePoiId = null;
        _arrived = false;

        if (navigationUIController == null)
        {
            Debug.LogError("[UaaLEntryPoint] mode:freeExplore — NavigationUIController not found in scene");
            return;
        }

        navigationUIController.ToggleDestinationSelectUI();
    }

    /// <summary>Call once the active navigation's destination is actually reached.</summary>
    public void NotifyNavigationArrived(string poiId)
    {
        _arrived = true;
        var payload = new NavigationArrivedPayload { poiId = poiId };
        SendEventToFlutter("navigationArrived", JsonUtility.ToJson(payload));
    }

    /// <summary>Call when the user backs out of / closes the AR session.</summary>
    public void CloseArSession()
    {
        var payload = new ArSessionClosedPayload { arrived = _arrived, poiId = _activePoiId };
        SendEventToFlutter("arSessionClosed", JsonUtility.ToJson(payload));
    }

    private void SendEventToFlutter(string eventName, string jsonPayload)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // NOTE: "onUnityMessage" is a placeholder native method name — must match whatever
        // host Activity method My-eRSIy-CopyCat implements to receive Unity callbacks (ROADMAP.md T4.5).
        using var jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = jc.GetStatic<AndroidJavaObject>("currentActivity");
        activity.Call("onUnityMessage", eventName, jsonPayload);
#else
        Debug.Log($"[UaaLEntryPoint] (editor stub) -> Flutter event '{eventName}': {jsonPayload}");
#endif
    }

    // --- Debug harness (ROADMAP.md T1.7) — right-click the component header to test without Flutter ---

    [ContextMenu("Debug/Simulate launchAR mode=navigate (Perpustakaan)")]
    private void Debug_SimulateNavigate()
    {
        ReceiveLaunchPayload("{\"action\":\"launchAR\",\"mode\":\"navigate\",\"poiId\":\"Perpustakaan\",\"poiName\":\"Perpustakaan\"}");
    }

    [ContextMenu("Debug/Simulate launchAR mode=navigate (invalid poiId)")]
    private void Debug_SimulateNavigateInvalid()
    {
        ReceiveLaunchPayload("{\"action\":\"launchAR\",\"mode\":\"navigate\",\"poiId\":\"NotARealPOI\"}");
    }

    [ContextMenu("Debug/Simulate launchAR mode=freeExplore")]
    private void Debug_SimulateFreeExplore()
    {
        ReceiveLaunchPayload("{\"action\":\"launchAR\",\"mode\":\"freeExplore\"}");
    }

    [ContextMenu("Debug/Simulate localizationSuccess")]
    private void Debug_SimulateLocalizationSuccess()
    {
        OnLocalizationSuccess();
    }
}
