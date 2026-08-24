using UnityEngine;

/// <summary>
/// Protokol Keselamatan Spasial Rumah Sakit (ADR-030 / AI-AVATAR-ASSISTANT.md §4).
/// Mengatur transparansi (alpha fade-out) avatar secara otomatis saat kamera berada terlalu dekat (< minSafeDistance).
/// Menjamin pandangan koridor fisik rumah sakit tidak tertutup saat pasien/pengguna berjalan mendekat.
/// </summary>
[DisallowMultipleComponent]
public class AvatarSafetyFade : MonoBehaviour
{
    [Header("Distance Thresholds (Meters)")]
    [Tooltip("Jarak di mana avatar mulai memudar (fade-out).")]
    [SerializeField] private float fadeStartDistance = 0.9f;
    [Tooltip("Jarak di mana avatar menjadi sepenuhnya transparan / disembunyikan.")]
    [SerializeField] private float fadeEndDistance = 0.5f;

    [Header("Fade Smoothing")]
    [Tooltip("Kecepatan transisi alpha.")]
    [SerializeField] private float fadeSpeed = 8.0f;

    [Header("Renderers to Fade")]
    [Tooltip("Daftar renderer yang akan dipudarkan. Jika kosong, otomatis mengambil semua Renderer di child.")]
    [SerializeField] private Renderer[] targetRenderers;

    private Transform _cameraTransform;
    private float _currentAlpha = 1.0f;
    private MaterialPropertyBlock _propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public float CurrentAlpha => _currentAlpha;
    public bool IsFadedOut => _currentAlpha <= 0.05f;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        CacheRenderers();
    }

    public void CacheRenderers()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (_cameraTransform == null)
        {
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
            else return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            CacheRenderers();
            if (targetRenderers == null || targetRenderers.Length == 0) return;
        }

        float distance = Vector3.Distance(transform.position, _cameraTransform.position);

        // Hitung target alpha berdasarkan jarak ke kamera
        float targetAlpha = 1.0f;
        if (distance <= fadeEndDistance)
        {
            targetAlpha = 0.0f;
        }
        else if (distance < fadeStartDistance)
        {
            targetAlpha = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, distance);
        }

        // Interpolasi alpha halus
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        ApplyAlpha(_currentAlpha);
    }

    /// <summary>
    /// Menerapkan nilai alpha ke seluruh target renderer.
    /// </summary>
    private void ApplyAlpha(float alpha)
    {
        if (targetRenderers == null) return;

        bool shouldRender = alpha > 0.02f;

        foreach (var rend in targetRenderers)
        {
            if (rend == null) continue;

            // Jika sangat dekat (alpha ~ 0), sembunyikan renderer sepenuhnya untuk keselamatan
            if (rend.enabled != shouldRender)
            {
                rend.enabled = shouldRender;
            }

            if (!shouldRender) continue;

            rend.GetPropertyBlock(_propBlock);
            
            if (rend.sharedMaterial != null)
            {
                Color c = rend.sharedMaterial.HasProperty(BaseColorId) ? rend.sharedMaterial.GetColor(BaseColorId) : 
                          rend.sharedMaterial.HasProperty(ColorId) ? rend.sharedMaterial.GetColor(ColorId) : Color.white;
                c.a = alpha;
                _propBlock.SetColor(BaseColorId, c);
                _propBlock.SetColor(ColorId, c);
            }

            rend.SetPropertyBlock(_propBlock);
        }
    }

    /// <summary>
    /// Memaksa set alpha ke nilai tertentu secara instan (misal saat spawn/despawn).
    /// </summary>
    public void SetAlphaInstant(float alpha)
    {
        _currentAlpha = Mathf.Clamp01(alpha);
        ApplyAlpha(_currentAlpha);
    }
}
