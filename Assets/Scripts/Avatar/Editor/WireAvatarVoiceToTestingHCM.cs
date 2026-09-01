using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRM;

/// <summary>
/// Skrip Editor satu-kali untuk menambahkan komponen Voice/Lip-Sync Fase 2
/// (AvatarAudioClient, AvatarSpeechLipSync, uLipSync, AudioSource) ke GameObject
/// Avatar_Guide yang SUDAH ADA di scene TestingHCM.unity.
///
/// KENAPA skrip ini diperlukan:
/// Komponen suara dan lip-sync selama ini hanya ada di Sandbox_AvatarCompanion.unity
/// (terisolasi, tanpa VPS/MultiSet). Untuk tes lapangan pertama (ADR-034 keputusan 7),
/// komponen tersebut perlu dipasang di scene navigasi sungguhan yang memiliki ShowPath,
/// NavigationController, dan FloorTransitionController.
///
/// Pola wiring: sama persis dengan AvatarSandboxSceneBuilder.cs baris 125-162,
/// cuma targetnya Avatar_Guide di TestingHCM, bukan Avatar_Companion di sandbox.
///
/// Menu: DARSI > Avatar > Wire Voice to TestingHCM / Tools > Avatar > Wire Voice to TestingHCM.
/// </summary>
public static class WireAvatarVoiceToTestingHCM
{
    private const string TestingHCMPath =
        "Assets/Samples/MultiSet-SDK/1.11.5/Sample Scenes/Navigation/TestingHCM.unity";

    private const string ProfilePathFemale =
        "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
    private const string ProfilePathGeneric =
        "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset";

    [MenuItem("DARSI/Avatar/Wire Voice to TestingHCM")]
    [MenuItem("Tools/Avatar/Wire Voice to TestingHCM")]
    public static void Execute()
    {
        // 1. Buka scene TestingHCM (minta simpan scene aktif terlebih dahulu)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[WireVoice] Dibatalkan oleh pengguna.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TestingHCMPath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[WireVoice] Gagal membuka scene: {TestingHCMPath}");
            return;
        }

        // 2. Cari Avatar_Guide yang sudah ada
        var avatarGuide = GameObject.Find("Avatar_Guide");
        if (avatarGuide == null)
        {
            Debug.LogError("[WireVoice] GameObject 'Avatar_Guide' tidak ditemukan di TestingHCM. " +
                           "Pastikan scene sudah memiliki hierarki avatar dari sesi sebelumnya.");
            return;
        }

        var guideCtrl = avatarGuide.GetComponent<AIAvatarGuideController>();
        if (guideCtrl == null)
        {
            Debug.LogError("[WireVoice] AIAvatarGuideController tidak ditemukan pada Avatar_Guide.");
            return;
        }

        // 3. Cek apakah sudah pernah di-wire (idempoten)
        if (avatarGuide.GetComponent<AvatarAudioClient>() != null)
        {
            Debug.LogWarning("[WireVoice] AvatarAudioClient sudah terpasang di Avatar_Guide. " +
                             "Tidak menambahkan ulang. Hapus manual jika ingin meng-ulang wiring.");
            return;
        }

        // 4. Ambil referensi model VRM child (Animator dan VRMBlendShapeProxy)
        var animator = avatarGuide.GetComponentInChildren<Animator>();
        var blendShapeProxy = avatarGuide.GetComponentInChildren<VRMBlendShapeProxy>();

        if (animator == null)
            Debug.LogWarning("[WireVoice] Animator tidak ditemukan di child Avatar_Guide. " +
                             "Lip-sync mungkin tidak berfungsi tanpa model VRM.");
        if (blendShapeProxy == null)
            Debug.LogWarning("[WireVoice] VRMBlendShapeProxy tidak ditemukan di child Avatar_Guide. " +
                             "Lip-sync akan menggunakan fallback RMS.");

        // 5. Tambahkan AudioSource
        var audioSource = avatarGuide.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D audio (langsung ke speaker, bukan spatialized)
        Debug.Log("[WireVoice] AudioSource ditambahkan.");

        // 6. Tambahkan uLipSync dengan profil suara perempuan
        var uLipSyncComp = avatarGuide.AddComponent<uLipSync.uLipSync>();
        var profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(ProfilePathFemale) ??
                      AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(ProfilePathGeneric);
        if (profile != null)
        {
            uLipSyncComp.profile = profile;
            Debug.Log($"[WireVoice] uLipSync ditambahkan dengan profil: {profile.name}");
        }
        else
        {
            Debug.LogWarning("[WireVoice] Profil uLipSync tidak ditemukan. " +
                             "Lip-sync akan menggunakan fallback RMS prosedural.");
        }

        // 7. Tambahkan AvatarSpeechLipSync dan hubungkan referensi
        var lipSyncDriver = avatarGuide.AddComponent<AvatarSpeechLipSync>();
        var lipSo = new SerializedObject(lipSyncDriver);
        lipSo.FindProperty("blendShapeProxy").objectReferenceValue = blendShapeProxy;
        lipSo.FindProperty("audioSource").objectReferenceValue = audioSource;
        lipSo.FindProperty("lipSync").objectReferenceValue = uLipSyncComp;
        lipSo.ApplyModifiedProperties();
        Debug.Log("[WireVoice] AvatarSpeechLipSync ditambahkan dan dihubungkan.");

        // 8. Tambahkan AvatarAudioClient dan hubungkan referensi
        var audioClient = avatarGuide.AddComponent<AvatarAudioClient>();
        var audioClientSo = new SerializedObject(audioClient);
        audioClientSo.FindProperty("baseUrl").stringValue = "https://api-darsi.rockhead07.tech";
        audioClientSo.FindProperty("lipSyncDriver").objectReferenceValue = lipSyncDriver;
        audioClientSo.FindProperty("audioSource").objectReferenceValue = audioSource;
        audioClientSo.FindProperty("guideController").objectReferenceValue = guideCtrl;
        audioClientSo.ApplyModifiedProperties();
        Debug.Log("[WireVoice] AvatarAudioClient ditambahkan dengan baseUrl Named Tunnel.");

        // 9. Simpan scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[WireVoice] ✓ Selesai. Komponen Voice/Lip-Sync berhasil dipasang di Avatar_Guide " +
                  $"pada scene {TestingHCMPath}.\n" +
                  "Komponen yang ditambahkan:\n" +
                  "  - AudioSource (playOnAwake=false, spatialBlend=0)\n" +
                  "  - uLipSync.uLipSync (profil Female/Generic)\n" +
                  "  - AvatarSpeechLipSync (terhubung ke VRMBlendShapeProxy + AudioSource + uLipSync)\n" +
                  "  - AvatarAudioClient (baseUrl=Named Tunnel, terhubung ke lipSyncDriver + guideController)\n\n" +
                  "AssistantClient akan menemukan AvatarAudioClient secara otomatis via FindAnyObjectByType di Awake().\n" +
                  "AIAvatarGuideController.showPath sudah terhubung ke ShowPath. " +
                  "navigation dan floorTransition di-resolve otomatis di Awake().");
    }
}
