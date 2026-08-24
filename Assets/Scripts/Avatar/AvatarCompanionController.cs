using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controller utama untuk Avatar Companion 3D (Tahap 1 MVP - ADR-030).
/// Mengelola siklus hidup avatar: Spawning di depan kamera -> Wave -> Idle (LookAt aktif) -> Pointing -> Despawn Fade-out.
/// </summary>
[DisallowMultipleComponent]
public class AvatarCompanionController : MonoBehaviour
{
    public enum AvatarState
    {
        Hidden,
        Spawning,
        Greeting,
        Idle,
        Pointing,
        Despawning
    }

    [Header("Dependencies")]
    [SerializeField] private Animator animator;
    [SerializeField] private AvatarLookAtController lookAtController;
    [SerializeField] private AvatarSafetyFade safetyFade;
    [Tooltip("GameObject visual anak yang di-enable/disable. JANGAN masukkan GameObject ini sendiri agar controller tetap aktif.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Spawn Settings")]
    [Tooltip("Otomatis spawn saat scene Play dimulai (memudahkan pengujian cepat).")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [Tooltip("Jarak spawn di depan kamera (meter).")]
    [SerializeField] private float spawnDistance = 1.8f;
    [Tooltip("Offset ketinggian dari posisi kamera (meter).")]
    [SerializeField] private float spawnHeightOffset = -0.5f;
    [Tooltip("Durasi animasi wave/menyapa awal sebelum masuk ke Idle (detik).")]
    [SerializeField] private float greetingDuration = 1.5f;
    [Tooltip("Durasi animasi menunjuk sebelum kembali ke Idle (detik).")]
    [SerializeField] private float pointingDuration = 2.0f;
    [Tooltip("Durasi transisi fade out saat despawn (detik).")]
    [SerializeField] private float despawnDuration = 0.5f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string triggerWaveParam = "Wave";
    [SerializeField] private string triggerPointParam = "Point";
    [SerializeField] private string isVisibleParam = "IsVisible";

    [Header("Events")]
    public UnityEvent<AvatarState> onStateChanged;
    public UnityEvent onSpawned;
    public UnityEvent onDismissed;

    private AvatarState _currentState = AvatarState.Hidden;
    private Coroutine _activeRoutine;

    public AvatarState CurrentState => _currentState;
    public bool IsVisible => _currentState != AvatarState.Hidden && _currentState != AvatarState.Despawning;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (lookAtController == null) lookAtController = GetComponentInChildren<AvatarLookAtController>(true);
        if (safetyFade == null) safetyFade = GetComponentInChildren<AvatarSafetyFade>(true);
        
        if (visualRoot == null)
        {
            // Ambil child pertama sebagai visual root jika belum di-assign
            if (transform.childCount > 0)
                visualRoot = transform.GetChild(0).gameObject;
        }

        if (!autoSpawnOnStart)
        {
            SetVisualActive(false);
            SetState(AvatarState.Hidden);
        }
    }

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnCompanion();
        }
    }

    /// <summary>
    /// Memunculkan avatar di depan kamera AR pengguna dan memulai gestur sapaan (Wave).
    /// </summary>
    public void SpawnCompanion()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        SetState(AvatarState.Spawning);

        // Posisikan di depan Camera.main
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            Vector3 camForwardFlat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (camForwardFlat.sqrMagnitude < 0.01f) camForwardFlat = cam.forward;

            Vector3 spawnPos = cam.position + (camForwardFlat * spawnDistance);
            spawnPos.y += spawnHeightOffset;

            transform.position = spawnPos;
            // Menghadap ke arah kamera
            transform.rotation = Quaternion.LookRotation(-camForwardFlat, Vector3.up);
        }

        SetVisualActive(true);

        if (safetyFade != null) safetyFade.SetAlphaInstant(1f);
        if (lookAtController != null) lookAtController.IsLookAtEnabled = true;

        if (animator != null)
        {
            if (HasParameter(isVisibleParam)) animator.SetBool(isVisibleParam, true);
            if (HasParameter(triggerWaveParam)) animator.SetTrigger(triggerWaveParam);
        }

        onSpawned?.Invoke();
        SetState(AvatarState.Greeting);

        // Tunggu durasi greeting/wave
        yield return new WaitForSeconds(greetingDuration);

        SetState(AvatarState.Idle);
        _activeRoutine = null;
    }

    /// <summary>
    /// Memicu gestur menunjuk arah (Pointing) lalu otomatis kembali ke Idle.
    /// </summary>
    public void TriggerPointing()
    {
        if (!IsVisible)
        {
            Debug.LogWarning("[AvatarCompanion] Tidak bisa Pointing saat avatar tidak aktif.");
            return;
        }

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(PointingRoutine());
    }

    private IEnumerator PointingRoutine()
    {
        SetState(AvatarState.Pointing);

        if (animator != null && HasParameter(triggerPointParam))
        {
            animator.SetTrigger(triggerPointParam);
        }

        yield return new WaitForSeconds(pointingDuration);

        SetState(AvatarState.Idle);
        _activeRoutine = null;
    }

    /// <summary>
    /// Menutup avatar dengan animasi pamit dan fade-out.
    /// </summary>
    public void Dismiss()
    {
        if (_currentState == AvatarState.Hidden || _currentState == AvatarState.Despawning) return;

        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(DismissRoutine());
    }

    private IEnumerator DismissRoutine()
    {
        SetState(AvatarState.Despawning);

        if (lookAtController != null) lookAtController.IsLookAtEnabled = false;
        if (animator != null && HasParameter(isVisibleParam)) animator.SetBool(isVisibleParam, false);

        // Fade out visual
        float timer = 0f;
        while (timer < despawnDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / despawnDuration);
            if (safetyFade != null) safetyFade.SetAlphaInstant(alpha);
            yield return null;
        }

        SetVisualActive(false);
        SetState(AvatarState.Hidden);
        onDismissed?.Invoke();
        _activeRoutine = null;
    }

    private void SetVisualActive(bool active)
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(active);
        }
        else
        {
            // Fallback jika tidak ada visualRoot: aktifkan/nonaktifkan renderer anak
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = active;
            }
        }
    }

    private void SetState(AvatarState newState)
    {
        _currentState = newState;
        onStateChanged?.Invoke(_currentState);
        Debug.Log($"[AvatarCompanion] State -> {_currentState}");
    }

    private bool HasParameter(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in animator.parameters)
        {
            if (p.name == paramName) return true;
        }
        return false;
    }
}
