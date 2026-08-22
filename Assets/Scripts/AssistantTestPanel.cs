using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel VALIDASI untuk asisten RAG. BUKAN UX final.
///
/// Tujuannya satu: membuktikan rantai Unity -> backend -> retrieval -> LLM benar
/// benar jalan, dan melihat mutu jawabannya dengan mata sendiri. Input teks
/// dipilih karena paling murah, bukan karena bagus. Mengetik di layar AR sambil
/// berjalan itu buruk, jadi UX yang sesungguhnya (suara) diputuskan SETELAH
/// backend terbukti, dan caranya lewat mengekstrak SpeechRecognizer jadi komponen
/// bersama, bukan menyalin kodenya dari VoiceInputHandler.
///
/// Digerbangi tombol yang sama dengan HUD admin (5x tap logo) supaya tidak
/// terlihat pengguna biasa. UI dibangun runtime, mengikuti pola
/// LocalizationDebugHUD, jadi tidak ada YAML scene yang disuntik.
/// </summary>
public class AssistantTestPanel : MonoBehaviour
{
    [Tooltip("Kosongkan untuk cari otomatis.")]
    [SerializeField] private AssistantClient client;

    [Tooltip("Tombol/logo yang di-tap 5x cepat buat memunculkan panel ini. " +
             "Pakai target yang sama dengan LocalizationDebugHUD.")]
    [SerializeField] private Button logoTapTarget;

    private const string AdminPrefKey = "DARSI_AdminMode";
    private const int TapsToToggle = 5;
    private const float TapWindowSeconds = 20f;

    private int _tapCount;
    private float _firstTapTime;
    private bool _isAdmin;

    private GameObject _panel;
    private TMP_InputField _input;
    private TMP_Text _output;
    private Button _askButton;
    private Button _navButton;
    private AssistantAnswer _lastAnswer;

    void Awake()
    {
        if (client == null) client = FindAnyObjectByType<AssistantClient>();
        _isAdmin = PlayerPrefs.GetInt(AdminPrefKey, 0) == 1;
        BuildUI();
        if (_panel != null) _panel.SetActive(_isAdmin);
    }

    void Start()
    {
        if (logoTapTarget != null)
            logoTapTarget.onClick.AddListener(OnLogoTapped);
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
            if (_panel != null) _panel.SetActive(_isAdmin);
        }
    }

    private void OnAskClicked()
    {
        if (client == null)
        {
            _output.text = "AssistantClient tidak ditemukan di scene.";
            return;
        }
        if (client.IsProcessing) return;

        string q = _input != null ? _input.text : "";
        if (string.IsNullOrWhiteSpace(q)) return;

        _lastAnswer = null;
        if (_navButton != null) _navButton.gameObject.SetActive(false);
        _output.text = "Memproses...";
        _askButton.interactable = false;

        StartCoroutine(client.Ask(q, OnAnswer));
    }

    private void OnAnswer(AssistantAnswer answer)
    {
        _askButton.interactable = true;
        _lastAnswer = answer;

        if (answer == null)
        {
            _output.text = "Gagal menghubungi asisten. Cek backend jalan dan URL-nya benar.";
            return;
        }

        // ADR-026 mewajibkan penanda ini selama contains_simulated_data true. Nama
        // dokter dan jam praktek fiktif yang tampil tanpa penanda bisa menyesatkan.
        string penanda = answer.contains_simulated_data
            ? "<color=#FFB300>[DATA SIMULASI]</color>\n"
            : "";

        _output.text = penanda + answer.answer;

        // Tombol rute cuma muncul kalau jawabannya memang menyangkut satu lokasi.
        // poi_id diturunkan backend dari metadata chunk, tidak pernah dikarang LLM.
        if (_navButton != null && !string.IsNullOrEmpty(answer.poi_id))
        {
            _navButton.gameObject.SetActive(true);
            var label = _navButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"Mulai Rute: {answer.poi_name ?? "lokasi ini"}";
        }
    }

    private void OnNavClicked()
    {
        if (client == null || _lastAnswer == null) return;
        if (client.StartNavigationFrom(_lastAnswer))
            _panel.SetActive(false);
    }

    // ── UI dibangun runtime, nempel ke Canvas utama ──
    private void BuildUI()
    {
        // Sama seperti LocalizationDebugHUD: FindAnyObjectByType<Canvas>() itu ambigu
        // karena scene ini punya ~11 Canvas kecil per-POI. Turunkan dari logoTapTarget.
        Canvas canvas = logoTapTarget != null ? logoTapTarget.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[AssistantTestPanel] Tidak ada Canvas di scene, panel tidak dibuat.");
            return;
        }

        _panel = new GameObject("AssistantTestPanel", typeof(RectTransform));
        _panel.transform.SetParent(canvas.transform, false);
        var rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 320f);
        rt.sizeDelta = new Vector2(900f, 480f);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.82f);

        CreateText("Judul", _panel.transform, new Vector2(0f, -12f), new Vector2(-24f, 44f),
                   new Vector2(0f, 1f), new Vector2(1f, 1f),
                   "Asisten (panel uji)", 26f, TextAlignmentOptions.Center);

        _output = CreateText("Output", _panel.transform, Vector2.zero, Vector2.zero,
                             Vector2.zero, Vector2.one, "Ketik pertanyaan lalu tekan Tanya.",
                             24f, TextAlignmentOptions.TopLeft);
        var ort = _output.rectTransform;
        ort.offsetMin = new Vector2(16f, 150f);
        ort.offsetMax = new Vector2(-16f, -60f);
        _output.textWrappingMode = TextWrappingModes.Normal;

        _input = CreateInput(_panel.transform);
        _askButton = CreateButton("Tanya", _panel.transform, new Vector2(-16f, 16f),
                                  new Vector2(200f, 60f), stretch: false, OnAskClicked);
        _navButton = CreateButton("Mulai Rute", _panel.transform, new Vector2(0f, 84f),
                                  new Vector2(-32f, 56f), stretch: true, OnNavClicked);
        _navButton.GetComponent<Image>().color = new Color(0.02f, 0.31f, 0.19f, 1f);
        _navButton.gameObject.SetActive(false);

        _panel.SetActive(false);
    }

    private TMP_Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size,
                                Vector2 aMin, Vector2 aMax, string text, float fontSize,
                                TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = align;
        t.richText = true;
        return t;
    }

    private TMP_InputField CreateInput(Transform parent)
    {
        var go = new GameObject("Input", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(-108f, 16f);
        rt.sizeDelta = new Vector2(-248f, 60f);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);

        var textArea = new GameObject("Text", typeof(RectTransform));
        textArea.transform.SetParent(go.transform, false);
        var trt = textArea.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 4f);
        trt.offsetMax = new Vector2(-12f, -4f);
        var txt = textArea.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 24f;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        var field = go.AddComponent<TMP_InputField>();
        field.textComponent = txt;
        field.textViewport = trt;
        field.lineType = TMP_InputField.LineType.SingleLine;
        return field;
    }

    /// <param name="stretch">true = melebar mengikuti lebar panel (sizeDelta.x jadi
    /// margin kiri-kanan). false = lebar tetap, ditempel ke sudut kanan bawah.</param>
    private Button CreateButton(string label, Transform parent, Vector2 pos, Vector2 size,
                                bool stretch, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
        }
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.9f, 1f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        CreateText("Label", go.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one,
                   label, 24f, TextAlignmentOptions.Center);
        return btn;
    }
}
