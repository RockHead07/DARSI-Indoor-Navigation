using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
using MultiSet;

public class PlayerNavigationController : MonoBehaviourPunCallbacks
{
    public static PlayerNavigationController instance;

    [Header("Navigation State")]
    public bool isNavigatingToPlayer = false;
    public string currentTargetPlayerName = "";
    private Transform playerTarget;

    [Header("Settings")]
    public float arrivalDistance = 1.5f;

    [Header("Events")]
    public UnityEvent OnArrived;

    private Camera arCamera;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        arCamera = Camera.main;
    }

    public void NavigateToPlayer(Transform playerCapsuleTransform, string playerName)
    {
        if (NavigationControllerExtension.instance == null)
        {
            Debug.LogError("NavigationControllerExtension instance is null! " +
                "Make sure NavigationControllerExtension script is attached " +
                "to a GameObject in the scene.");
            return;
        }
        
        if (NavigationUIExtension.instance == null)
        {
            // Try to find it in scene before giving up
            NavigationUIExtension found = FindObjectOfType<NavigationUIExtension>();
            if (found != null)
            {
                Debug.LogWarning("NavigationUIExtension instance was null, " +
                    "found via FindObjectOfType");
            }
            else
            {
                Debug.LogError("NavigationUIExtension instance is null! " +
                    "Cannot find it in scene either.");
                return;
            }
        }

        currentTargetPlayerName = playerName;
        playerTarget = playerCapsuleTransform;
        isNavigatingToPlayer = true;
        
        NavigationControllerExtension.instance.SetTransformForNavigation(playerCapsuleTransform);
        NavigationUIExtension.instance.StartPlayerNavigation(playerName);
    }

    public void StopNavigation()
    {
        isNavigatingToPlayer = false;
        playerTarget = null;
        currentTargetPlayerName = "";

        if (NavigationControllerExtension.instance != null)
        {
            NavigationControllerExtension.instance.StopPlayerNavigation();
        }

        NavigationUIExtension.instance.StopPlayerNavigation();
    }

    public float GetDistanceToTarget()
    {
        if (playerTarget == null || arCamera == null) return 0f;

        Vector3 myPos = arCamera.transform.position;
        Vector3 targetPos = playerTarget.position;
        
        // Use horizontal distance
        myPos.y = 0;
        targetPos.y = 0;

        return Vector3.Distance(myPos, targetPos);
    }

    private void Update()
    {
        if (isNavigatingToPlayer)
        {
            if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy)
            {
                StopNavigation();
                return;
            }
            
            float distance = GetDistanceToTarget();
            if (distance <= arrivalDistance)
            {
                // We have arrived
                OnArrived?.Invoke();
                StopNavigation();
                
                // Show arrival toast like MultiSet does for POI
                if (ToastManager.Instance != null)
                {
                    ToastManager.Instance.ShowAlert(
                        "Kamu sudah sampai di lokasi " + currentTargetPlayerName + "!");
                }
            }
        }
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        if (!isNavigatingToPlayer || playerTarget == null) return;
        
        // Check if the player we're navigating to has left
        PlayerSync targetSync = playerTarget.GetComponentInParent<PlayerSync>();
        if (targetSync != null && !targetSync.photonView.IsMine)
        {
            if (targetSync.photonView.Owner == otherPlayer)
            {
                StopNavigation();
                Debug.Log("Target player left the room. Navigation stopped.");
                // Optional: show toast/notification to user
            }
        }
    }
}
