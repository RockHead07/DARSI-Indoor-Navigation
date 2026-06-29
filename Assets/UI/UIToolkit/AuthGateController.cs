using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AuthGateController : MonoBehaviour
{
    private UIDocument _doc;
    private Button _btnLogin, _btnRegister, _btnGuest;

    private void Awake() => _doc = GetComponent<UIDocument>();

    private void OnEnable()
    {
        var root = _doc.rootVisualElement;
        if (root == null) return;

        _btnLogin    = root.Q<Button>("btn-login");
        _btnRegister = root.Q<Button>("btn-register");
        _btnGuest    = root.Q<Button>("btn-guest");

        if (_btnLogin    != null) _btnLogin.clicked    += OnLogin;
        if (_btnRegister != null) _btnRegister.clicked += OnRegister;
        if (_btnGuest    != null) _btnGuest.clicked    += OnGuest;
    }

    private void OnDisable()
    {
        if (_btnLogin    != null) _btnLogin.clicked    -= OnLogin;
        if (_btnRegister != null) _btnRegister.clicked -= OnRegister;
        if (_btnGuest    != null) _btnGuest.clicked    -= OnGuest;
    }

    private void OnLogin()    => ScreenManager.Instance.ShowLogin();
    private void OnRegister() => ScreenManager.Instance.ShowRegister();
    private void OnGuest()    => ScreenManager.Instance.ShowAR();
}
