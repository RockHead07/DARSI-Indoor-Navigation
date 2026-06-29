using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class RebuildFriendListUI
{
    [MenuItem("Tools/UI/Rebuild FriendList UI")]
    public static void Rebuild()
    {
        RebuildEntryPrefab();
        Debug.Log("[RebuildFriendListUI] Done!");
        EditorUtility.DisplayDialog("Done", "FriendListEntry prefab rebuilt!\n\nJangan lupa assign ulang:\n- FriendListEntry.avatarImage → Avatar\n- FriendListEntry.initialsText → Initials", "OK");
    }

    static RectTransform RT(GameObject go) =>
        go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void RebuildEntryPrefab()
    {
        const string path = "Assets/Prefabs/FriendListEntry.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogError("Prefab not found: " + path); return; }

        using var scope = new PrefabUtility.EditPrefabContentsScope(path);
        var root = scope.prefabContentsRoot;

        // Clear old children
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        // ── Root: dark card ────────────────────────────────────────────────
        var rootImg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        rootImg.color = new Color32(28, 28, 42, 255);

        var hlg = root.GetComponent<HorizontalLayoutGroup>() ?? root.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 12, 12);
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth  = false;
        hlg.childControlHeight = false;

        var rootLE = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
        rootLE.preferredHeight = 72;
        rootLE.flexibleWidth   = 1;

        var rootRT = RT(root);
        rootRT.sizeDelta = new Vector2(0, 72);

        // ── Avatar circle ─────────────────────────────────────────────────
        var avatarGO  = Child("Avatar", root.transform);
        var avatarRT  = RT(avatarGO);
        avatarRT.sizeDelta = new Vector2(46, 46);

        var avatarImg = avatarGO.AddComponent<Image>();
        avatarImg.color  = new Color32(52, 115, 217, 255);
        avatarImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var avatarLE = avatarGO.AddComponent<LayoutElement>();
        avatarLE.preferredWidth  = 46;
        avatarLE.preferredHeight = 46;
        avatarLE.minWidth        = 46;
        avatarLE.minHeight       = 46;

        // Initials label on avatar
        var initialsGO = Child("Initials", avatarGO.transform);
        var initialsRT = RT(initialsGO);
        initialsRT.anchorMin = Vector2.zero;
        initialsRT.anchorMax = Vector2.one;
        initialsRT.offsetMin = Vector2.zero;
        initialsRT.offsetMax = Vector2.zero;
        var initTMP = initialsGO.AddComponent<TextMeshProUGUI>();
        initTMP.text      = "AR";
        initTMP.fontSize  = 15;
        initTMP.fontStyle = FontStyles.Bold;
        initTMP.color     = Color.white;
        initTMP.alignment = TextAlignmentOptions.Center;

        // ── Info column (name + distance) ─────────────────────────────────
        var infoGO = Child("Info", root.transform);
        RT(infoGO).sizeDelta = new Vector2(120, 48);

        var vlg = infoGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.MiddleLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight   = true;
        vlg.spacing = 3;

        var infoLE = infoGO.AddComponent<LayoutElement>();
        infoLE.flexibleWidth   = 1;
        infoLE.preferredHeight = 48;

        // Name
        var nameGO  = Child("PlayerName", infoGO.transform);
        RT(nameGO);
        var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Player Name";
        nameTMP.fontSize  = 15;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color     = Color.white;
        nameTMP.alignment = TextAlignmentOptions.Left;
        var nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 22;

        // Distance
        var distGO  = Child("Distance", infoGO.transform);
        RT(distGO);
        var distTMP  = distGO.AddComponent<TextMeshProUGUI>();
        distTMP.text      = "± 0 m";
        distTMP.fontSize  = 12;
        distTMP.color     = new Color(0.65f, 0.65f, 0.65f, 1f);
        distTMP.alignment = TextAlignmentOptions.Left;
        var distLE = distGO.AddComponent<LayoutElement>();
        distLE.preferredHeight = 18;

        // ── Navigate button ───────────────────────────────────────────────
        var btnGO = Child("NavigateButton", root.transform);
        RT(btnGO).sizeDelta = new Vector2(108, 44);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.07f);

        var btnOutline = btnGO.AddComponent<Outline>();
        btnOutline.effectColor    = new Color(1f, 1f, 1f, 0.4f);
        btnOutline.effectDistance = new Vector2(1, 1);

        var btn       = btnGO.AddComponent<Button>();
        var btnColors = btn.colors;
        btnColors.normalColor      = new Color(1, 1, 1, 0.07f);
        btnColors.highlightedColor = new Color(1, 1, 1, 0.18f);
        btnColors.pressedColor     = new Color(1, 1, 1, 0.28f);
        btn.colors       = btnColors;
        btn.targetGraphic = btnImg;

        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth  = 108;
        btnLE.preferredHeight = 44;
        btnLE.minWidth        = 108;

        // Button label
        var btnTextGO = Child("Label", btnGO.transform);
        var btnTextRT = RT(btnTextGO);
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;
        var btnTMP  = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnTMP.text      = "△ Navigasi";
        btnTMP.fontSize  = 13;
        btnTMP.color     = Color.white;
        btnTMP.alignment = TextAlignmentOptions.Center;

        // ── Wire FriendListEntry component ────────────────────────────────
        var entry = root.GetComponent<FriendListEntry>() ?? root.AddComponent<FriendListEntry>();
        entry.playerNameText  = nameTMP;
        entry.distanceText    = distTMP;
        entry.navigateButton  = btn;

        Debug.Log("[RebuildFriendListUI] FriendListEntry prefab rebuilt.");
    }
}
