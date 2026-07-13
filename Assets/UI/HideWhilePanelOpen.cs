using UnityEngine;

/// <summary>
/// Menyembunyikan tombol HUD persisten (Mic, PlayerListButton, CaptureButton,
/// ShowDestinationsButton) selama ADA page exclusive terbuka (lihat <see cref="ExclusivePanel"/>).
///
/// Kenapa: page full-screen itu modal, tapi tombol HUD ini sibling terpisah dengan
/// sibling-index lebih tinggi → render DI ATAS page & tidak ikut tertutup → overlap
/// mengganggu pandangan. Ini menegakkan "HUD minggir saat page terbuka".
///
/// Pakai CanvasGroup (alpha 0 + blocksRaycasts off), BUKAN SetActive: GameObject tetap
/// aktif sehingga komponen ini terus jalan & bisa memunculkan tombol kembali saat page
/// tertutup. Murni aditif — tempel di tiap tombol HUD, tak perlu wiring referensi.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[DisallowMultipleComponent]
public class HideWhilePanelOpen : MonoBehaviour
{
    private CanvasGroup _cg;

    private void Awake() => _cg = GetComponent<CanvasGroup>();

    private void OnEnable()
    {
        ExclusivePanel.AnyOpenChanged += Apply;
        Apply(ExclusivePanel.AnyOpen); // sinkron dengan keadaan saat ini
    }

    private void OnDisable()
    {
        ExclusivePanel.AnyOpenChanged -= Apply;
    }

    private void Apply(bool anyPanelOpen)
    {
        _cg.alpha = anyPanelOpen ? 0f : 1f;
        _cg.interactable = !anyPanelOpen;
        _cg.blocksRaycasts = !anyPanelOpen;
    }
}
