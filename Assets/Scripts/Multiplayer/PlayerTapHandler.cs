using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerTapHandler : MonoBehaviour
{
    private Camera arCamera;

    private void Start()
    {
        arCamera = Camera.main;
    }

    private void Update()
    {
        if (arCamera == null) return;

        Vector2 inputPosition = Vector2.zero;
        bool inputDetected = false;

        // Android touch
        if (Touchscreen.current != null && 
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            inputPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            inputDetected = true;
        }
        // Editor mouse click
        else if (Mouse.current != null && 
                 Mouse.current.leftButton.wasPressedThisFrame)
        {
            inputPosition = Mouse.current.position.ReadValue();
            inputDetected = true;
        }

        if (!inputDetected) return;

        // Prevent raycast if tapping on UI
        if (EventSystem.current != null && 
            EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = arCamera.ScreenPointToRay(inputPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            PlayerSync playerSync = hit.collider.GetComponentInParent<PlayerSync>();
            
            if (playerSync != null && !playerSync.photonView.IsMine)
            {
                if (PlayerInfoPopup.instance != null)
                {
                    PlayerInfoPopup.instance.Show(playerSync);
                }
                else
                {
                    Debug.LogWarning("PlayerInfoPopup instance is missing!");
                }
            }
        }
    }
}
