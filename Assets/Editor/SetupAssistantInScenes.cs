#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupAssistantInScenes
{
    public static void Execute()
    {
        SetupProductionScene();
    }

    [MenuItem("DARSI/Setup Assistant in Production Scene")]
    public static void SetupProductionScene()
    {
        string scenePath = "Assets/Samples/MultiSet-SDK/1.11.5/Sample Scenes/Navigation/DARSi-Indoor Navigation.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. Cari / Buat AssistantManager
        GameObject assistantObj = GameObject.Find("AssistantManager");
        if (assistantObj == null)
        {
            assistantObj = new GameObject("AssistantManager");
            Undo.RegisterCreatedObjectUndo(assistantObj, "Create AssistantManager");
            Debug.Log("[SetupAssistant] Created AssistantManager GameObject");
        }

        // 2. Setup AssistantClient
        AssistantClient client = assistantObj.GetComponent<AssistantClient>();
        if (client == null)
        {
            client = assistantObj.AddComponent<AssistantClient>();
        }

        var clientSo = new SerializedObject(client);
        clientSo.FindProperty("baseUrl").stringValue = "https://api-darsi.rockhead07.tech";
        clientSo.FindProperty("timeoutSeconds").intValue = 60;
        clientSo.FindProperty("buildingName").stringValue = "RS Islam Ahmad Yani";
        clientSo.ApplyModifiedProperties();

        // 3. Setup AssistantTestPanel
        AssistantTestPanel testPanel = assistantObj.GetComponent<AssistantTestPanel>();
        if (testPanel == null)
        {
            testPanel = assistantObj.AddComponent<AssistantTestPanel>();
        }

        var panelSo = new SerializedObject(testPanel);
        Button logoButton = null;
        GameObject logoObj = GameObject.Find("Canvas/Logo");
        if (logoObj != null) logoButton = logoObj.GetComponent<Button>();
        if (logoButton != null)
        {
            panelSo.FindProperty("logoTapTarget").objectReferenceValue = logoButton;
        }
        panelSo.ApplyModifiedProperties();

        // 4. Wire ke VoiceInputHandler
        VoiceInputHandler voiceHandler = Object.FindAnyObjectByType<VoiceInputHandler>();
        if (voiceHandler != null)
        {
            var voiceSo = new SerializedObject(voiceHandler);
            voiceSo.FindProperty("assistantClient").objectReferenceValue = client;
            voiceSo.FindProperty("useRAGPrimary").boolValue = true;
            voiceSo.FindProperty("enableFallback").boolValue = true;
            voiceSo.ApplyModifiedProperties();
            Debug.Log("[SetupAssistant] Wired AssistantClient to VoiceInputHandler in DARSi-Indoor Navigation");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupAssistant] DARSi-Indoor Navigation scene saved successfully!");
    }
}
#endif
