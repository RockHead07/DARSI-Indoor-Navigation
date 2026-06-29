using UnityEditor;

public class RefreshAssets
{
    public static void Execute()
    {
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
    }
}
