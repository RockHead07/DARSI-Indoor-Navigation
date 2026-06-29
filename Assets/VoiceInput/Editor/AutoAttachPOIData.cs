using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: menambahkan komponen POIData ke semua child di bawah 
/// GameObject bernama "POIs" di scene aktif.
/// </summary>
public class AutoAttachPOIData
{
    [MenuItem("Tools/POI/Auto Attach POIData")]
    static void AttachPOIDataToChildren()
    {
        // Cari semua root GameObjects di scene
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().GetRootGameObjects();

        Transform poiRoot = null;

        foreach (GameObject root in rootObjects)
        {
            // Cari langsung di root
            if (root.name == "POIs")
            {
                poiRoot = root.transform;
                break;
            }

            // Cari di children (termasuk inactive)
            Transform found = FindChildRecursive(root.transform, "POIs");
            if (found != null)
            {
                poiRoot = found;
                break;
            }
        }

        if (poiRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Auto Attach POIData",
                "Tidak ditemukan GameObject bernama 'POIs' di scene aktif.\n" +
                "Buat GameObject 'POIs' terlebih dahulu sebagai parent POI.",
                "OK"
            );
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;

        foreach (Transform child in poiRoot)
        {
            POIData existing = child.GetComponent<POIData>();
            if (existing != null)
            {
                skippedCount++;
                continue;
            }

            POIData newPOI = Undo.AddComponent<POIData>(child.gameObject);
            newPOI.poiName = child.gameObject.name;
            addedCount++;
        }

        EditorUtility.DisplayDialog(
            "Auto Attach POIData",
            $"Selesai!\n\n" +
            $"Ditambahkan: {addedCount} POIData\n" +
            $"Sudah ada (skip): {skippedCount}\n" +
            $"Total children: {poiRoot.childCount}",
            "OK"
        );

        Debug.Log($"[AutoAttachPOIData] Added {addedCount}, skipped {skippedCount} under '{poiRoot.name}'");
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
