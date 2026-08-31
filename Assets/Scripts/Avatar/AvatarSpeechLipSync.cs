using System.Collections.Generic;
using UnityEngine;
using VRM;

#if ULIPSYNC_SUPPORT || true
using uLipSync;
#endif

/// <summary>
/// Driver Lip-Sync untuk Avatar 3D VRM (Fase 2 - ADR-033 / ADR-037).
/// Memetakan output analisis audio (uLipSync MFCC Burst atau Fallback RMS/Formant)
/// ke 5 preset vokal VRMBlendShapeProxy: A, I, U, E, O.
/// 
/// Alasan arsitektur:
/// 1. Menggunakan uLipSync (Burst-accelerated MFCC) sebagai estimasi fonem primer tanpa beban GC.
/// 2. Menyediakan fallback prosedural otomatis jika uLipSync tidak terpasang/non-aktif.
/// 3. Perhitungan blending dan smoothing dilakukan di LateUpdate agar tidak bertabrakan dengan Animator.
/// </summary>
[DisallowMultipleComponent]
public class AvatarSpeechLipSync : MonoBehaviour
{
    [Header("Komponen Target")]
    [Tooltip("Komponen VRMBlendShapeProxy milik avatar VRM 0.x. Jika kosong, dicari otomatis.")]
    [SerializeField] private VRMBlendShapeProxy blendShapeProxy;

    [Tooltip("AudioSource sumber suara ucapan avatar.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Komponen uLipSync penganalisis MFCC audio. Jika kosong, dicari otomatis di GameObject ini atau AudioSource.")]
    [SerializeField] private uLipSync.uLipSync lipSync;

    [Header("Parameter Responsivitas")]
    [Tooltip("Waktu redaman (smooth damp) perubahan bentuk mulut (detik).")]
    [Range(0.01f, 0.2f)] [SerializeField] private float smoothness = 0.06f;

    [Tooltip("Bobot maksimal pembukaan viseme bibir (0.0 - 1.0).")]
    [Range(0.1f, 1.5f)] [SerializeField] private float maxWeight = 1.0f;

    [Tooltip("Ambang batas volume minimal untuk memicu gerakan bibir (mencegah desis/derau mikrofon).")]
    [Range(0.0f, 0.2f)] [SerializeField] private float minVolumeThreshold = 0.02f;

    [Tooltip("Penguatan volume untuk meningkatkan amplitudo bukaan mulut.")]
    [Range(0.5f, 5.0f)] [SerializeField] private float volumeGain = 1.5f;

    // Nilai target viseme hasil analisis
    private float _targetA, _targetI, _targetU, _targetE, _targetO;

    // Nilai bobot aktif ter-smooth
    private float _currentA, _currentI, _currentU, _currentE, _currentO;

    // Kecepatan transisi untuk Mathf.SmoothDamp
    private float _velA, _velI, _velU, _velE, _velO;

    // Status diagnostik
    private float _currentVolume;
    private string _activePhoneme = "-";
    private bool _hasLipSyncUpdate = false;

    // Buffer FFT fallback (zero GC allocation)
    private readonly float[] _fallbackSamples = new float[512];

    private static readonly BlendShapeKey KeyA = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
    private static readonly BlendShapeKey KeyI = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
    private static readonly BlendShapeKey KeyU = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
    private static readonly BlendShapeKey KeyE = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
    private static readonly BlendShapeKey KeyO = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);

    public VRMBlendShapeProxy BlendShapeProxy
    {
        get
        {
            if (blendShapeProxy == null) ResolveComponents();
            return blendShapeProxy;
        }
        set => blendShapeProxy = value;
    }

    public AudioSource AudioSource
    {
        get
        {
            if (audioSource == null) ResolveComponents();
            return audioSource;
        }
        set => audioSource = value;
    }

    public uLipSync.uLipSync LipSyncComponent
    {
        get
        {
            if (lipSync == null) ResolveComponents();
            return lipSync;
        }
        set => RegisterLipSync(value);
    }

    public bool IsSpeaking => audioSource != null && audioSource.isPlaying;
    public float CurrentVolume => _currentVolume;
    public string ActivePhoneme => _activePhoneme;

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        ResolveComponents();
        if (lipSync != null)
        {
            lipSync.onLipSyncUpdate.AddListener(OnLipSyncUpdated);
        }
    }

    private void OnDisable()
    {
        if (lipSync != null)
        {
            lipSync.onLipSyncUpdate.RemoveListener(OnLipSyncUpdated);
        }
        ResetVisemes();
    }

    public void ResolveComponents()
    {
        if (blendShapeProxy == null)
        {
            blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>(true);
            if (blendShapeProxy == null)
            {
                blendShapeProxy = GetComponentInParent<VRMBlendShapeProxy>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = GetComponentInChildren<AudioSource>(true);
            }
        }

        if (lipSync == null)
        {
            lipSync = GetComponent<uLipSync.uLipSync>();
            if (lipSync == null && audioSource != null)
            {
                lipSync = audioSource.GetComponent<uLipSync.uLipSync>();
            }
        }
    }

    private void RegisterLipSync(uLipSync.uLipSync newLipSync)
    {
        if (lipSync != null)
        {
            lipSync.onLipSyncUpdate.RemoveListener(OnLipSyncUpdated);
        }

        lipSync = newLipSync;

        if (lipSync != null && isActiveAndEnabled)
        {
            lipSync.onLipSyncUpdate.AddListener(OnLipSyncUpdated);
        }
    }

    /// <summary>
    /// Callback saat uLipSync selesai memproses frame audio via Job System & Burst.
    /// </summary>
    public void OnLipSyncUpdated(LipSyncInfo info)
    {
        _hasLipSyncUpdate = true;
        _currentVolume = info.volume;
        _activePhoneme = string.IsNullOrEmpty(info.phoneme) ? "-" : info.phoneme;

        if (_currentVolume < minVolumeThreshold)
        {
            _targetA = 0f;
            _targetI = 0f;
            _targetU = 0f;
            _targetE = 0f;
            _targetO = 0f;
            return;
        }

        float scaledVol = Mathf.Clamp01(_currentVolume * volumeGain);

        // Jika rasio fonem tersedia (uLipSync standard), terapkan per vokal
        if (info.phonemeRatios != null && info.phonemeRatios.Count > 0)
        {
            _targetA = GetRatio(info.phonemeRatios, "A") * scaledVol * maxWeight;
            _targetI = GetRatio(info.phonemeRatios, "I") * scaledVol * maxWeight;
            _targetU = GetRatio(info.phonemeRatios, "U") * scaledVol * maxWeight;
            _targetE = GetRatio(info.phonemeRatios, "E") * scaledVol * maxWeight;
            _targetO = GetRatio(info.phonemeRatios, "O") * scaledVol * maxWeight;
        }
        else
        {
            // Jika hanya fonem dominan tunggal
            _targetA = (info.phoneme == "A") ? scaledVol * maxWeight : 0f;
            _targetI = (info.phoneme == "I") ? scaledVol * maxWeight : 0f;
            _targetU = (info.phoneme == "U") ? scaledVol * maxWeight : 0f;
            _targetE = (info.phoneme == "E") ? scaledVol * maxWeight : 0f;
            _targetO = (info.phoneme == "O") ? scaledVol * maxWeight : 0f;
        }
    }

    private float GetRatio(Dictionary<string, float> ratios, string vowel)
    {
        return ratios.TryGetValue(vowel, out float val) ? val : 0f;
    }

    private void Update()
    {
        // Jika uLipSync tidak ada atau tidak aktif, gunakan analisis RMS/amplitudo lokal sebagai fallback
        if (lipSync == null || !lipSync.isActiveAndEnabled || !_hasLipSyncUpdate)
        {
            UpdateFallbackLipSync();
        }

        _hasLipSyncUpdate = false;
    }

    /// <summary>
    /// Fallback penganalisis audio sederhana jika uLipSync tidak berjalan.
    /// Mengekstrak RMS volume dan memetakan bukaan mulut ritmis vokal A.
    /// </summary>
    private void UpdateFallbackLipSync()
    {
        if (audioSource == null || !audioSource.isPlaying)
        {
            _targetA = 0f;
            _targetI = 0f;
            _targetU = 0f;
            _targetE = 0f;
            _targetO = 0f;
            _currentVolume = 0f;
            _activePhoneme = "-";
            return;
        }

        audioSource.GetOutputData(_fallbackSamples, 0);

        float sum = 0f;
        for (int i = 0; i < _fallbackSamples.Length; i++)
        {
            sum += _fallbackSamples[i] * _fallbackSamples[i];
        }

        float rms = Mathf.Sqrt(sum / _fallbackSamples.Length);
        _currentVolume = rms;

        if (rms >= minVolumeThreshold)
        {
            float opening = Mathf.Clamp01((rms - minVolumeThreshold) * volumeGain * 3.0f) * maxWeight;
            _targetA = opening * 0.7f;
            _targetO = opening * 0.3f;
            _targetI = 0f;
            _targetU = 0f;
            _targetE = 0f;
            _activePhoneme = "A (Fallback)";
        }
        else
        {
            _targetA = 0f;
            _targetI = 0f;
            _targetU = 0f;
            _targetE = 0f;
            _targetO = 0f;
            _activePhoneme = "-";
        }
    }

    private void LateUpdate()
    {
        if (blendShapeProxy == null) return;

        // Terapkan SmoothDamp per frame agar gerakan bibir luwes dan tidak bergetar tajam
        float dt = Time.deltaTime;
        _currentA = Mathf.SmoothDamp(_currentA, _targetA, ref _velA, smoothness, Mathf.Infinity, dt);
        _currentI = Mathf.SmoothDamp(_currentI, _targetI, ref _velI, smoothness, Mathf.Infinity, dt);
        _currentU = Mathf.SmoothDamp(_currentU, _targetU, ref _velU, smoothness, Mathf.Infinity, dt);
        _currentE = Mathf.SmoothDamp(_currentE, _targetE, ref _velE, smoothness, Mathf.Infinity, dt);
        _currentO = Mathf.SmoothDamp(_currentO, _targetO, ref _velO, smoothness, Mathf.Infinity, dt);

        // Langsung set nilai ke VRMBlendShapeProxy
        blendShapeProxy.ImmediatelySetValue(KeyA, _currentA);
        blendShapeProxy.ImmediatelySetValue(KeyI, _currentI);
        blendShapeProxy.ImmediatelySetValue(KeyU, _currentU);
        blendShapeProxy.ImmediatelySetValue(KeyE, _currentE);
        blendShapeProxy.ImmediatelySetValue(KeyO, _currentO);
    }

    /// <summary>
    /// Memutar AudioClip pada AudioSource lokal dan memulai proses sinkronisasi bibir.
    /// </summary>
    public void PlayAudio(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Menghentikan audio seketika dan meredam viseme bibir ke pose diam.
    /// </summary>
    public void StopAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        _targetA = 0f;
        _targetI = 0f;
        _targetU = 0f;
        _targetE = 0f;
        _targetO = 0f;
    }

    /// <summary>
    /// Mereset seluruh nilai blend shape bibir ke 0 seketika (misal saat avatar despawn).
    /// </summary>
    public void ResetVisemes()
    {
        _targetA = _targetI = _targetU = _targetE = _targetO = 0f;
        _currentA = _currentI = _currentU = _currentE = _currentO = 0f;
        _velA = _velI = _velU = _velE = _velO = 0f;

        if (blendShapeProxy != null)
        {
            blendShapeProxy.ImmediatelySetValue(KeyA, 0f);
            blendShapeProxy.ImmediatelySetValue(KeyI, 0f);
            blendShapeProxy.ImmediatelySetValue(KeyU, 0f);
            blendShapeProxy.ImmediatelySetValue(KeyE, 0f);
            blendShapeProxy.ImmediatelySetValue(KeyO, 0f);
        }
    }
}
