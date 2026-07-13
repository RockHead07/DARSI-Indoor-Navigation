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
        public string poiName; // display name for the WebView banner (poiId is a GUID now)
    }

    private LaunchPayload _pendingPayload;
    private bool _isLocalized;

    /// <summary>True setelah MultiSet localize sukses. Posisi/jarak baru valid setelah ini
    /// (ADR-007) — dipakai NavBoundaryNotifier (ADR-019) untuk gating deteksi out-of-bounds.</summary>
    public bool IsLocalized => _isLocalized;

    // Tracks state needed to build arSessionClosed's payload per docs/INTEGRATION.md
    // ("poiId dari tujuan aktif + arrived dari flag internal") since NavigationAdapter
    // itself has no arrival/active-destination concept.
    private string _activePoiId;
    private string _activePoiName;
    private bool _arrived;

    /// <summary>Resolved POI currently being navigated to, or null outside mode:navigate.
    /// Read by FloorVisibilityManager (ADR-018) so the active target stays visible even on
    /// a floor different from the user's current one.</summary>
    public POIData ActiveNavTarget { get; private set; }

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

        // Cold-launch path: the Flutter host (MainActivity.kt) starts UnityPlayerGameActivity
        // with the launchAR payload as an intent extra. Read it here instead of relying on
        // UnitySendMessage, which would race the not-yet-loaded player on a fresh activity.
        ReadLaunchIntent();
    }

    private void ReadLaunchIntent()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = jc.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = activity.Call<AndroidJavaObject>("getIntent");
            string json = intent.Call<string>("getStringExtra", "darsiPayload");
            if (!string.IsNullOrEmpty(json))
                ReceiveLaunchPayload(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UaaLEntryPoint] Failed to read launch intent: {e.Message}");
        }
#endif
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
                // Fase 2 (blocked on ROADMAP.md T0.8) — friend positions need Photon + real
                // identity + 2 co-located devices. Until then don't strand the user in a blank
                // AR view (the WebView already fires launchAR for this): tell them explicitly.
                ToastManager.Instance?.ShowAlert("Fitur Cari Teman di AR belum tersedia.");
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

        var poi = ResolvePoi(payload);
        if (poi == null)
        {
            ToastManager.Instance?.ShowAlert($"Lokasi \"{payload.poiName ?? payload.poiId}\" tidak ditemukan.");
            Debug.LogWarning($"[UaaLEntryPoint] poiId '{payload.poiId}' did not resolve to a POIData");
            return;
        }

        _activePoiId = payload.poiId;
        _activePoiName = poi.EffectiveName;
        _arrived = false;
        ActiveNavTarget = poi;
        navigationAdapter.NavigateToPOI(poi);
    }

    /// <summary>Resolve an incoming launch payload to a POIData. Stable GUID (POIData.poiId)
    /// wins so a display-name rename never breaks navigation; falls back to fuzzy name match
    /// for legacy POIs without a synced GUID.</summary>
    private POIData ResolvePoi(LaunchPayload payload)
    {
        if (poiManager == null) return null;

        if (!string.IsNullOrEmpty(payload.poiId))
        {
            foreach (var poi in poiManager.GetAllPOIs())
                if (poi.poiId == payload.poiId) return poi;
        }

        return poiManager.FindBestMatchWithContext(payload.poiName ?? payload.poiId, null);
    }

    private void RouteFreeExplore()
    {
        _activePoiId = null;
        _activePoiName = null;
        _arrived = false;
        ActiveNavTarget = null;

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

    /// <summary>Signalled when the MultiSet SDK reports POI arrival. Detection lives in
    /// MultiSetSDK.dll, so its "You arrived at the destination!" toast (observed via
    /// ToastTranslator) is the one point arrival surfaces in editable code — hooking it
    /// reuses the SDK's own decision instead of running a second distance detector that
    /// could disagree. Guarded: fires the bridge event only for an active mode:navigate
    /// target, once. Without this, _arrived stays false forever and arSessionClosed always
    /// reports arrived:false (the WebView arrival banner never shows).</summary>
    public void ReportArrivalAtActiveTarget()
    {
        if (ActiveNavTarget == null || _arrived) return;
        NotifyNavigationArrived(_activePoiId);
    }

    /// <summary>Call when the user backs out of / closes the AR session.</summary>
    public void CloseArSession()
    {
        var payload = new ArSessionClosedPayload { arrived = _arrived, poiId = _activePoiId, poiName = _activePoiName };
        SendEventToFlutter("arSessionClosed", JsonUtility.ToJson(payload));
    }

    private void SendEventToFlutter(string eventName, string jsonPayload)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Route to the host's UnityBridge (Kotlin static), which hops to the platform
        // thread and calls the "darsi/unity" MethodChannel (T4.5). Class name is the
        // host app package — only exists when embedded in My-eRSIy-CopyCat.
        try
        {
            using var bridge = new AndroidJavaClass("com.rsislam.surabaya.rs_islam_app.UnityBridge");
            bridge.CallStatic("send", eventName, jsonPayload);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UaaLEntryPoint] SendEventToFlutter('{eventName}') failed: {e.Message}");
        }
#else
        Debug.Log($"[UaaLEntryPoint] (editor stub) -> Flutter event '{eventName}': {jsonPayload}");
#endif
    }

    // AR is tearing down (user backed out of the activity). Report the session result so
    // the WebView can resume (T4.5). DarsiUnityActivity.onUnityPlayerUnloaded() then
    // finish()es back to the Flutter host. The main-thread post in UnityBridge survives
    // this teardown, so the event still reaches Dart after MainActivity resumes.
    private void OnApplicationQuit()
    {
        CloseArSession();
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
