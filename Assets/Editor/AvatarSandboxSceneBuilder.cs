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
    private const string VRMPath = "Assets/3d-Models-Char-VRM/AvatarSample_A.vrm";

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

        Transform headTransform = null;
        var visualModel = CreateStylizedAssistantVisual(avatarRoot.transform, out headTransform);

        // Pasang VRMRuntimeLoader untuk memuat AvatarSample_A.vrm saat Play Mode
        var vrmLoader = avatarRoot.AddComponent<VRMRuntimeLoader>();
        var vrmSo = new SerializedObject(vrmLoader);
        vrmSo.FindProperty("vrmRelativePath").stringValue = VRMPath;
        vrmSo.FindProperty("vrmRotationOffset").vector3Value = new Vector3(0, 180f, 0);
        vrmSo.FindProperty("fallbackVisual").objectReferenceValue = visualModel;
        vrmSo.ApplyModifiedProperties();

        // Pasang Controller & Look-At ke Head Bone
        var lookAt = avatarRoot.AddComponent<AvatarLookAtController>();
        if (headTransform != null)
        {
            var lookAtSo = new SerializedObject(lookAt);
            lookAtSo.FindProperty("headBone").objectReferenceValue = headTransform;
            lookAtSo.ApplyModifiedProperties();
        }

        var safetyFade = avatarRoot.AddComponent<AvatarSafetyFade>();
        safetyFade.CacheRenderers();

        var companionCtrl = avatarRoot.AddComponent<AvatarCompanionController>();

        // Wire serialized properties on companionCtrl
        var ctrlSo = new SerializedObject(companionCtrl);
        ctrlSo.FindProperty("visualRoot").objectReferenceValue = visualModel;
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

    private static GameObject CreateStylizedAssistantVisual(Transform parent, out Transform headTransform)
    {
        var visualModel = new GameObject("Model_Visual_Stylized");
        visualModel.transform.SetParent(parent, false);
        visualModel.transform.localPosition = Vector3.zero;
        visualModel.transform.localRotation = Quaternion.identity;

        var whiteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        whiteMat.color = new Color(0.95f, 0.95f, 0.98f);

        var medicalTealMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        medicalTealMat.color = new Color(0.0f, 0.65f, 0.60f);

        var darkFootMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        darkFootMat.color = new Color(0.15f, 0.18f, 0.22f);

        var visorGlowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        visorGlowMat.color = new Color(0.1f, 0.85f, 1.0f);
        if (visorGlowMat.HasProperty("_EmissionColor"))
        {
            visorGlowMat.EnableKeyword("_EMISSION");
            visorGlowMat.SetColor("_EmissionColor", new Color(0.1f, 0.85f, 1.0f) * 1.5f);
        }

        // Kaki Kiri (Menyentuh lantai Y = 0)
        var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftLeg.name = "Left_Leg";
        leftLeg.transform.SetParent(visualModel.transform, false);
        leftLeg.transform.localPosition = new Vector3(-0.16f, 0.25f, 0);
        leftLeg.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
        leftLeg.GetComponent<MeshRenderer>().sharedMaterial = darkFootMat;
        Object.DestroyImmediate(leftLeg.GetComponent<Collider>());

        // Kaki Kanan (Menyentuh lantai Y = 0)
        var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightLeg.name = "Right_Leg";
        rightLeg.transform.SetParent(visualModel.transform, false);
        rightLeg.transform.localPosition = new Vector3(0.16f, 0.25f, 0);
        rightLeg.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
        rightLeg.GetComponent<MeshRenderer>().sharedMaterial = darkFootMat;
        Object.DestroyImmediate(rightLeg.GetComponent<Collider>());

        // Badan / Torso (Capsule berdiri di atas kaki, Y = 0.90)
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body_Torso";
        body.transform.SetParent(visualModel.transform, false);
        body.transform.localPosition = new Vector3(0, 0.90f, 0);
        body.transform.localScale = new Vector3(0.55f, 0.50f, 0.45f);
        body.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // Sabuk / Strip Medis (Cylinder)
        var belt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        belt.name = "Medical_Belt";
        belt.transform.SetParent(body.transform, false);
        belt.transform.localPosition = Vector3.zero;
        belt.transform.localScale = new Vector3(1.05f, 0.15f, 1.05f);
        belt.GetComponent<MeshRenderer>().sharedMaterial = medicalTealMat;
        Object.DestroyImmediate(belt.GetComponent<Collider>());

        // Kepala (Head Bone untuk Look-At Tracking pada Y = 1.45)
        var headBone = new GameObject("Head_Bone");
        headBone.transform.SetParent(visualModel.transform, false);
        headBone.transform.localPosition = new Vector3(0, 1.45f, 0);
        headTransform = headBone.transform;

        var headMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headMesh.name = "Head_Mesh";
        headMesh.transform.SetParent(headBone.transform, false);
        headMesh.transform.localPosition = Vector3.zero;
        headMesh.transform.localScale = new Vector3(0.46f, 0.50f, 0.46f);
        headMesh.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
        Object.DestroyImmediate(headMesh.GetComponent<Collider>());

        // Visor Mata Glowing
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor_Eyes";
        visor.transform.SetParent(headBone.transform, false);
        visor.transform.localPosition = new Vector3(0, 0.05f, 0.21f);
        visor.transform.localScale = new Vector3(0.34f, 0.12f, 0.15f);
        visor.GetComponent<MeshRenderer>().sharedMaterial = visorGlowMat;
        Object.DestroyImmediate(visor.GetComponent<Collider>());

        // Topi Medis / Nurse Cap (Cube tipis)
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "Nurse_Cap";
        cap.transform.SetParent(headBone.transform, false);
        cap.transform.localPosition = new Vector3(0, 0.25f, -0.05f);
        cap.transform.localScale = new Vector3(0.30f, 0.10f, 0.22f);
        cap.GetComponent<MeshRenderer>().sharedMaterial = medicalTealMat;
        Object.DestroyImmediate(cap.GetComponent<Collider>());

        // Tangan Kiri
        var leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftArm.name = "Left_Arm";
        leftArm.transform.SetParent(visualModel.transform, false);
        leftArm.transform.localPosition = new Vector3(-0.36f, 0.88f, 0);
        leftArm.transform.localScale = new Vector3(0.15f, 0.40f, 0.15f);
        leftArm.transform.localRotation = Quaternion.Euler(0, 0, -10f);
        leftArm.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
        Object.DestroyImmediate(leftArm.GetComponent<Collider>());

        // Tangan Kanan
        var rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rightArm.name = "Right_Arm";
        rightArm.transform.SetParent(visualModel.transform, false);
        rightArm.transform.localPosition = new Vector3(0.36f, 0.88f, 0);
        rightArm.transform.localScale = new Vector3(0.15f, 0.40f, 0.15f);
        rightArm.transform.localRotation = Quaternion.Euler(0, 0, 10f);
        rightArm.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
        Object.DestroyImmediate(rightArm.GetComponent<Collider>());

        return visualModel;
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
