using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FriendListEntry : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerNameText;
    public TMP_Text distanceText;
    public TMP_Text initialsText;   // text on avatar circle
    public Image    avatarImage;    // the circle image
    public Button   navigateButton;

    // Avatar colors — cycles based on player name hash
    private static readonly Color32[] AvatarColors = {
        new Color32(52,  115, 217, 255), // blue
        new Color32(76,  175,  80, 255), // green
        new Color32(233,  30,  99, 255), // pink
        new Color32(156,  39, 176, 255), // purple
        new Color32(255, 152,   0, 255), // orange
        new Color32( 0,  188, 212, 255), // cyan
    };

    private PlayerSync targetPlayer;
    private Camera     arCamera;
    private string     pName;

    public void Setup(PlayerSync player, Camera cam)
    {
        targetPlayer = player;
        arCamera     = cam;
        pName        = (player.photonView.Owner != null)
                       ? player.photonView.Owner.NickName
                       : "Player";

        // Name label
        if (playerNameText != null)
            playerNameText.text = pName;
        else
            Debug.LogWarning("FriendListEntry: playerNameText is null!");

        // Avatar initials & color
        string initials = GetInitials(pName);
        if (initialsText != null)
            initialsText.text = initials;

        if (avatarImage != null)
            avatarImage.color = AvatarColors[Mathf.Abs(pName.GetHashCode()) % AvatarColors.Length];

        // Navigate button
        if (navigateButton != null)
        {
            navigateButton.onClick.RemoveAllListeners();
            navigateButton.onClick.AddListener(OnNavigateClicked);
        }
        else
            Debug.LogWarning("FriendListEntry: navigateButton is null!");

        if (distanceText == null)
            Debug.LogWarning("FriendListEntry: distanceText is null!");

        UpdateDistance();
    }

    public void UpdateDistance()
    {
        // AR camera may not have existed when this entry was created — re-acquire if needed
        if (arCamera == null) arCamera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();

        if (targetPlayer == null || arCamera == null || distanceText == null) return;

        Vector3 myPos     = arCamera.transform.position;
        Vector3 targetPos = targetPlayer.transform.position;
        myPos.y     = 0;
        targetPos.y = 0;

        float dist = Vector3.Distance(myPos, targetPos);
        distanceText.text = $"± {dist:F0} m";
    }

    private void OnNavigateClicked()
    {
        if (targetPlayer != null && PlayerNavigationController.instance != null)
        {
            PlayerNavigationController.instance.NavigateToPlayer(targetPlayer.transform, pName);
            if (FriendListPanel.instance != null)
                FriendListPanel.instance.TogglePanel();
        }
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        string[] parts = name.Trim().Split(' ');
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        return name.Length >= 2
            ? name.Substring(0, 2).ToUpper()
            : name.ToUpper();
    }
}
