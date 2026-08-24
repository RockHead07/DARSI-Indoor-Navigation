using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tool otomatis untuk membuat dan menyiapkan Scene Sandbox Avatar Companion (Tahap 1 MVP - ADR-030).
/// Menu: Tools > DARSI Avatar > Setup Sandbox Scene.
/// </summary>
public static class AvatarSandboxSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox_AvatarCompanion.unity";

    [MenuItem("DARSI/Avatar/Setup Sandbox Scene")]
    [MenuItem("Tools/Avatar/Setup Sandbox Scene")]
    public static void CreateOrOpenSandboxScene()
    {
        // 1. Buat scene baru
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2. Setup Kamera & Cahaya
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0, 1.6f, 0);
            mainCam.transform.rotation = Quaternion.identity;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        }

        // 3. Buat Lantai Grid (Simulasi Ruangan RS)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor_Grid";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);

        // 4. Buat Avatar Companion GameObject
        var avatarRoot = new GameObject("Avatar_Companion");
        avatarRoot.transform.position = new Vector3(0, 0, 1.8f);
        avatarRoot.transform.rotation = Quaternion.Euler(0, 180f, 0);

        // Pasang Visual Model (Placeholder Humanoid jika model fbx ada)
        GameObject visualModel = null;
        string[] modelGuids = AssetDatabase.FindAssets("Idle t:Model");
        if (modelGuids.Length > 0)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[0]);
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                visualModel = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, avatarRoot.transform);
                visualModel.name = "Model_Visual";
                visualModel.transform.localPosition = Vector3.zero;
                visualModel.transform.localRotation = Quaternion.identity;
            }
        }

        if (visualModel == null)
        {
            // Fallback capsule placeholder
            visualModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualModel.name = "Model_Placeholder";
            visualModel.transform.SetParent(avatarRoot.transform, false);
            visualModel.transform.localPosition = new Vector3(0, 0.9f, 0);

            // Visor mata
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor_Eyes";
            visor.transform.SetParent(visualModel.transform, false);
            visor.transform.localPosition = new Vector3(0, 0.35f, 0.35f);
            visor.transform.localScale = new Vector3(0.5f, 0.2f, 0.3f);
        }

        var lookAt = avatarRoot.AddComponent<AvatarLookAtController>();
        var safetyFade = avatarRoot.AddComponent<AvatarSafetyFade>();
        var companionCtrl = avatarRoot.AddComponent<AvatarCompanionController>();

        // 5. Buat Canvas UI Sandbox
        var canvasGo = new GameObject("Canvas_SandboxUI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel Container
        var panelGo = new GameObject("Panel_Controls", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 30f);
        panelRect.sizeDelta = new Vector2(500f, 180f);
        panelGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

        // Status Text
        var txtStatusGo = new GameObject("Txt_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtStatusGo.transform.SetParent(panelGo.transform, false);
        var statusRect = txtStatusGo.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 60f);
        statusRect.sizeDelta = new Vector2(460f, 30f);
        var txtStatus = txtStatusGo.GetComponent<TextMeshProUGUI>();
        txtStatus.text = "State: Hidden";
        txtStatus.alignment = TextAlignmentOptions.Center;
        txtStatus.fontSize = 18;
        txtStatus.color = Color.white;

        // Distance Text
        var txtDistGo = new GameObject("Txt_Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtDistGo.transform.SetParent(panelGo.transform, false);
        var distRect = txtDistGo.GetComponent<RectTransform>();
        distRect.anchoredPosition = new Vector2(0, 30f);
        distRect.sizeDelta = new Vector2(460f, 25f);
        var txtDist = txtDistGo.GetComponent<TextMeshProUGUI>();
        txtDist.text = "Jarak Kamera: -";
        txtDist.alignment = TextAlignmentOptions.Center;
        txtDist.fontSize = 14;
        txtDist.color = new Color(0.8f, 0.8f, 0.8f);

        // Buttons Layout
        var btnGroupGo = new GameObject("Buttons_Group", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnGroupGo.transform.SetParent(panelGo.transform, false);
        var bgRect = btnGroupGo.GetComponent<RectTransform>();
        bgRect.anchoredPosition = new Vector2(0, -25f);
        bgRect.sizeDelta = new Vector2(460f, 50f);
        var hlg = btnGroupGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;

        var btnSpawn = CreateButton("Btn_Spawn", "Panggil Avatar", btnGroupGo.transform, new Color(0.15f, 0.55f, 0.35f));
        var btnPoint = CreateButton("Btn_Point", "Tunjuk Arah", btnGroupGo.transform, new Color(0.2f, 0.45f, 0.7f));
        var btnDismiss = CreateButton("Btn_Dismiss", "Tutup", btnGroupGo.transform, new Color(0.65f, 0.2f, 0.2f));

        var sandboxUI = canvasGo.AddComponent<AvatarSandboxUI>();
        
        // Serialized property setup via SerializedObject
        var so = new SerializedObject(sandboxUI);
        so.FindProperty("companionController").objectReferenceValue = companionCtrl;
        so.FindProperty("btnSpawn").objectReferenceValue = btnSpawn;
        so.FindProperty("btnPoint").objectReferenceValue = btnPoint;
        so.FindProperty("btnDismiss").objectReferenceValue = btnDismiss;
        so.FindProperty("txtStatus").objectReferenceValue = txtStatus;
        so.FindProperty("txtDistance").objectReferenceValue = txtDist;
        so.ApplyModifiedProperties();

        // 6. Simpan Scene
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[AvatarSandboxSceneBuilder] Scene berhasil dibuat dan disimpan di: {ScenePath}");
    }

    private static Button CreateButton(string name, string label, Transform parent, Color bgColor)
    {
        var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = bgColor;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 16;
        tmp.color = Color.white;

        return btnGo.GetComponent<Button>();
    }
}
