using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInfoPopup : MonoBehaviour
{
    public static PlayerInfoPopup instance;

    [Header("UI References")]
    public GameObject popupPanel;
    public TMP_Text playerNameText;
    public TMP_Text distanceText;
    public Button navigateButton;
    public Button closeButton;

    private PlayerSync currentPlayerSync;
    private Camera arCamera;

    private void Awake()
    {
        if (instance == null) instance = this;

        if (navigateButton != null)
        {
            navigateButton.onClick.AddListener(OnNavigateClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void Start()
    {
        arCamera = Camera.main;
    }

    private void Update()
    {
        if (popupPanel != null && popupPanel.activeSelf && currentPlayerSync != null)
        {
            UpdateDistanceText();
        }
    }

    public void Show(PlayerSync player)
    {
        currentPlayerSync = player;
        
        if (playerNameText != null)
        {
            playerNameText.text = player.photonView.Owner != null ? player.photonView.Owner.NickName : "Unknown Player";
        }

        UpdateDistanceText();

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
        currentPlayerSync = null;
    }

    public void OnNavigateClicked()
    {
        if (currentPlayerSync == null) return;
        
        PlayerNavigationController.instance.NavigateToPlayer(
            currentPlayerSync.transform,
            currentPlayerSync.photonView.Owner.NickName
        );
        
        Hide();
    }

    public void OnCloseClicked()
    {
        Hide();
    }

    private void UpdateDistanceText()
    {
        if (distanceText == null || arCamera == null || currentPlayerSync == null) return;

        Vector3 myPos = arCamera.transform.position;
        Vector3 targetPos = currentPlayerSync.transform.position;
        
        // Use horizontal distance
        myPos.y = 0;
        targetPos.y = 0;

        float distance = Vector3.Distance(myPos, targetPos);
        distanceText.text = $"Jarak: {distance:F1} m";
    }
}
