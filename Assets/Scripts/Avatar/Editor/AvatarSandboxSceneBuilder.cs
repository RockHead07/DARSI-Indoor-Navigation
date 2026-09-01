using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using VRM;

/// <summary>
/// Tool otomatis untuk membuat dan menyiapkan Scene Sandbox Avatar Companion & Lip-Sync (Tahap 1 & Fase 2 - ADR-030 / ADR-037).
/// Menyiapkan AvatarSample_A.prefab dengan Look-At, Safety Fade, AudioSource, uLipSync, dan AvatarSpeechLipSync.
/// Menu: DARSI > Avatar > Setup Sandbox Scene / Tools > Avatar > Setup Sandbox Scene.
/// </summary>
public static class AvatarSandboxSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox_AvatarCompanion.unity";
    private const string VRMPrefabPath = "Assets/Avatar/Model/AvatarSample_A.prefab";
    private const string ProfilePathFemale = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
    private const string ProfilePathGeneric = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset";
    private const string ClipAIUEOPath = "Assets/Avatar/Audio/Sample_Voice_AIUEO.wav";
    private const string ClipGreetingPath = "Assets/Avatar/Audio/Sample_Voice_Greeting.wav";

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

        // Pasang prefab VRM hasil impor UniVRM (ADR-034 keputusan 3)
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
        var blendShapeProxy = visualModel.GetComponentInChildren<VRMBlendShapeProxy>();

        // Tulang kepala diambil dari peta Humanoid
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

        // Isi langsung dengan renderer VRM sesungguhnya (ADR-034 Amandemen 034-A)
        var vrmRenderers = visualModel.GetComponentsInChildren<Renderer>(true);
        var fadeSo = new SerializedObject(safetyFade);
        var rendArr = fadeSo.FindProperty("targetRenderers");
        rendArr.arraySize = vrmRenderers.Length;
        for (int i = 0; i < vrmRenderers.Length; i++)
            rendArr.GetArrayElementAtIndex(i).objectReferenceValue = vrmRenderers[i];
        fadeSo.ApplyModifiedProperties();

        // Pasang Audio & Lip-Sync Driver (Fase 2 - ADR-037)
        var audioSource = avatarRoot.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        var uLipSyncComp = avatarRoot.AddComponent<uLipSync.uLipSync>();
        var profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(ProfilePathFemale) ??
                      AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(ProfilePathGeneric);
        if (profile != null)
        {
            uLipSyncComp.profile = profile;
        }

        var lipSyncDriver = avatarRoot.AddComponent<AvatarSpeechLipSync>();
        var lipSo = new SerializedObject(lipSyncDriver);
        lipSo.FindProperty("blendShapeProxy").objectReferenceValue = blendShapeProxy;
        lipSo.FindProperty("audioSource").objectReferenceValue = audioSource;
        lipSo.FindProperty("lipSync").objectReferenceValue = uLipSyncComp;
        lipSo.ApplyModifiedProperties();

        var audioClient = avatarRoot.AddComponent<AvatarAudioClient>();
        var audioClientSo = new SerializedObject(audioClient);
        audioClientSo.FindProperty("lipSyncDriver").objectReferenceValue = lipSyncDriver;
        audioClientSo.FindProperty("audioSource").objectReferenceValue = audioSource;

        // Tambahkan AIAvatarGuideController ke scene sandbox agar probe
        // dapat memverifikasi bahwa StartLeading() sungguhan terpanggil.
        // Di scene produksi komponen ini sudah ada tersendiri; di sandbox
        // perlu ditambahkan eksplisit karena scene ini tidak punya ShowPath
        // atau NavigationController (itu tidak apa-apa, yang diuji hanya
        // transisi state FSM setelah SpeakAnswerAndGuide selesai).
        var guideCtrl = avatarRoot.AddComponent<AIAvatarGuideController>();
        var guideSo = new SerializedObject(guideCtrl);
        guideSo.FindProperty("animator").objectReferenceValue = animator;
        guideSo.ApplyModifiedProperties();

        audioClientSo.FindProperty("guideController").objectReferenceValue = guideCtrl;
        audioClientSo.ApplyModifiedProperties();

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
        panelRect.sizeDelta = new Vector2(560f, 240f);
        panelGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.92f);

        // Status Text
        var txtStatusGo = new GameObject("Txt_Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtStatusGo.transform.SetParent(panelGo.transform, false);
        var statusRect = txtStatusGo.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 95f);
        statusRect.sizeDelta = new Vector2(520f, 26f);
        var txtStatus = txtStatusGo.GetComponent<TextMeshProUGUI>();
        txtStatus.text = "State: <b>Idle</b> (VRM + Lip-Sync Active)";
        txtStatus.alignment = TextAlignmentOptions.Center;
        txtStatus.fontSize = 16;
        txtStatus.color = Color.white;

        // Distance Text
        var txtDistGo = new GameObject("Txt_Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtDistGo.transform.SetParent(panelGo.transform, false);
        var distRect = txtDistGo.GetComponent<RectTransform>();
        distRect.anchoredPosition = new Vector2(0, 70f);
        distRect.sizeDelta = new Vector2(520f, 24f);
        var txtDist = txtDistGo.GetComponent<TextMeshProUGUI>();
        txtDist.text = "Jarak Kamera: 1.80 m (Aman)";
        txtDist.alignment = TextAlignmentOptions.Center;
        txtDist.fontSize = 13;
        txtDist.color = new Color(0.8f, 0.8f, 0.8f);

        // Phoneme Diagnostic Text
        var txtPhonemeGo = new GameObject("Txt_Phoneme", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtPhonemeGo.transform.SetParent(panelGo.transform, false);
        var phonemeRect = txtPhonemeGo.GetComponent<RectTransform>();
        phonemeRect.anchoredPosition = new Vector2(0, 46f);
        phonemeRect.sizeDelta = new Vector2(520f, 24f);
        var txtPhoneme = txtPhonemeGo.GetComponent<TextMeshProUGUI>();
        txtPhoneme.text = "Bicara: <color=#888888>Diam</color> | Fonem: - | Vol: 0.00";
        txtPhoneme.alignment = TextAlignmentOptions.Center;
        txtPhoneme.fontSize = 13;
        txtPhoneme.color = new Color(0.7f, 0.9f, 1f);

        // Buttons Layout - Row 1 (Actions)
        var btnGroupActionsGo = new GameObject("Buttons_Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnGroupActionsGo.transform.SetParent(panelGo.transform, false);
        var bgActionsRect = btnGroupActionsGo.GetComponent<RectTransform>();
        bgActionsRect.anchoredPosition = new Vector2(0, 10f);
        bgActionsRect.sizeDelta = new Vector2(520f, 40f);
        var hlgActions = btnGroupActionsGo.GetComponent<HorizontalLayoutGroup>();
        hlgActions.spacing = 10;
        hlgActions.childControlWidth = true;
        hlgActions.childControlHeight = true;
        hlgActions.childForceExpandWidth = true;

        var btnSpawn = CreateButton("Btn_Spawn", "Panggil Avatar", btnGroupActionsGo.transform, new Color(0.15f, 0.55f, 0.35f));
        var btnPoint = CreateButton("Btn_Point", "Tunjuk Arah", btnGroupActionsGo.transform, new Color(0.2f, 0.45f, 0.7f));
        var btnDismiss = CreateButton("Btn_Dismiss", "Tutup", btnGroupActionsGo.transform, new Color(0.65f, 0.2f, 0.2f));

        // Buttons Layout - Row 2 (Voice & Lip-Sync)
        var btnGroupVoiceGo = new GameObject("Buttons_Voice", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnGroupVoiceGo.transform.SetParent(panelGo.transform, false);
        var bgVoiceRect = btnGroupVoiceGo.GetComponent<RectTransform>();
        bgVoiceRect.anchoredPosition = new Vector2(0, -38f);
        bgVoiceRect.sizeDelta = new Vector2(520f, 40f);
        var hlgVoice = btnGroupVoiceGo.GetComponent<HorizontalLayoutGroup>();
        hlgVoice.spacing = 10;
        hlgVoice.childControlWidth = true;
        hlgVoice.childControlHeight = true;
        hlgVoice.childForceExpandWidth = true;

        var btnPlayAIUEO = CreateButton("Btn_PlayAIUEO", "Uji AIUEO", btnGroupVoiceGo.transform, new Color(0.55f, 0.35f, 0.15f));
        var btnPlayGreeting = CreateButton("Btn_PlayGreeting", "Uji Sapaan RS", btnGroupVoiceGo.transform, new Color(0.45f, 0.25f, 0.65f));
        var btnTestBackendTTS = CreateButton("Btn_TestBackendTTS", "Uji TTS Backend", btnGroupVoiceGo.transform, new Color(0.15f, 0.45f, 0.75f));
        var btnStopVoice = CreateButton("Btn_StopVoice", "Stop Suara", btnGroupVoiceGo.transform, new Color(0.4f, 0.4f, 0.4f));

        var clipAIUEO = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipAIUEOPath);
        var clipGreeting = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipGreetingPath);

        var sandboxUI = canvasGo.AddComponent<AvatarSandboxUI>();
        
        var so = new SerializedObject(sandboxUI);
        so.FindProperty("companionController").objectReferenceValue = companionCtrl;
        so.FindProperty("lipSyncDriver").objectReferenceValue = lipSyncDriver;
        so.FindProperty("audioClient").objectReferenceValue = audioClient;
        so.FindProperty("btnSpawn").objectReferenceValue = btnSpawn;
        so.FindProperty("btnPoint").objectReferenceValue = btnPoint;
        so.FindProperty("btnDismiss").objectReferenceValue = btnDismiss;
        so.FindProperty("btnPlayAIUEO").objectReferenceValue = btnPlayAIUEO;
        so.FindProperty("btnPlayGreeting").objectReferenceValue = btnPlayGreeting;
        so.FindProperty("btnTestBackendTTS").objectReferenceValue = btnTestBackendTTS;
        so.FindProperty("btnStopVoice").objectReferenceValue = btnStopVoice;
        so.FindProperty("clipAIUEO").objectReferenceValue = clipAIUEO;
        so.FindProperty("clipGreeting").objectReferenceValue = clipGreeting;
        so.FindProperty("txtStatus").objectReferenceValue = txtStatus;
        so.FindProperty("txtDistance").objectReferenceValue = txtDist;
        so.FindProperty("txtPhoneme").objectReferenceValue = txtPhoneme;
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
        tmp.fontSize = 14;
        tmp.color = Color.white;

        return btnGo.GetComponent<Button>();
    }
}
