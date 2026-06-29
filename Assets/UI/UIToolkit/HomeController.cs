using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Home dashboard with 3 role variants: Mahasiswa, Dosen/Staff, Tamu.
/// One UXML/USS; the role is applied by swapping a class on the root element
/// (.role-mahasiswa / .role-dosen / .role-tamu) which USS uses to show/hide
/// the guest banner, lock cards, swap history list for the empty state, etc.
///
/// Role source is mocked for now (Inspector field / PlayerPrefs). Wire it to
/// the real auth response when the backend exists.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HomeController : MonoBehaviour
{
    public enum Role { Mahasiswa, Dosen, Tamu }

    [Header("Mock data (replace with auth response)")]
    [SerializeField] private Role role = Role.Mahasiswa;

    private VisualElement _root;

    private Label _greeting, _badge, _initials;
    private Label _qa3Label, _qa4Label;
    private VisualElement _qa3Icon, _qa4Icon, _qa3, _qa4;
    private Button _qaAr, _qaRoom, _btnMic, _btnBell;
    private Button _navHome, _navMap, _navFab, _navHistory, _navProfile, _btnGuestDaftar;

    private void Awake() => _root = GetComponent<UIDocument>().rootVisualElement;

    private void OnEnable()
    {
        // Allow a logged-in role to override the Inspector default.
        var saved = PlayerPrefs.GetString("session_role", "");
        if (saved == "dosen") role = Role.Dosen;
        else if (saved == "tamu") role = Role.Tamu;

        Query();
        ApplyRole(role);
        WireButtons();
    }

    private void Query()
    {
        _greeting       = _root.Q<Label>("greeting-name");
        _badge          = _root.Q<Label>("role-badge");
        _initials       = _root.Q<Label>("avatar-initials");
        _qa3Label       = _root.Q<Label>("qa-3-label");
        _qa4Label       = _root.Q<Label>("qa-4-label");
        _qa3Icon        = _root.Q<VisualElement>("qa-3-icon");
        _qa4Icon        = _root.Q<VisualElement>("qa-4-icon");
        _qa3            = _root.Q<Button>("qa-3");
        _qa4            = _root.Q<Button>("qa-4");

        _qaAr           = _root.Q<Button>("qa-ar");
        _qaRoom         = _root.Q<Button>("qa-room");
        _btnMic         = _root.Q<Button>("btn-mic");
        _btnBell        = _root.Q<Button>("btn-bell");
        _btnGuestDaftar = _root.Q<Button>("btn-guest-daftar");

        _navHome        = _root.Q<Button>("nav-home");
        _navMap         = _root.Q<Button>("nav-map");
        _navFab         = _root.Q<Button>("nav-fab");
        _navHistory     = _root.Q<Button>("nav-history");
        _navProfile     = _root.Q<Button>("nav-profile");
    }

    private void ApplyRole(Role r)
    {
        // root class drives all USS-level variant styling
        _root.RemoveFromClassList("role-mahasiswa");
        _root.RemoveFromClassList("role-dosen");
        _root.RemoveFromClassList("role-tamu");

        SetIcon(_qa3Icon, "icon-calendar");
        _qa3?.RemoveFromClassList("locked");
        _qa4?.RemoveFromClassList("locked");

        switch (r)
        {
            case Role.Mahasiswa:
                _root.AddToClassList("role-mahasiswa");
                Set(_greeting, "Halo, Andi");
                Set(_badge, "Mahasiswa");
                Set(_initials, "AK");
                Set(_qa3Label, "Jadwal Hari Ini");
                Set(_qa4Label, "Rute Favorit");
                SetIcon(_qa4Icon, "icon-star");
                break;

            case Role.Dosen:
                _root.AddToClassList("role-dosen");
                Set(_greeting, "Halo, Dr. Sari");
                Set(_badge, "Dosen/Staff");
                Set(_initials, "DS");
                Set(_qa3Label, "Jadwal Mengajar");
                Set(_qa4Label, "Ruang Saya");
                SetIcon(_qa4Icon, "icon-room2");
                break;

            case Role.Tamu:
                _root.AddToClassList("role-tamu");
                Set(_greeting, "Mode tamu");
                Set(_badge, "Tamu");
                Set(_initials, "?");
                Set(_qa3Label, "Jadwal");
                Set(_qa4Label, "Favorit");
                SetIcon(_qa4Icon, "icon-star");
                _qa3?.AddToClassList("locked");
                _qa4?.AddToClassList("locked");
                break;
        }
    }

    private void WireButtons()
    {
        if (_qaAr   != null) _qaAr.clicked   += StartLocalization;
        if (_navFab != null) _navFab.clicked += StartLocalization;
        if (_qaRoom != null) _qaRoom.clicked += () => ScreenManager.Instance.ShowAR();
        if (_navMap != null) _navMap.clicked += () => ScreenManager.Instance.ShowAR();

        if (_btnGuestDaftar != null) _btnGuestDaftar.clicked += () => ScreenManager.Instance.ShowRegister();

        if (_btnMic     != null) _btnMic.clicked     += () => Debug.Log("[Home] Mic (voice search) — TODO");
        if (_btnBell    != null) _btnBell.clicked    += () => Debug.Log("[Home] Notifikasi — TODO");
        if (_navHome    != null) _navHome.clicked    += () => Debug.Log("[Home] Beranda (current)");
        if (_navHistory != null) _navHistory.clicked += () => Debug.Log("[Home] Riwayat — TODO");
        if (_navProfile != null) _navProfile.clicked += () => Debug.Log("[Home] Profil — TODO");
    }

    private void StartLocalization()
    {
        // Masuk ke AR scene untuk mulai scanning & localization.
        // MultiSet LocalizationSuccessDataHandler akan callback ke PhotonManager
        // setelah area berhasil dikenali.
        Debug.Log("[Home] Memulai scanning area…");
        ScreenManager.Instance.ShowAR();
    }

    private static void Set(Label l, string t) { if (l != null) l.text = t; }

    private static void SetIcon(VisualElement el, string iconClass)
    {
        if (el == null) return;
        el.RemoveFromClassList("icon-calendar");
        el.RemoveFromClassList("icon-star");
        el.RemoveFromClassList("icon-room2");
        el.AddToClassList(iconClass);
    }
}
