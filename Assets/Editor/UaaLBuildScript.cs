#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// Script khusus untuk memproduksi Build Unity as a Library (UaaL) Android Export
/// untuk di-consume oleh aplikasi Flutter (MyRSIy).
/// </summary>
public static class UaaLBuildScript
{
    [MenuItem("Build/Build Android UaaL Export")]
    public static void BuildAndroidUaaL()
    {
        string exportPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "AndroidUaaL");
        Debug.Log($"[UaaLBuildScript] Memulai export UaaL ke: {exportPath}");

        if (Directory.Exists(exportPath))
        {
            Directory.Delete(exportPath, true);
        }
        Directory.CreateDirectory(exportPath);

        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.buildAppBundle = false;

        string[] scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("[UaaLBuildScript] Tidak ada Scene yang di-enable di Build Settings!");
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exportPath,
            target = BuildTarget.Android,
            options = BuildOptions.AcceptExternalModificationsToPlayer
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[UaaLBuildScript] Export UaaL BERHASIL! Total size: {summary.totalSize} bytes");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[UaaLBuildScript] Export UaaL GAGAL! Total errors: {summary.totalErrors}");
            throw new Exception($"[UaaLBuildScript] Build Gagal dengan result: {summary.result}");
        }
    }

    private static string[] GetEnabledScenes()
    {
        List<string> enabledScenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                enabledScenes.Add(scene.path);
            }
        }
        return enabledScenes.ToArray();
    }
}
#endif
