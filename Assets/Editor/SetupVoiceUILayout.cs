#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SetupVoiceUILayout
{
    [MenuItem("DARSI/Setup Voice UI Layout in All Scenes")]
    public static void Execute()
    {
        string[] scenes = new string[]
        {
            "Assets/Samples/MultiSet-SDK/1.11.5/Sample Scenes/Navigation/DARSi-Indoor Navigation.unity",
            "Assets/Samples/MultiSet-SDK/1.11.5/Sample Scenes/Navigation/TestingHCM.unity"
        };

        foreach (var scenePath in scenes)
        {
            SetupScene(scenePath);
        }
    }

    private static void SetupScene(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log($"[SetupVoiceUILayout] Processing scene: {scene.name}");

        GameObject voicePanel = GameObject.Find("VoicePanel");
        if (voicePanel == null)
        {
            // Search inactive
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindInChildren(root.transform, "VoicePanel");
                if (found != null)
                {
                    voicePanel = found.gameObject;
                    break;
                }
            }
        }

        if (voicePanel == null)
        {
            Debug.LogError($"[SetupVoiceUILayout] VoicePanel not found in {scene.name}!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(voicePanel, "Setup Voice UI Layout");

        RectTransform vpRect = voicePanel.GetComponent<RectTransform>();
        if (vpRect != null)
        {
            vpRect.anchorMin = new Vector2(0.5f, 0f);
            vpRect.anchorMax = new Vector2(0.5f, 0f);
            vpRect.pivot = new Vector2(0.5f, 0f);
            vpRect.sizeDelta = new Vector2(1080f, 1100f);
            vpRect.anchoredPosition = new Vector2(0f, -100f);
        }

        // 1. Exit Button
        Transform exitT = voicePanel.transform.Find("Exit");
        if (exitT != null)
        {
            RectTransform exitRect = exitT.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(0.5f, 0.5f);
            exitRect.anchorMax = new Vector2(0.5f, 0.5f);
            exitRect.pivot = new Vector2(0.5f, 0.5f);
            exitRect.anchoredPosition = new Vector2(430f, 470f);
            exitRect.sizeDelta = new Vector2(90f, 90f);

            Button btn = exitT.GetComponent<Button>();
            if (btn != null)
            {
                VoiceUIController vui = voicePanel.GetComponent<VoiceUIController>();
                VoiceInputHandler vih = Object.FindAnyObjectByType<VoiceInputHandler>();

                // Bersihkan dan pasang listener bersih
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, 0);
                while (btn.onClick.GetPersistentEventCount() > 0)
                {
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, 0);
                }

                if (vui != null)
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, vui.HidePanel);
                }
                if (vih != null)
                {
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, vih.CancelListening);
                }
            }
        }

        // 2. Illustration Image (Voice-Logo)
        Transform imgT = voicePanel.transform.Find("Image");
        if (imgT != null)
        {
            RectTransform imgRect = imgT.GetComponent<RectTransform>();
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.anchoredPosition = new Vector2(0f, 230f);
            imgRect.sizeDelta = new Vector2(360f, 360f);

            Image img = imgT.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
            }
        }

        // 3. Text Transcript
        Transform transcriptT = voicePanel.transform.Find("Text Transcript");
        if (transcriptT != null)
        {
            RectTransform transRect = transcriptT.GetComponent<RectTransform>();
            transRect.anchorMin = new Vector2(0.5f, 0.5f);
            transRect.anchorMax = new Vector2(0.5f, 0.5f);
            transRect.pivot = new Vector2(0.5f, 0.5f);
            transRect.anchoredPosition = new Vector2(0f, -50f);
            transRect.sizeDelta = new Vector2(880f, 160f);

            TextMeshProUGUI tmp = transcriptT.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 32f;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 20f;
                tmp.fontSizeMax = 34f;
                tmp.enableWordWrapping = true;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }

        // 4. StatusPill
        Transform pillT = voicePanel.transform.Find("StatusPill");
        if (pillT != null)
        {
            RectTransform pillRect = pillT.GetComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(0.5f, 0.5f);
            pillRect.anchorMax = new Vector2(0.5f, 0.5f);
            pillRect.pivot = new Vector2(0.5f, 0.5f);
            pillRect.anchoredPosition = new Vector2(0f, -190f);
            pillRect.sizeDelta = new Vector2(340f, 56f);

            Image pillImg = pillT.GetComponent<Image>();
            if (pillImg != null)
            {
                pillImg.type = Image.Type.Sliced;
            }

            Transform statusTextT = pillT.Find("StatusText");
            if (statusTextT != null)
            {
                RectTransform stRect = statusTextT.GetComponent<RectTransform>();
                stRect.anchorMin = new Vector2(0f, 0f);
                stRect.anchorMax = new Vector2(1f, 1f);
                stRect.pivot = new Vector2(0.5f, 0.5f);
                stRect.anchoredPosition = Vector2.zero;
                stRect.sizeDelta = new Vector2(-20f, -10f);

                TextMeshProUGUI stTmp = statusTextT.GetComponent<TextMeshProUGUI>();
                if (stTmp != null)
                {
                    stTmp.fontSize = 24f;
                    stTmp.enableAutoSizing = true;
                    stTmp.fontSizeMin = 16f;
                    stTmp.fontSizeMax = 24f;
                    stTmp.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        // 5. WaveFormContainer
        Transform waveT = voicePanel.transform.Find("WaveFormContainer");
        if (waveT != null)
        {
            RectTransform waveRect = waveT.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0.5f, 0.5f);
            waveRect.anchorMax = new Vector2(0.5f, 0.5f);
            waveRect.pivot = new Vector2(0.5f, 0.5f);
            waveRect.anchoredPosition = new Vector2(0f, -260f);
            waveRect.sizeDelta = new Vector2(240f, 40f);
        }

        // 6. Setup VoiceUIController serialized properties
        VoiceUIController voiceController = voicePanel.GetComponent<VoiceUIController>();
        if (voiceController != null)
        {
            var so = new SerializedObject(voiceController);
            so.FindProperty("voicePanel").objectReferenceValue = voicePanel;
            so.FindProperty("startHidden").boolValue = true;
            if (pillT != null)
            {
                so.FindProperty("statusPillBg").objectReferenceValue = pillT.GetComponent<Image>();
                Transform st = pillT.Find("StatusText");
                if (st != null) so.FindProperty("statusText").objectReferenceValue = st.GetComponent<TMP_Text>();
            }
            if (transcriptT != null)
            {
                so.FindProperty("transcriptText").objectReferenceValue = transcriptT.GetComponent<TMP_Text>();
            }
            if (waveT != null)
            {
                so.FindProperty("waveformContainer").objectReferenceValue = waveT.GetComponent<RectTransform>();
            }
            so.FindProperty("transcriptAutoHideSecondsDefault").floatValue = 5.0f;
            so.FindProperty("autoHidePanelAfterTranscript").boolValue = true;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupVoiceUILayout] Successfully updated and saved {scene.name}!");
    }

    private static Transform FindInChildren(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindInChildren(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
