using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BottomNavigationManager : MonoBehaviour
{
    [System.Serializable]
    public class NavigationTab
    {
        public Button buttonElement;       // Tombol UI
        public Image backgroundImage;     // Latar belakang dari tombol ini (Kotak Kuning)
        public Image iconImage;           // Ikon di dalam tombol (Mikrofon, Search, dll)
    }

    [Header("Navigation Settings")]
    public List<NavigationTab> navigationTabs; // Daftar semua tombol navigasimu
    
    [Header("Background Colors")]
    public Color bgActiveColor = new Color(1f, 0.84f, 0f); // Warna kuning cerah
    public Color bgInactiveColor = Color.clear;           // Transparan untuk yang tidak aktif

    [Header("Icon Colors")]
    public Color iconActiveColor = new Color(0.05f, 0.18f, 0.39f); // Warna biru tua saat aktif (kontras dengan kuning)
    public Color iconInactiveColor = Color.white;                  // Warna awal ikon saat tidak aktif (putih)

    void Start()
    {
        // Berikan listener otomatis ke setiap tombol menggunakan logika perulangan
        for (int i = 0; i < navigationTabs.Count; i++)
        {
            int index = i; // Salin index agar tidak terjadi closure bug di Unity
            if (navigationTabs[i].buttonElement != null)
            {
                navigationTabs[i].buttonElement.onClick.AddListener(() => OnTabSelected(index));
            }
        }

        // KRITIK: Baris OnTabSelected(0) yang mengaktifkan kotak kuning di awal SUDAH DIHAPUS.
        // Sekarang kita hanya mengeset seluruh tombol ke status INACTIVE (Warna awal) saat pertama kali buka.
        ResetAllTabsToDefault();
    }

    // Fungsi khusus untuk memastikan kondisi awal aplikasi bersih tanpa background aktif
    private void ResetAllTabsToDefault()
    {
        for (int i = 0; i < navigationTabs.Count; i++)
        {
            if (navigationTabs[i].backgroundImage != null)
            {
                navigationTabs[i].backgroundImage.color = bgInactiveColor;
            }
            
            if (navigationTabs[i].iconImage != null)
            {
                navigationTabs[i].iconImage.color = iconInactiveColor;
            }
        }
        Debug.Log("[NAV SYSTEM] Aplikasi dimulai: Seluruh tab berada di status awal (Clean).");
    }

    public void OnTabSelected(int selectedIndex)
    {
        // Iterasi seluruh tombol untuk menentukan siapa yang berhak berubah warna
        for (int i = 0; i < navigationTabs.Count; i++)
        {
            if (i == selectedIndex)
            {
                // 1. Aktifkan latar belakang kuning pada tombol terpilih
                if (navigationTabs[i].backgroundImage != null)
                {
                    navigationTabs[i].backgroundImage.color = bgActiveColor;
                }

                // 2. Ubah warna ikon menjadi warna aktif (saat ditekan)
                if (navigationTabs[i].iconImage != null)
                {
                    navigationTabs[i].iconImage.color = iconActiveColor;
                }

                if (navigationTabs[i].buttonElement != null)
                {
                    Debug.Log($"[NAV SYSTEM] Tab {navigationTabs[i].buttonElement.name} Aktif.");
                }
            }
            else
            {
                // 3. Kembalikan background tombol lain menjadi transparan
                if (navigationTabs[i].backgroundImage != null)
                {
                    navigationTabs[i].backgroundImage.color = bgInactiveColor;
                }

                // 4. Kembalikan warna ikon tombol lain menjadi warna awal (putih/default)
                if (navigationTabs[i].iconImage != null)
                {
                    navigationTabs[i].iconImage.color = iconInactiveColor;
                }
            }
        }

        // Tempat memicu fungsi utama dari masing-masing tombol
        ExecuteTabFunction(selectedIndex);
    }

    private void ExecuteTabFunction(int index)
    {
        switch (index)
        {
            case 0:
                Debug.Log("[NAV EXECUTE] Membuka Menu Search.");
                break;
            case 1:
                Debug.Log("[NAV EXECUTE] Membuka Menu Voice AI.");
                break;
            case 2:
                Debug.Log("[NAV EXECUTE] Membuka Menu Multiplayer.");
                break;
            case 3:
                Debug.Log("[NAV EXECUTE] Membuka Menu Scanner.");
                break;
        }
    }
}