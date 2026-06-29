using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

public class FixPanelSettings
{
    public static void Execute()
    {
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/UIToolkit/SplashPanelSettings.asset");
        if (ps == null) { Debug.LogError("PanelSettings not found"); return; }

        ps.referenceResolution = new Vector2Int(1080, 1920);
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;

        EditorUtility.SetDirty(ps);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixPanelSettings] referenceResolution set to 1080x1920");
    }
}
