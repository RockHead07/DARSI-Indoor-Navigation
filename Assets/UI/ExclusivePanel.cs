using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menjamin hanya SATU panel "page" full-screen yang terbuka pada satu waktu.
///
/// Akar masalah (bug overlap): tiap panel page (VoicePanel, FriendListPanel,
/// Destination Select) nge-<c>SetActive</c> DIRINYA SENDIRI saat tombolnya ditekan,
/// tanpa menutup panel lain. Dua sheet full-screen bisa aktif barengan → overlap →
/// menutupi pandangan AR.
///
/// Fix ini AD-DITIF: tempel komponen ini di tiap GameObject page yang mutually-exclusive.
/// Saat sebuah page jadi aktif (dibuka lewat jalur APAPUN — tombol, voice auto-open, dll),
/// <c>OnEnable</c> menutup semua page terdaftar lainnya. TIDAK menyentuh script panel
/// yang protected (VoiceUIController/FriendListPanel/PlayerInfoPopup) — mereka tetap
/// nge-toggle diri seperti biasa, komponen ini cuma menegakkan mutual-exclusion.
///
/// Best-practice penuh = screen-stack manager tunggal yang memiliki visibilitas (panel
/// tak nge-toggle sendiri). Itu perlu merombak panel protected → ditunda; ini retrofit
/// aditif dengan invariant yang sama, risiko regresi minimal.
///
/// CATATAN: PlayerInfoPopup SENGAJA tidak diberi komponen ini — dia popup transient yang
/// muncul di atas FriendListPanel (tap teman) & menutup dirinya sendiri; menjadikannya
/// exclusive akan menutup list di belakangnya.
/// </summary>
[DisallowMultipleComponent]
public class ExclusivePanel : MonoBehaviour
{
    // Registry semua page exclusive yang hidup di scene. Static = tak perlu manager
    // object terpisah / wiring referensi (murni drop-in per panel).
    private static readonly List<ExclusivePanel> Registry = new List<ExclusivePanel>();

    // Jumlah page exclusive yang sedang terbuka + event saat menyeberang 0↔1. Dipakai
    // HideWhilePanelOpen untuk menyembunyikan tombol HUD (Mic/PlayerListButton/dll) selama
    // ada page terbuka — page = modal, HUD-nya minggir supaya tidak overlap di atas panel.
    private static int _openCount;
    public static bool AnyOpen => _openCount > 0;
    public static event System.Action<bool> AnyOpenChanged;

    private void Awake()
    {
        if (!Registry.Contains(this)) Registry.Add(this);
    }

    private void OnDestroy()
    {
        Registry.Remove(this);
    }

    // Page ini baru dibuka (jalur apapun) → tutup semua page exclusive lain yang aktif.
    // SetActive(false) memicu OnDisable (bukan OnEnable) pada yang lain → tidak ada loop.
    private void OnEnable()
    {
        // Hitung diri sendiri DULU sebelum menutup sibling, supaya count tak sempat jatuh
        // ke 0 di tengah pergantian page (mencegah HUD berkedip muncul-hilang saat switch).
        SetOpenCount(_openCount + 1);

        for (int i = 0; i < Registry.Count; i++)
        {
            var other = Registry[i];
            if (other != null && other != this && other.gameObject.activeSelf)
                other.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        SetOpenCount(_openCount - 1);
    }

    private static void SetOpenCount(int value)
    {
        bool was = _openCount > 0;
        _openCount = Mathf.Max(0, value);
        bool now = _openCount > 0;
        if (was != now) AnyOpenChanged?.Invoke(now);
    }
}
