using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VoiceUIController : MonoBehaviour
{
    public enum VoiceState { Idle, Listening, Processing, Error }

    [Header("Config (optional, overrides defaults)")]
    [SerializeField] private VoiceUIConfig config;

    [Header("UI References")]
    [SerializeField] private GameObject voicePanel;
    [SerializeField] private bool startHidden = false;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text transcriptText;
    [SerializeField] private RectTransform waveformContainer;
    [SerializeField] private Image statusPillBg;

    [Header("Mic Pulse (optional)")]
    [SerializeField] private Image micButtonImage;
    [SerializeField] private bool enableMicPulse = true;

    [Header("Waveform Defaults")]
    [SerializeField] private int barCount = 8;
    [SerializeField] private float barWidth = 6f;
    [SerializeField] private float barSpacing = 4f;
    [SerializeField] private float barMinHeight = 4f;
    [SerializeField] private float barMaxHeight = 30f;
    [SerializeField] private Color barColor = new Color(0.4f, 0.85f, 1f, 1f);

    [Header("Color Defaults")]
    [SerializeField] private Color idleColor = new Color(0.3f, 0.3f, 0.35f, 1f);
    [SerializeField] private Color listeningColor = new Color(0.2f, 0.75f, 0.4f, 1f);
    [SerializeField] private Color processingColor = new Color(0.95f, 0.7f, 0.2f, 1f);
    [SerializeField] private Color errorColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    [Header("Transcript Defaults")]
    [SerializeField] private float transcriptAutoHideSecondsDefault = 4f;

    private Image[] waveformBars;
    private VoiceState currentState = VoiceState.Idle;
    private Coroutine errorResetCoroutine;
    private Coroutine transcriptHideCoroutine;
    private Vector3 micOriginalScale;
    private Color micOriginalColor;
    private CanvasGroup transcriptCanvasGroup;

    // Resolved values (config or defaults)
    private int _barCount;
    private float _barWidth;
    private float _barSpacing;
    private float _barMinHeight;
    private float _barMaxHeight;
    private Color _barColor;
    private Color _idleColor;
    private Color _listeningColor;
    private Color _processingColor;
    private Color _errorColor;
    private float _pulseSpeed;
    private float _pulseScaleMin;
    private float _pulseScaleMax;
    private float _pulseAlphaMin;
    private float _transcriptAutoHide;

    void Awake()
    {
        ResolveConfig();
        if (voicePanel != null && startHidden)
        {
            voicePanel.SetActive(false);
        }
        if (micButtonImage != null)
        {
            micOriginalScale = micButtonImage.rectTransform.localScale;
            micOriginalColor = micButtonImage.color;
        }
        GenerateWaveformBars();
        SetState(VoiceState.Idle);
    }

    void Update()
    {
        if (currentState == VoiceState.Listening)
        {
            AnimateWaveform();
            AnimateMicPulse();
        }
        else
        {
            FlattenWaveform();
            ResetMicPulse();
        }
    }

    // ── Public API ──

    public void SetListening(bool isListening)
    {
        if (isListening)
            SetState(VoiceState.Listening);
        else if (currentState == VoiceState.Listening)
            SetState(VoiceState.Idle);
    }

    public void SetProcessing(bool isProcessing)
    {
        if (isProcessing)
            SetState(VoiceState.Processing);
        else if (currentState == VoiceState.Processing)
            SetState(VoiceState.Idle);
    }

    public void SetError(string message)
    {
        SetState(VoiceState.Error);
        if (statusText != null)
            statusText.text = message ?? "Error";
        if (errorResetCoroutine != null)
            StopCoroutine(errorResetCoroutine);
        errorResetCoroutine = StartCoroutine(ResetAfterDelay(2.5f));
    }

    public void SetTranscript(string text)
    {
        if (transcriptText == null) return;
        transcriptText.text = text ?? "";
        ShowTranscript(true);
        if (_transcriptAutoHide > 0f)
        {
            if (transcriptHideCoroutine != null)
                StopCoroutine(transcriptHideCoroutine);
            transcriptHideCoroutine = StartCoroutine(AutoHideTranscript(_transcriptAutoHide));
        }
    }

    public void ShowPanel()
    {
        if (voicePanel != null)
            voicePanel.SetActive(true);
    }

    public void HidePanel()
    {
        if (voicePanel != null)
            voicePanel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (voicePanel != null)
            voicePanel.SetActive(!voicePanel.activeSelf);
    }

    // ── Config ──

    private void ResolveConfig()
    {
        if (config != null)
        {
            _barCount = config.barCount;
            _barWidth = config.barWidth;
            _barSpacing = config.barSpacing;
            _barMinHeight = config.barMinHeight;
            _barMaxHeight = config.barMaxHeight;
            _barColor = config.barColor;
            _idleColor = config.idleColor;
            _listeningColor = config.listeningColor;
            _processingColor = config.processingColor;
            _errorColor = config.errorColor;
            _pulseSpeed = config.pulseSpeed;
            _pulseScaleMin = config.pulseScaleMin;
            _pulseScaleMax = config.pulseScaleMax;
            _pulseAlphaMin = config.pulseAlphaMin;
            _transcriptAutoHide = config.transcriptAutoHideSeconds;
        }
        else
        {
            _barCount = barCount;
            _barWidth = barWidth;
            _barSpacing = barSpacing;
            _barMinHeight = barMinHeight;
            _barMaxHeight = barMaxHeight;
            _barColor = barColor;
            _idleColor = idleColor;
            _listeningColor = listeningColor;
            _processingColor = processingColor;
            _errorColor = errorColor;
            _pulseSpeed = 3f;
            _pulseScaleMin = 0.9f;
            _pulseScaleMax = 1.15f;
            _pulseAlphaMin = 0.6f;
            _transcriptAutoHide = transcriptAutoHideSecondsDefault;
        }
    }

    // ── State ──

    private void SetState(VoiceState state)
    {
        currentState = state;
        UpdateStatusUI();
    }

    private void UpdateStatusUI()
    {
        string label;
        Color pillColor;
        switch (currentState)
        {
            case VoiceState.Listening:
                label = "Listening...";
                pillColor = _listeningColor;
                break;
            case VoiceState.Processing:
                label = "Processing...";
                pillColor = _processingColor;
                break;
            case VoiceState.Error:
                label = "Error";
                pillColor = _errorColor;
                break;
            default:
                label = "Idle";
                pillColor = _idleColor;
                break;
        }
        if (statusText != null)
            statusText.text = label;
        if (statusPillBg != null)
            statusPillBg.color = pillColor;
    }

    private IEnumerator ResetAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (currentState == VoiceState.Error)
            SetState(VoiceState.Idle);
        errorResetCoroutine = null;
    }

    // ── Transcript Auto-Hide ──

    private CanvasGroup GetTranscriptCanvasGroup()
    {
        if (transcriptCanvasGroup == null && transcriptText != null)
        {
            transcriptCanvasGroup = transcriptText.GetComponent<CanvasGroup>();
            if (transcriptCanvasGroup == null)
                transcriptCanvasGroup = transcriptText.gameObject.AddComponent<CanvasGroup>();
        }
        return transcriptCanvasGroup;
    }

    private void ShowTranscript(bool visible)
    {
        CanvasGroup cg = GetTranscriptCanvasGroup();
        if (cg != null) cg.alpha = visible ? 1f : 0f;
    }

    private IEnumerator AutoHideTranscript(float delay)
    {
        yield return new WaitForSeconds(delay);
        CanvasGroup cg = GetTranscriptCanvasGroup();
        if (cg == null) yield break;
        float elapsed = 0f;
        float fadeDuration = 0.4f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = 0f;
        transcriptHideCoroutine = null;
    }

    // ── Mic Pulse ──

    private void AnimateMicPulse()
    {
        if (!enableMicPulse || micButtonImage == null) return;
        float t = Time.time * _pulseSpeed;
        float s = Mathf.Lerp(_pulseScaleMin, _pulseScaleMax, Mathf.Sin(t) * 0.5f + 0.5f);
        micButtonImage.rectTransform.localScale = micOriginalScale * s;
        Color c = micOriginalColor;
        c.a = Mathf.Lerp(_pulseAlphaMin, 1f, Mathf.Sin(t) * 0.5f + 0.5f);
        micButtonImage.color = c;
    }

    private void ResetMicPulse()
    {
        if (!enableMicPulse || micButtonImage == null) return;
        micButtonImage.rectTransform.localScale = Vector3.Lerp(
            micButtonImage.rectTransform.localScale, micOriginalScale, Time.deltaTime * 8f);
        Color c = micButtonImage.color;
        c.a = Mathf.Lerp(c.a, micOriginalColor.a, Time.deltaTime * 8f);
        micButtonImage.color = c;
    }

    // ── Waveform ──

    private void GenerateWaveformBars()
    {
        if (waveformContainer == null) return;

        waveformBars = new Image[_barCount];
        float totalWidth = _barCount * _barWidth + (_barCount - 1) * _barSpacing;
        float startX = -totalWidth * 0.5f + _barWidth * 0.5f;

        for (int i = 0; i < _barCount; i++)
        {
            GameObject barGO = new GameObject("Bar_" + i, typeof(RectTransform), typeof(Image));
            barGO.transform.SetParent(waveformContainer, false);

            RectTransform rt = barGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_barWidth, _barMinHeight);
            rt.anchoredPosition = new Vector2(startX + i * (_barWidth + _barSpacing), 0f);

            Image img = barGO.GetComponent<Image>();
            img.color = _barColor;
            waveformBars[i] = img;
        }
    }

    private void AnimateWaveform()
    {
        if (waveformBars == null) return;
        float t = Time.time;
        for (int i = 0; i < waveformBars.Length; i++)
        {
            float sin = Mathf.Sin(t * 5f + i * 0.8f) * 0.5f + 0.5f;
            float noise = Mathf.PerlinNoise(t * 3f + i * 1.3f, 0f);
            float h = Mathf.Lerp(_barMinHeight, _barMaxHeight, sin * 0.6f + noise * 0.4f);
            RectTransform rt = waveformBars[i].rectTransform;
            rt.sizeDelta = new Vector2(_barWidth, h);
        }
    }

    private void FlattenWaveform()
    {
        if (waveformBars == null) return;
        for (int i = 0; i < waveformBars.Length; i++)
        {
            RectTransform rt = waveformBars[i].rectTransform;
            Vector2 sz = rt.sizeDelta;
            sz.y = Mathf.Lerp(sz.y, _barMinHeight, Time.deltaTime * 8f);
            rt.sizeDelta = sz;
        }
    }
}
