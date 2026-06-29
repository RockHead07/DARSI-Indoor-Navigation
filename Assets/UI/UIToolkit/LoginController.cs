using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Login screen with 4 visual states: Default, Active, Error, Loading.
///
/// Mock auth (no backend yet):
///  - Valid-looking email (contains '@' and '.') + password length >= 6 → enables submit.
///  - On submit → Loading (~1.8s) → success → save session → Home/AR.
///  - To demo the Error state, type password "salah".
/// Swap the mock block in TrySubmit() for a real API call when the backend exists.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoginController : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _emailBox, _passBox, _emailCheck;
    private TextField _email, _pass;
    private Button _submit, _back, _eye, _forgot, _register;
    private Label _heroTitle, _heroSub;

    private bool _passwordHidden = true;
    private Coroutine _loginRoutine;

    private void Awake()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
    }

    private void OnEnable()
    {
        // Reset to a clean Default state every time the screen is shown
        ResetState();

        _emailBox   = _root.Q<VisualElement>("email-box");
        _passBox    = _root.Q<VisualElement>("pass-box");
        _emailCheck = _root.Q<VisualElement>("email-check");
        _email      = _root.Q<TextField>("email-input");
        _pass       = _root.Q<TextField>("pass-input");
        _submit     = _root.Q<Button>("btn-submit");
        _back       = _root.Q<Button>("btn-back");
        _eye        = _root.Q<Button>("btn-eye");
        _forgot     = _root.Q<Button>("btn-forgot");
        _register   = _root.Q<Button>("btn-register");
        _heroTitle  = _root.Q<Label>("hero-title");
        _heroSub    = _root.Q<Label>("hero-sub");

        _email?.RegisterValueChangedCallback(OnAnyFieldChanged);
        _pass?.RegisterValueChangedCallback(OnAnyFieldChanged);

        // focus highlight
        _email?.RegisterCallback<FocusInEvent>(_ => _emailBox.AddToClassList("focused"));
        _email?.RegisterCallback<FocusOutEvent>(_ => _emailBox.RemoveFromClassList("focused"));
        _pass?.RegisterCallback<FocusInEvent>(_ => _passBox.AddToClassList("focused"));
        _pass?.RegisterCallback<FocusOutEvent>(_ => _passBox.RemoveFromClassList("focused"));

        if (_submit   != null) _submit.clicked   += TrySubmit;
        if (_back     != null) _back.clicked      += () => ScreenManager.Instance.ShowAuthGate();
        if (_eye      != null) _eye.clicked        += TogglePassword;
        if (_register != null) _register.clicked   += () => ScreenManager.Instance.ShowRegister();
        if (_forgot   != null) _forgot.clicked     += () => Debug.Log("[Login] Lupa password (belum diimplementasi)");

        UpdateActiveState();
    }

    private void OnDisable()
    {
        if (_loginRoutine != null) { StopCoroutine(_loginRoutine); _loginRoutine = null; }
    }

    // ─── Field change → toggle Active look + email check ───
    private void OnAnyFieldChanged(ChangeEvent<string> _) => UpdateActiveState();

    private void UpdateActiveState()
    {
        bool emailOk = IsEmailValid(_email != null ? _email.value : "");
        bool passOk  = (_pass != null ? _pass.value.Length : 0) >= 6;

        // green check on valid email
        if (_emailCheck != null)
            _emailCheck.EnableInClassList("visible", emailOk);

        // enable submit when both fields look valid
        if (_submit != null)
            _submit.EnableInClassList("enabled", emailOk && passOk);
    }

    // ─── Submit ───
    private void TrySubmit()
    {
        bool emailOk = IsEmailValid(_email != null ? _email.value : "");
        bool passOk  = (_pass != null ? _pass.value.Length : 0) >= 6;
        if (!emailOk || !passOk) return; // ignore taps while disabled

        if (_loginRoutine != null) StopCoroutine(_loginRoutine);
        _loginRoutine = StartCoroutine(LoginRoutine(_email.value, _pass.value));
    }

    private IEnumerator LoginRoutine(string email, string password)
    {
        // ── Enter Loading state ──
        _root.RemoveFromClassList("is-error");
        _root.AddToClassList("is-loading");
        if (_heroTitle != null) _heroTitle.text = "Memverifikasi akun…";
        if (_heroSub   != null) _heroSub.text   = "Mengambil data role dari server";
        if (_submit    != null) { _submit.text = "↻  Memverifikasi…"; _submit.SetEnabled(false); }

        yield return new WaitForSeconds(1.8f);

        // ── Mock result (replace with real API call) ──
        bool success = password != "salah";

        if (success)
        {
            PlayerPrefs.SetString("session_user", email);
            PlayerPrefs.Save();
            Debug.Log($"[Login] Success → {email}");
            _root.RemoveFromClassList("is-loading");
            ScreenManager.Instance.ShowHome();
        }
        else
        {
            // ── Error state ──
            _root.RemoveFromClassList("is-loading");
            _root.AddToClassList("is-error");
            if (_heroTitle != null) _heroTitle.text = "Selamat datang kembali";
            if (_heroSub   != null) _heroSub.text   = "Masuk untuk melanjutkan navigasi";
            if (_submit    != null) { _submit.text = "Coba lagi"; _submit.SetEnabled(true); }
        }
        _loginRoutine = null;
    }

    // ─── Password visibility toggle ───
    private void TogglePassword()
    {
        _passwordHidden = !_passwordHidden;
        if (_pass != null) _pass.isPasswordField = _passwordHidden;
        if (_eye  != null) _eye.EnableInClassList("icon-eye-on", !_passwordHidden);
    }

    // ─── Reset to Default ───
    private void ResetState()
    {
        _root.RemoveFromClassList("is-error");
        _root.RemoveFromClassList("is-loading");
        var submit = _root.Q<Button>("btn-submit");
        if (submit != null) { submit.text = "Masuk"; submit.SetEnabled(true); }
        var heroTitle = _root.Q<Label>("hero-title");
        var heroSub   = _root.Q<Label>("hero-sub");
        if (heroTitle != null) heroTitle.text = "Selamat datang kembali";
        if (heroSub   != null) heroSub.text   = "Masuk untuk melanjutkan navigasi";
    }

    private static bool IsEmailValid(string s)
        => !string.IsNullOrEmpty(s) && s.Contains("@") && s.Contains(".");
}
