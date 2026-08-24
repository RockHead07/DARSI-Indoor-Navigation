using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tool otomatis untuk membuat dan menyiapkan Scene Sandbox Avatar Companion (Tahap 1 MVP - ADR-030).
/// Menggunakan VRMRuntimeLoader untuk me-render AvatarSample_A.vrm secara otomatis saat Play mode.
/// Menu: DARSI > Avatar > Setup Sandbox Scene / Tools > Avatar > Setup Sandbox Scene.
/// </summary>
public static class AvatarSandboxSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox_AvatarCompanion.unity";
    private const string VRMPrefabPath = "Assets/Avatar/Model/AvatarSample_A.prefab";

    [MenuItem("DARSI/Avatar/Setup Sandbox Scene")]
    [MenuItem("Tools/Avatar/Setup Sandbox Scene")]
    public static void CreateOrOpenSandboxScene()
    {
        // 1. Buat scene baru
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2. Setup EventSystem dengan InputSystemUIInputModule (New Input System)
        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // 3. Setup Kamera Bebas (WASD + Mouse Look) & Cahaya
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0, 1.4f, 0);
            mainCam.transform.rotation = Quaternion.identity;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.12f, 0.15f, 0.20f);

            if (mainCam.GetComponent<SimpleSandboxFreeCam>() == null)
            {
                mainCam.gameObject.AddComponent<SimpleSandboxFreeCam>();
            }
        }

        // Setup Directional Light
        var lightObj = GameObject.Find("Directional Light");
        if (lightObj != null)
        {
            var light = lightObj.GetComponent<Light>();
            if (light != null)
            {
                light.color = new Color(1f, 0.98f, 0.94f);
                light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(45f, -35f, 0);
            }
        }

        // 4. Buat Lantai Grid (Simulasi Ruangan RS)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor_Grid";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        
        var floorRend = floor.GetComponent<MeshRenderer>();
        if (floorRend != null)
        {
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            floorMat.color = new Color(0.22f, 0.26f, 0.32f);
            floorRend.sharedMaterial = floorMat;
        }

        // 5. Buat Avatar Companion GameObject tepat di atas lantai (Y = 0)
        var avatarRoot = new GameObject("Avatar_Companion");
        avatarRoot.transform.position = new Vector3(0, 0f, 1.8f);
        avatarRoot.transform.rotation = Quaternion.Euler(0, 180f, 0);

        // Pasang prefab VRM hasil impor UniVRM (ADR-034 keputusan 3).
        // Tidak ada pemuatan runtime dan tidak ada dummy placeholder lagi.
        var vrmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VRMPrefabPath);
        if (vrmPrefab == null)
        {
            Debug.LogError($"[AvatarSandbox] Prefab VRM tidak ditemukan di {VRMPrefabPath}. " +
                           "Pastikan UniVRM terpasang dan .vrm sudah di-import.");
            return;
        }

        var visualModel = (GameObject)PrefabUtility.InstantiatePrefab(vrmPrefab, avatarRoot.transform);
        visualModel.transform.localPosition = Vector3.zero;
        visualModel.transform.localRotation = Quaternion.identity;

        var animator = visualModel.GetComponentInChildren<Animator>();

        // Tulang kepala diambil dari peta Humanoid, bukan pencocokan nama string.
        Transform headTransform = null;
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        // Pasang Controller & Look-At ke Head Bone
        var lookAt = avatarRoot.AddComponent<AvatarLookAtController>();
        if (headTransform != null)
        {
            var lookAtSo = new SerializedObject(lookAt);
            lookAtSo.FindProperty("headBone").objectReferenceValue = headTransform;
            lookAtSo.ApplyModifiedProperties();
        }

        var safetyFade = avatarRoot.AddComponent<AvatarSafetyFade>();

        // JANGAN panggil CacheRenderers() di edit time. Itu mem-bake array renderer ke scene,
        // dan guard `Length == 0` membuat re-cache saat runtime jadi no-op — persis bug yang
        // membuat safety fade memudarkan dummy, bukan model VRM (ADR-034 keputusan 4a).
        // Isi langsung dengan renderer VRM yang sesungguhnya.
        var vrmRenderers = visualModel.GetComponentsInChildren<Renderer>(true);
        var fadeSo = new SerializedObject(safetyFade);
        var rendArr = fadeSo.FindProperty("targetRenderers");
        rendArr.arraySize = vrmRenderers.Length;
        for (int i = 0; i < vrmRenderers.Length; i++)
            rendArr.GetArrayElementAtIndex(i).objectReferenceValue = vrmRenderers[i];
        fadeSo.ApplyModifiedProperties();

        var companionCtrl = avatarRoot.AddComponent<AvatarCompanionController>();

        // Wire serialized properties on companionCtrl
        var ctrlSo = new SerializedObject(companionCtrl);
        ctrlSo.FindProperty("visualRoot").objectReferenceValue = visualModel;
        ctrlSo.FindProperty("animator").objectReferenceValue = animator;
        ctrlSo.FindProperty("autoSpawnOnStart").boolValue = true;
        ctrlSo.FindProperty("lockToFloor").boolValue = true;
        ctrlSo.FindProperty("floorHeightY").floatValue = 0.0f;
        ctrlSo.FindProperty("lookAtController").objectReferenceValue = lookAt;
        ctrlSo.FindProperty("safetyFade").objectReferenceValue = safetyFade;
        ctrlSo.ApplyModifiedProperties();

        // 6. Buat Canvas UI Sandbox
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
        panelGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.88f);

        // Status Text
        var txtStatusGo = new GameObject("Txt_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtStatusGo.transform.SetParent(panelGo.transform, false);
        var statusRect = txtStatusGo.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 60f);
        statusRect.sizeDelta = new Vector2(460f, 30f);
        var txtStatus = txtStatusGo.GetComponent<TextMeshProUGUI>();
        txtStatus.text = "State: <b>Idle</b> (VRM Active)";
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
        txtDist.text = "Jarak Kamera: 1.80 m (Aman)";
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
        
        var so = new SerializedObject(sandboxUI);
        so.FindProperty("companionController").objectReferenceValue = companionCtrl;
        so.FindProperty("btnSpawn").objectReferenceValue = btnSpawn;
        so.FindProperty("btnPoint").objectReferenceValue = btnPoint;
        so.FindProperty("btnDismiss").objectReferenceValue = btnDismiss;
        so.FindProperty("txtStatus").objectReferenceValue = txtStatus;
        so.FindProperty("txtDistance").objectReferenceValue = txtDist;
        so.ApplyModifiedProperties();

        // 7. Simpan Scene
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
