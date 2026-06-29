using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Onboarding / Splash Screen controller.
/// - Animates floating dot decorations (opacity pulse)
/// - "Lewati" → skip to AuthGate
/// - "Mulai sekarang" → same as Lewati (no session exists yet)
/// - Auto-navigates after displayDuration only if a saved session exists
///   (mock: PlayerPrefs key "session_user" until real JWT backend is wired)
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SplashScreenController : MonoBehaviour
{
    [SerializeField] private float autoSkipDuration = 1.5f; // fast check, not a long splash

    private UIDocument _doc;
    private Button _btnSkip;
    private Button _btnStart;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _doc.rootVisualElement;

        _btnSkip  = root.Q<Button>("btn-skip");
        _btnStart = root.Q<Button>("btn-start");

        if (_btnSkip  != null) _btnSkip.clicked  += OnSkip;
        if (_btnStart != null) _btnStart.clicked += OnStart;

        // If there's already a saved session, skip onboarding quickly
        if (PlayerPrefs.HasKey("session_user"))
            StartCoroutine(AutoNavigateHome());
    }

    private void OnDisable()
    {
        if (_btnSkip  != null) _btnSkip.clicked  -= OnSkip;
        if (_btnStart != null) _btnStart.clicked -= OnStart;
    }

    private void OnSkip()  => ScreenManager.Instance.ShowAuthGate();
    private void OnStart() => ScreenManager.Instance.ShowAuthGate();

    private IEnumerator AutoNavigateHome()
    {
        yield return new WaitForSeconds(autoSkipDuration);
        ScreenManager.Instance.ShowHome();
    }
}
