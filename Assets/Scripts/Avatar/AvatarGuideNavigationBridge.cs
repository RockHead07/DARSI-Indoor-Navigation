using UnityEngine;

/// <summary>
/// Menyalakan dan mematikan pemandu mengikuti sesi navigasi MultiSet.
///
/// Pemanggil REAKTIF <see cref="AIAvatarGuideController.StartLeading"/> untuk jalur navigasi
/// non-suara (klik POI langsung). BUKAN satu-satunya pemanggil sah -- AvatarAudioClient juga
/// memanggilnya LANGSUNG untuk alur suara (ADR-034: avatar bicara dulu, baru memimpin).
/// Sebelum komponen ini ada, jalur non-suara sama sekali tidak punya pemanggil, sehingga
/// avatar di jalur itu hanya bisa digerakkan oleh probe di folder Editor. StartLeading()
/// sendiri sengaja idempoten supaya dua pemanggil ini tidak saling tabrak kalau sama-sama
/// terpicu untuk sesi memimpin yang sama (lihat komentar di StartLeading()).
///
/// GATE LOKALISASI (ADR-034 keputusan 5, ADR-007). Avatar hanya boleh bergerak setelah posisi
/// sah, yaitu setelah MultiSet localize berhasil. Gate-nya sengaja BUKAN flag yang di-serialize:
/// keputusan 5 melarang adanya saklar "matikan pengaman" yang bisa ikut ter-build tanpa sengaja.
/// Jalan pintas untuk pengujian Editor dibungkus #if UNITY_EDITOR sehingga DIHAPUS COMPILER dari
/// build Android. Bukan disiplin, tapi mustahil secara mekanis.
///
/// Pemasangan: taruh di GameObject yang sama dengan AIAvatarGuideController, lalu wire
/// <see cref="OnLocalizationSuccess"/> sebagai persistent listener pada UnityEvent
/// LocalizationSuccess milik SingleFrameLocalizationManager — pola yang sama dengan
/// FloorTransitionController, supaya hanya ada satu cara mengetahui status localize.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AIAvatarGuideController))]
public class AvatarGuideNavigationBridge : MonoBehaviour
{
    [SerializeField] private AIAvatarGuideController guide;
    [Tooltip("Kosongkan untuk mencari NavigationController otomatis di scene.")]
    [SerializeField] private NavigationController navigation;

    private bool _wasNavigating;
    private bool _localized;

    private void Awake()
    {
        if (guide == null) guide = GetComponent<AIAvatarGuideController>();
        if (navigation == null) navigation = FindFirstObjectByType<NavigationController>();
    }

    /// <summary>Wire sebagai persistent listener pada UnityEvent LocalizationSuccess.</summary>
    public void OnLocalizationSuccess() => _localized = true;

    /// <summary>Dipanggil kalau sesi AR ditutup atau tracking hilang, supaya avatar berhenti.</summary>
    public void OnLocalizationLost()
    {
        _localized = false;
        if (guide != null) guide.StopLeading();
    }

    private void Update()
    {
        if (guide == null || navigation == null) return;

        bool navigating = navigation.IsCurrentlyNavigating();
        if (navigating == _wasNavigating) return;   // hanya bertindak saat status BERUBAH
        _wasNavigating = navigating;

        if (navigating && PositionIsTrustworthy())
            guide.StartLeading();
        else
            guide.StopLeading();
    }

    private bool PositionIsTrustworthy()
    {
        if (_localized) return true;

#if UNITY_EDITOR
        // Di Editor tidak ada kamera AR sungguhan, jadi MultiSet tidak akan pernah localize dan
        // pemandu mustahil diuji manual. Blok ini tidak ada di build, jadi tidak bisa bocor ke
        // lapangan. Peringatannya sengaja keras supaya angka hasil uji Editor tidak pernah
        // disalahartikan sebagai bukti perilaku di device.
        Debug.LogWarning("[AvatarGuideNavigationBridge] EDITOR: memimpin TANPA localize. " +
                         "Posisi tidak sah di dunia nyata; hasil uji ini tidak berlaku untuk device.");
        return true;
#else
        return false;
#endif
    }
}
