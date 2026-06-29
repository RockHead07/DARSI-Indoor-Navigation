using UnityEngine;
using UnityEngine.UIElements;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    public enum Screen { Splash, AuthGate, Login, Register, Home, AR }

    [Header("UI Toolkit Documents")]
    [SerializeField] private UIDocument splashDocument;
    [SerializeField] private UIDocument authGateDocument;
    [SerializeField] private UIDocument loginDocument;
    [SerializeField] private UIDocument registerDocument;
    [SerializeField] private UIDocument homeDocument;

    [Header("uGUI")]
    [SerializeField] private GameObject arCanvas;

    private Screen _current;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetScreen(Screen.Splash);
    }

    public void ShowSplash()    => SetScreen(Screen.Splash);
    public void ShowAuthGate()  => SetScreen(Screen.AuthGate);
    public void ShowLogin()     => SetScreen(Screen.Login);
    public void ShowRegister()  => SetScreen(Screen.Register);
    public void ShowHome()      => SetScreen(Screen.Home);
    public void ShowAR()        => SetScreen(Screen.AR);

    private void SetScreen(Screen next)
    {
        _current = next;

        SetDoc(splashDocument,   next == Screen.Splash);
        SetDoc(authGateDocument, next == Screen.AuthGate);
        SetDoc(loginDocument,    next == Screen.Login);
        SetDoc(registerDocument, next == Screen.Register);
        SetDoc(homeDocument,     next == Screen.Home);
        SetCanvas(arCanvas,      next == Screen.AR);

        Debug.Log($"[ScreenManager] → {next}");
    }

    private static void SetDoc(UIDocument doc, bool visible)
    {
        if (doc == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void SetCanvas(GameObject canvas, bool visible)
    {
        if (canvas != null) canvas.SetActive(visible);
    }
}
