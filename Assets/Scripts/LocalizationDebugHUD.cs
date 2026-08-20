using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MultiSet;

/// <summary>
/// HUD admin-only untuk validasi ground-truth akurasi VPS -- bandingkan posisi/jarak
/// yang dihitung MultiSet vs pengukuran meteran asli di lapangan. Lihat
/// docs/superpowers/specs/2026-08-18-localization-ground-truth-hud-design.md.
///
/// Gerbang admin: 5x tap cepat (dalam TapWindowSeconds) di logoTapTarget, status
/// tersimpan di PlayerPrefs. UI dibuat sendiri saat runtime (bukan prefab/scene object)
/// supaya tidak perlu wiring manual selain dua referensi di bawah.
/// </summary>
public class LocalizationDebugHUD : MonoBehaviour
{
    [Tooltip("Kamera AR -- kosongkan untuk pakai Camera.main (pola sama dengan FloorVisibilityManager).")]
    [SerializeField] private Camera arCamera;

    [Tooltip("Tombol/logo yang di-tap 5x cepat buat toggle mode admin.")]
    [SerializeField] private Button logoTapTarget;

    [Tooltip("Kosongkan untuk cari otomatis (FindAnyObjectByType) -- manager localize yang sudah ada di scene.")]
    [SerializeField] private SingleFrameLocalizationManager localizationManager;

    private const string AdminPrefKey = "DARSI_AdminMode";
    private const int TapsToToggle = 5;
    private const float TapWindowSeconds = 20f;

    private int _tapCount;
    private float _firstTapTime;
    private bool _isAdmin;

    private Vector3? _origin;
    private GameObject _panel;
    private TMP_Text _infoText;
    private float? _lastConfidence;

    void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (localizationManager == null) localizationManager = FindAnyObjectByType<SingleFrameLocalizationManager>();
        _isAdmin = PlayerPrefs.GetInt(AdminPrefKey, 0) == 1;
        BuildUI();
        ApplyAdminState();
    }

    void Start()
    {
        if (logoTapTarget != null)
            logoTapTarget.onClick.AddListener(OnLogoTapped);
        else
            Debug.LogWarning("[LocalizationDebugHUD] logoTapTarget belum di-assign -- gerbang 5x-tap tidak jalan.");

        if (localizationManager != null)
            localizationManager.OnLocalizationWithResponse += OnLocalizationResponse;
        else
            Debug.LogWarning("[LocalizationDebugHUD] SingleFrameLocalizationManager tidak ditemukan -- confidence tidak akan tampil.");
    }

    void OnDestroy()
    {
        if (localizationManager != null)
            localizationManager.OnLocalizationWithResponse -= OnLocalizationResponse;
    }

    private void OnLocalizationResponse(LocalizationSuccessResponse response)
    {
        _lastConfidence = response.confidence;
    }

    void Update()
    {
        if (!_isAdmin || arCamera == null) return;
        UpdateInfoText();
    }

    private void OnLogoTapped()
    {
        float now = Time.unscaledTime;
        if (_tapCount == 0 || now - _firstTapTime > TapWindowSeconds)
        {
            _tapCount = 1;
            _firstTapTime = now;
            return;
        }
        _tapCount++;
        if (_tapCount >= TapsToToggle)
        {
            _tapCount = 0;
            _isAdmin = !_isAdmin;
            PlayerPrefs.SetInt(AdminPrefKey, _isAdmin ? 1 : 0);
            PlayerPrefs.Save();
            ApplyAdminState();
        }
    }

    private void ApplyAdminState()
    {
        if (_panel != null) _panel.SetActive(_isAdmin);
    }

    private void UpdateInfoText()
    {
        Vector3 pos = arCamera.transform.position;
        string confLine = "confidence: " + (_lastConfidence.HasValue ? $"{_lastConfidence.Value:F2}" : "(belum ada)");

        if (_origin.HasValue)
        {
            Vector3 d = pos - _origin.Value;
            float groundDist = new Vector2(d.x, d.z).magnitude;
            _infoText.text =
                $"pos(map): x={pos.x:F2} y={pos.y:F2} z={pos.z:F2}\n" +
                $"Δx={d.x:F2}  Δz={d.z:F2}\n" +
                $"jarak dari titik 0: {groundDist:F2} m\n" +
                confLine;
        }
        else
        {
            _infoText.text =
                $"pos(map): x={pos.x:F2} y={pos.y:F2} z={pos.z:F2}\n" +
                "(belum ada titik 0 -- pencet \"Set Titik 0\")\n" +
                confLine;
        }
    }

    private void OnSetOriginClicked()
    {
        if (arCamera == null) return;
        _origin = arCamera.transform.position;
    }

    // ── UI dibuat sendiri saat runtime, minimal, nempel ke Canvas yang sudah ada ──
    private void BuildUI()
    {
        // FindAnyObjectByType<Canvas>() itu ambigu -- scene ini punya ~11 Canvas kecil
        // per-POI (papan tanda) selain Canvas utama, dan bisa kepilih yang salah (nempel
        // ke Canvas POI yang kebetulan nonaktif, jadi HUD toggle tapi gak pernah kelihatan).
        // logoTapTarget sudah pasti nempel ke Canvas utama -- turunkan dari situ.
        Canvas canvas = logoTapTarget != null ? logoTapTarget.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[LocalizationDebugHUD] Tidak ada Canvas di scene, HUD tidak dibuat.");
            return;
        }

        _panel = new GameObject("LocalizationDebugPanel", typeof(RectTransform));
        _panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(20f, -160f);
        panelRt.sizeDelta = new Vector2(520f, 240f);

        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject textGO = new GameObject("InfoText", typeof(RectTransform));
        textGO.transform.SetParent(_panel.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(14f, 54f);
        textRt.offsetMax = new Vector2(-14f, -10f);
        _infoText = textGO.AddComponent<TextMeshProUGUI>();
        _infoText.fontSize = 28f;
        _infoText.color = Color.white;
        _infoText.text = "";

        GameObject btnGO = new GameObject("SetOriginButton", typeof(RectTransform));
        btnGO.transform.SetParent(_panel.transform, false);
        RectTransform btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0f, 0f);
        btnRt.anchorMax = new Vector2(1f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 10f);
        btnRt.sizeDelta = new Vector2(-28f, 40f);
        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f, 1f);
        Button setOriginButton = btnGO.AddComponent<Button>();
        setOriginButton.onClick.AddListener(OnSetOriginClicked);

        GameObject btnTextGO = new GameObject("Label", typeof(RectTransform));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        RectTransform btnTextRt = btnTextGO.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
        TextMeshProUGUI btnLabel = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnLabel.text = "Set Titik 0";
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.fontSize = 22f;
        btnLabel.color = Color.white;

        _panel.SetActive(false);
    }
}
