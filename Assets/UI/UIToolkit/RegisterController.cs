using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Register screen — states: Default · Active · Error.
///
/// Mock (no backend yet):
///  - Submit enabled when name filled, email valid (has '@' and '.'), password >= 8.
///  - Email "andi@student.ac.id" is treated as already-registered → Error state.
///  - Otherwise success → save session + chosen role → Home.
/// Role selector: Mahasiswa (default) / Pengunjung. Dosen/Staff is admin-only.
/// Swap the mock block in RegisterRoutine() for a real API call later.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RegisterController : MonoBehaviour
{
    private const string TakenEmail = "andi@student.ac.id";

    private VisualElement _root;
    private VisualElement _nameBox, _emailBox, _passBox, _emailCheck;
    private VisualElement _strengthWrap, _strengthBar, _seg1, _seg2, _seg3;
    private Label _strengthLabel;
    private TextField _name, _email, _pass;
    private Button _submit, _back, _eye, _login, _roleStudent, _roleGuest;

    private bool _passwordHidden = true;
    private bool _guestSelected;          // false = Mahasiswa (default)
    private Coroutine _routine;

    private void Awake() => _root = GetComponent<UIDocument>().rootVisualElement;

    private void OnEnable()
    {
        ResetState();

        _nameBox      = _root.Q<VisualElement>("name-box");
        _emailBox     = _root.Q<VisualElement>("email-box");
        _passBox      = _root.Q<VisualElement>("pass-box");
        _emailCheck   = _root.Q<VisualElement>("email-check");
        _strengthWrap = _root.Q<VisualElement>("strength-wrap");
        _strengthBar  = _root.Q<VisualElement>("strength-bar");
        _seg1         = _root.Q<VisualElement>("seg-1");
        _seg2         = _root.Q<VisualElement>("seg-2");
        _seg3         = _root.Q<VisualElement>("seg-3");
        _strengthLabel= _root.Q<Label>("strength-label");

        _name  = _root.Q<TextField>("name-input");
        _email = _root.Q<TextField>("email-input");
        _pass  = _root.Q<TextField>("pass-input");

        _submit      = _root.Q<Button>("btn-submit");
        _back        = _root.Q<Button>("btn-back");
        _eye         = _root.Q<Button>("btn-eye");
        _login       = _root.Q<Button>("btn-login");
        _roleStudent = _root.Q<Button>("role-student");
        _roleGuest   = _root.Q<Button>("role-guest");

        _name?.RegisterValueChangedCallback(_ => Refresh());
        _email?.RegisterValueChangedCallback(_ => Refresh());
        _pass?.RegisterValueChangedCallback(_ => Refresh());

        _name?.RegisterCallback<FocusInEvent>(_  => _nameBox.AddToClassList("focused"));
        _name?.RegisterCallback<FocusOutEvent>(_ => _nameBox.RemoveFromClassList("focused"));
        _email?.RegisterCallback<FocusInEvent>(_  => _emailBox.AddToClassList("focused"));
        _email?.RegisterCallback<FocusOutEvent>(_ => _emailBox.RemoveFromClassList("focused"));
        _pass?.RegisterCallback<FocusInEvent>(_  => _passBox.AddToClassList("focused"));
        _pass?.RegisterCallback<FocusOutEvent>(_ => _passBox.RemoveFromClassList("focused"));

        if (_submit      != null) _submit.clicked      += TrySubmit;
        if (_back        != null) _back.clicked        += () => ScreenManager.Instance.ShowAuthGate();
        if (_login       != null) _login.clicked       += () => ScreenManager.Instance.ShowLogin();
        if (_eye         != null) _eye.clicked         += TogglePassword;
        if (_roleStudent != null) _roleStudent.clicked += () => SelectRole(false);
        if (_roleGuest   != null) _roleGuest.clicked   += () => SelectRole(true);

        Refresh();
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    // ─── Role selection ───
    private void SelectRole(bool guest)
    {
        _guestSelected = guest;
        _roleStudent?.EnableInClassList("selected", !guest);
        _roleGuest?.EnableInClassList("selected", guest);
    }

    // ─── Field change ───
    private void Refresh()
    {
        bool emailOk = IsEmailValid(_email != null ? _email.value : "");
        bool nameOk  = !string.IsNullOrWhiteSpace(_name != null ? _name.value : "");
        int  passLen = _pass != null ? _pass.value.Length : 0;
        bool passOk  = passLen >= 8;

        _emailCheck?.EnableInClassList("visible", emailOk);
        UpdateStrength(_pass != null ? _pass.value : "");

        _submit?.EnableInClassList("enabled", nameOk && emailOk && passOk);
    }

    // ─── Password strength meter ───
    private void UpdateStrength(string pw)
    {
        bool show = pw.Length > 0;
        _strengthWrap?.EnableInClassList("visible", show);
        if (!show) return;

        int score = ScorePassword(pw);          // 1..3
        _seg1?.EnableInClassList("on", score >= 1);
        _seg2?.EnableInClassList("on", score >= 2);
        _seg3?.EnableInClassList("on", score >= 3);

        string level = score >= 3 ? "strong" : score == 2 ? "medium" : "weak";
        SetClassExclusive(_strengthBar, level, "weak", "medium", "strong");
        SetClassExclusive(_strengthWrap, "lvl-" + level, "lvl-weak", "lvl-medium", "lvl-strong");
        if (_strengthLabel != null)
            _strengthLabel.text = score >= 3 ? "Kuat" : score == 2 ? "Sedang" : "Lemah";
    }

    private static int ScorePassword(string pw)
    {
        int score = 0;
        if (pw.Length >= 8) score++;
        bool hasDigit = false, hasLetter = false, hasOther = false;
        foreach (char c in pw)
        {
            if (char.IsDigit(c)) hasDigit = true;
            else if (char.IsLetter(c)) hasLetter = true;
            else hasOther = true;
        }
        if (hasDigit && hasLetter) score++;
        if (hasOther || (pw.Length >= 12 && hasDigit && hasLetter)) score++;
        return Mathf.Clamp(score, 1, 3);
    }

    // ─── Submit ───
    private void TrySubmit()
    {
        bool emailOk = IsEmailValid(_email != null ? _email.value : "");
        bool nameOk  = !string.IsNullOrWhiteSpace(_name != null ? _name.value : "");
        bool passOk  = (_pass != null ? _pass.value.Length : 0) >= 8;
        if (!nameOk || !emailOk || !passOk) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RegisterRoutine(_email.value));
    }

    private IEnumerator RegisterRoutine(string email)
    {
        _root.RemoveFromClassList("is-error");
        if (_submit != null) { _submit.text = "Membuat akun…"; _submit.SetEnabled(false); }

        yield return new WaitForSeconds(1.2f);

        // ── Mock result (replace with real API call) ──
        bool taken = email.Trim().ToLower() == TakenEmail;

        if (!taken)
        {
            PlayerPrefs.SetString("session_user", email);
            PlayerPrefs.SetString("session_role", _guestSelected ? "tamu" : "mahasiswa");
            PlayerPrefs.Save();
            Debug.Log($"[Register] Success → {email} ({(_guestSelected ? "tamu" : "mahasiswa")})");
            ScreenManager.Instance.ShowHome();
        }
        else
        {
            _root.AddToClassList("is-error");
            if (_submit != null) { _submit.text = "Coba lagi"; _submit.SetEnabled(true); }
        }
        _routine = null;
    }

    // ─── Password visibility ───
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
        var submit = _root.Q<Button>("btn-submit");
        if (submit != null) { submit.text = "Buat akun"; submit.SetEnabled(true); }
    }

    private static void SetClassExclusive(VisualElement el, string keep, params string[] all)
    {
        if (el == null) return;
        foreach (var c in all) el.RemoveFromClassList(c);
        el.AddToClassList(keep);
    }

    private static bool IsEmailValid(string s)
        => !string.IsNullOrEmpty(s) && s.Contains("@") && s.Contains(".");
}
