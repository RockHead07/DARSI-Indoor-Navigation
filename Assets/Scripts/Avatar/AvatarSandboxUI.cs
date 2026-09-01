using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI pengendali untuk pengujian Sandbox Avatar Companion & Lip-Sync (Tahap 1 & Fase 2).
/// Menyediakan kontrol Spawn, Point, Dismiss, pemutaran audio uji (AIUEO & Sapaan), serta monitor diagnostik fonem/volume.
/// </summary>
public class AvatarSandboxUI : MonoBehaviour
{
    [Header("Controller Target")]
    [SerializeField] private AvatarCompanionController companionController;
    [SerializeField] private AvatarSpeechLipSync lipSyncDriver;
    [SerializeField] private AvatarAudioClient audioClient;

    [Header("UI Buttons - Gerakan")]
    [SerializeField] private Button btnSpawn;
    [SerializeField] private Button btnPoint;
    [SerializeField] private Button btnDismiss;

    [Header("UI Buttons - Suara & Lip-Sync")]
    [SerializeField] private Button btnPlayAIUEO;
    [SerializeField] private Button btnPlayGreeting;
    [SerializeField] private Button btnTestBackendTTS;
    [SerializeField] private Button btnStopVoice;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip clipAIUEO;
    [SerializeField] private AudioClip clipGreeting;

    [Header("Status Feedback")]
    [SerializeField] private TMP_Text txtStatus;
    [SerializeField] private TMP_Text txtDistance;
    [SerializeField] private TMP_Text txtPhoneme;

    private void Awake()
    {
        if (companionController == null)
        {
            companionController = FindFirstObjectByType<AvatarCompanionController>();
        }

        if (lipSyncDriver == null)
        {
            lipSyncDriver = FindFirstObjectByType<AvatarSpeechLipSync>();
        }

        if (audioClient == null)
        {
            audioClient = FindFirstObjectByType<AvatarAudioClient>();
        }

        if (btnSpawn != null) btnSpawn.onClick.AddListener(OnSpawnClicked);
        if (btnPoint != null) btnPoint.onClick.AddListener(OnPointClicked);
        if (btnDismiss != null) btnDismiss.onClick.AddListener(OnDismissClicked);

        if (btnPlayAIUEO != null) btnPlayAIUEO.onClick.AddListener(OnPlayAIUEOClicked);
        if (btnPlayGreeting != null) btnPlayGreeting.onClick.AddListener(OnPlayGreetingClicked);
        if (btnTestBackendTTS != null) btnTestBackendTTS.onClick.AddListener(OnTestBackendTTSClicked);
        if (btnStopVoice != null) btnStopVoice.onClick.AddListener(OnStopVoiceClicked);

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
        if (companionController != null && Camera.main != null && companionController.IsVisible)
        {
            float dist = Vector3.Distance(companionController.transform.position, Camera.main.transform.position);
            if (txtDistance != null)
            {
                txtDistance.text = $"Jarak Kamera: {dist:F2} m {(dist < 0.8f ? "<color=red>(Safety Fade Active)</color>" : "<color=green>(Aman)</color>")}";
            }
        }
        else if (txtDistance != null)
        {
            txtDistance.text = "Avatar Non-Aktif";
        }

        // Monitor diagnostik lip-sync
        if (txtPhoneme != null && lipSyncDriver != null)
        {
            if (lipSyncDriver.IsSpeaking)
            {
                txtPhoneme.text = $"Bicara: <color=#00FFAA>Aktif</color> | Fonem: <b>{lipSyncDriver.ActivePhoneme}</b> | Vol: {lipSyncDriver.CurrentVolume:F2}";
            }
            else
            {
                txtPhoneme.text = "Bicara: <color=#888888>Diam</color> | Fonem: - | Vol: 0.00";
            }
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

    private void OnPlayAIUEOClicked()
    {
        if (lipSyncDriver != null && clipAIUEO != null)
        {
            lipSyncDriver.PlayAudio(clipAIUEO);
        }
    }

    private void OnPlayGreetingClicked()
    {
        if (lipSyncDriver != null && clipGreeting != null)
        {
            lipSyncDriver.PlayAudio(clipGreeting);
        }
    }

    private void OnTestBackendTTSClicked()
    {
        if (audioClient != null)
        {
            StartCoroutine(audioClient.SpeakText("Poli Anak berada di Lantai 2. Dokter spesialis anak praktek hingga pukul 14.00. Saya siapkan rutenya ya!"));
        }
    }

    private void OnStopVoiceClicked()
    {
        if (lipSyncDriver != null)
        {
            lipSyncDriver.StopAudio();
        }
        if (audioClient != null)
        {
            audioClient.StopSpeaking();
        }
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
