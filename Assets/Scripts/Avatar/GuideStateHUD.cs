using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD diagnostik untuk menguji AIAvatarGuideController di sandbox.
/// Menampilkan state pemandu, kecepatan, jarak ke pengguna, jarak ke waypoint,
/// dan timeScale yang bisa diubah lewat tombol T.
///
/// Pola OnGUI dan New Input System mengikuti SimpleSandboxFreeCam.cs.
/// Script ini BUKAN di folder Editor/ -- wajib jalan di Play Mode.
/// </summary>
public class GuideStateHUD : MonoBehaviour
{
    [Tooltip("Kosongkan untuk mencari otomatis di scene.")]
    [SerializeField] private AIAvatarGuideController guide;
    [Tooltip("Kosongkan untuk mencari otomatis di scene.")]
    [SerializeField] private ShowPath showPath;

    // Siklus timeScale: 1.0 -> 0.5 -> 0.25 -> kembali ke 1.0
    private static readonly float[] TimeScaleSteps = { 1.0f, 0.5f, 0.25f };
    private int _timeScaleIndex;

    private void Awake()
    {
        if (guide == null) guide = FindFirstObjectByType<AIAvatarGuideController>();
        if (showPath == null) showPath = FindFirstObjectByType<ShowPath>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.tKey.wasPressedThisFrame)
        {
            _timeScaleIndex = (_timeScaleIndex + 1) % TimeScaleSteps.Length;
            Time.timeScale = TimeScaleSteps[_timeScaleIndex];
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            TriggerRoute();
        }
    }

    /// <summary>
    /// Menyiapkan rute ShowPath dari RouteStart ke RouteEnd dan memulai memimpin avatar.
    /// Dipanggil saat tombol R ditekan atau dipanggil via probe pengujian.
    /// </summary>
    public void TriggerRoute()
    {
        if (showPath == null) showPath = FindFirstObjectByType<ShowPath>();
        if (showPath == null)
        {
            Debug.LogWarning("[GuideStateHUD] ShowPath tidak ditemukan di scene.");
            return;
        }

        var startObj = GameObject.Find("RouteMarkers/RouteStart") ?? GameObject.Find("RouteStart");
        var endObj = GameObject.Find("RouteMarkers/RouteEnd") ?? GameObject.Find("RouteEnd");
        if (startObj == null || endObj == null)
        {
            Debug.LogWarning("[GuideStateHUD] RouteMarkers (RouteStart/RouteEnd) tidak ditemukan di scene.");
            return;
        }

        showPath.SetPositionFrom(startObj.transform);
        showPath.SetPositionTo(endObj.transform);

        if (guide != null)
        {
            guide.StartLeading();
            Debug.Log("[GuideStateHUD] Route dipasang: ShowPath terhubung ke RouteStart dan RouteEnd, guide.StartLeading() dipanggil.");
        }
        else
        {
            Debug.LogWarning("[GuideStateHUD] AIAvatarGuideController tidak ditemukan.");
        }
    }

    private void OnGUI()
    {
        // Kolom kanan atas, tidak bertabrakan dengan OnGUI SimpleSandboxFreeCam (kiri atas).
        float boxW = 340f;
        float boxH = 155f;
        float x = Screen.width - boxW - 20f;
        float y = 20f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, boxW, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string stateText = guide != null ? guide.CurrentState.ToString() : "---";
        string leadingText = guide != null ? guide.IsLeading.ToString() : "---";
        string speedText = guide != null ? $"{guide.DiagCurrentSpeed:F2}" : "---";

        // Jarak horizontal (Y diabaikan) dari avatar ke Camera.main
        string distUserText = "---";
        string distWpText = "---";
        if (guide != null && Camera.main != null)
        {
            Vector3 avatarPos = guide.transform.position;
            Vector3 userPos = Camera.main.transform.position;
            Vector3 waypointPos = guide.DiagWaypoint;

            float hDistUser = Vector2.Distance(
                new Vector2(avatarPos.x, avatarPos.z),
                new Vector2(userPos.x, userPos.z));
            distUserText = $"{hDistUser:F2}";

            float hDistWp = Vector2.Distance(
                new Vector2(avatarPos.x, avatarPos.z),
                new Vector2(waypointPos.x, waypointPos.z));
            distWpText = $"{hDistWp:F2}";
        }

        string label =
            "<b><size=14>Guide State HUD</size></b>\n" +
            $"State: <b>{stateText}</b>\n" +
            $"IsLeading: <b>{leadingText}</b>\n" +
            $"Speed: <b>{speedText}</b> m/s\n" +
            $"Dist User: <b>{distUserText}</b> m\n" +
            $"Dist Waypoint: <b>{distWpText}</b> m\n" +
            $"TimeScale: <b>{Time.timeScale:F2}x</b>  <size=11>(T = cycle, R = route)</size>";

        GUI.Label(new Rect(x + 8f, y + 4f, boxW - 16f, boxH - 8f), label);
    }
}
