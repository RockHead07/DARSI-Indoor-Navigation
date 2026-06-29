using UnityEngine;

[CreateAssetMenu(fileName = "VoiceUIConfig", menuName = "Voice/UI Config")]
public class VoiceUIConfig : ScriptableObject
{
    [Header("State Colors")]
    public Color idleColor = new Color(0.3f, 0.3f, 0.35f, 1f);
    public Color listeningColor = new Color(0.2f, 0.75f, 0.4f, 1f);
    public Color processingColor = new Color(0.95f, 0.7f, 0.2f, 1f);
    public Color errorColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    [Header("Waveform Bars")]
    public int barCount = 8;
    public float barWidth = 6f;
    public float barSpacing = 4f;
    public float barMinHeight = 4f;
    public float barMaxHeight = 30f;
    public Color barColor = new Color(0.4f, 0.85f, 1f, 1f);

    [Header("Mic Pulse")]
    public float pulseSpeed = 3f;
    [Range(0.8f, 1.3f)] public float pulseScaleMin = 0.9f;
    [Range(1f, 1.5f)] public float pulseScaleMax = 1.15f;
    [Range(0.3f, 1f)] public float pulseAlphaMin = 0.6f;

    [Header("Transcript")]
    [Tooltip("Auto-hide transcript after N seconds. 0 = never hide.")]
    public float transcriptAutoHideSeconds = 4f;
}
