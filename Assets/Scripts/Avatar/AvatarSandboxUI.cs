using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI pengendali untuk pengujian Sandbox Avatar Companion (Tahap 1 MVP).
/// Menyediakan tombol uji coba Spawn, Point, Dismiss, serta monitor status state machine.
/// </summary>
public class AvatarSandboxUI : MonoBehaviour
{
    [Header("Controller Target")]
    [SerializeField] private AvatarCompanionController companionController;

    [Header("UI Buttons")]
    [SerializeField] private Button btnSpawn;
    [SerializeField] private Button btnPoint;
    [SerializeField] private Button btnDismiss;

    [Header("Status Feedback")]
    [SerializeField] private TMP_Text txtStatus;
    [SerializeField] private TMP_Text txtDistance;

    private void Awake()
    {
        if (companionController == null)
        {
            companionController = FindFirstObjectByType<AvatarCompanionController>();
        }

        if (btnSpawn != null) btnSpawn.onClick.AddListener(OnSpawnClicked);
        if (btnPoint != null) btnPoint.onClick.AddListener(OnPointClicked);
        if (btnDismiss != null) btnDismiss.onClick.AddListener(OnDismissClicked);

        if (companionController != null)
        {
            companionController.onStateChanged.AddListener(UpdateStateDisplay);
        }
    }

    private void Start()
    {
        if (companionController != null)
        {
            UpdateStateDisplay(companionController.CurrentState);
        }
    }

    private void Update()
    {
        if (txtDistance != null && companionController != null && Camera.main != null && companionController.IsVisible)
        {
            float dist = Vector3.Distance(companionController.transform.position, Camera.main.transform.position);
            txtDistance.text = $"Jarak Kamera: {dist:F2} m {(dist < 0.8f ? "<color=red>(Safety Fade Active)</color>" : "<color=green>(Aman)</color>")}";
        }
        else if (txtDistance != null)
        {
            txtDistance.text = "Avatar Non-Aktif";
        }
    }

    private void OnSpawnClicked()
    {
        if (companionController != null) companionController.SpawnCompanion();
    }

    private void OnPointClicked()
    {
        if (companionController != null) companionController.TriggerPointing();
    }

    private void OnDismissClicked()
    {
        if (companionController != null) companionController.Dismiss();
    }

    private void UpdateStateDisplay(AvatarCompanionController.AvatarState state)
    {
        if (txtStatus != null)
        {
            txtStatus.text = $"State: <b>{state}</b>";
        }

        // Atur interaktivitas tombol
        if (btnSpawn != null) btnSpawn.interactable = (state == AvatarCompanionController.AvatarState.Hidden);
        if (btnPoint != null) btnPoint.interactable = (state == AvatarCompanionController.AvatarState.Idle);
        if (btnDismiss != null) btnDismiss.interactable = (state != AvatarCompanionController.AvatarState.Hidden && state != AvatarCompanionController.AvatarState.Despawning);
    }
}
