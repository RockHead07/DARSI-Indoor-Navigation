using UnityEngine;
using UnityEngine.AI;

public class NavigationControllerExtension : MonoBehaviour
{
    public static NavigationControllerExtension instance;

    public Transform playerTarget;
    public bool isNavigatingToPlayer;

    private NavigationController navController;
    private GameObject _navMeshProxy;
    private Vector3 _lastProxyPosition;
    private float _pathRecalcThreshold = 0.3f;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        navController = NavigationController.instance;
    }

    private void Update()
    {
        if (isNavigatingToPlayer && playerTarget != null && 
            navController != null && navController.agent != null)
        {
            NavMeshHit hit;
            Vector3 targetPos = playerTarget.position;
            targetPos.y = 0f;
            
            if (NavMesh.SamplePosition(targetPos, out hit, 2f, NavMesh.AllAreas))
            {
                navController.agent.destination = hit.position;
                
                // Update proxy position to follow player
                if (_navMeshProxy != null)
                {
                    _navMeshProxy.transform.position = hit.position;
                    
                    if (Vector3.Distance(_navMeshProxy.transform.position, _lastProxyPosition) > _pathRecalcThreshold)
                    {
                        _lastProxyPosition = _navMeshProxy.transform.position;
                        // Force ShowPath recalculation by re-setting destination
                        if (ShowPath.instance != null)
                        {
                            ShowPath.instance.SetPositionTo(_navMeshProxy.transform);
                        }
                    }
                }
            }
        }
    }

    public void SetTransformForNavigation(Transform targetTransform)
    {
        if (navController == null) navController = NavigationController.instance;
        
        if (navController != null)
            navController.StopNavigation();
        
        playerTarget = targetTransform;
        isNavigatingToPlayer = true;
        
        if (navController?.agent != null && ShowPath.instance != null)
        {
            // Sample nearest NavMesh position for the target
            NavMeshHit hit;
            Vector3 targetPos = playerTarget.position;
            targetPos.y = 0f; // force floor level
            
            if (NavMesh.SamplePosition(targetPos, out hit, 2f, NavMesh.AllAreas))
            {
                // Use sampled NavMesh position instead of raw capsule position
                navController.agent.destination = hit.position;
                ShowPath.instance.SetPositionFrom(navController.agent.transform);
                
                // Create or reuse a proxy GameObject on NavMesh
                if (_navMeshProxy == null)
                {
                    _navMeshProxy = new GameObject("NavMeshProxy");
                }
                _navMeshProxy.transform.position = hit.position;
                
                // Reset PathEstimationUtils to prevent null reference
                if (PathEstimationUtils.instance != null)
                {
                    PathEstimationUtils.instance.ResetEstimation();
                }
                
                ShowPath.instance.SetPositionTo(_navMeshProxy.transform);
                
                // After NavMesh sample successful, set dummy destination to prevent crash
                POI[] allPOIs = FindObjectsOfType<POI>();
                if (allPOIs.Length > 0 && PathEstimationUtils.instance != null)
                {
                    // Use reflection to set private destination field
                    var field = typeof(PathEstimationUtils)
                        .GetField("destination", 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(PathEstimationUtils.instance, allPOIs[0]);
                        
                        // Also set destinationColliderRadius to prevent radius crash
                        var radiusField = typeof(PathEstimationUtils)
                            .GetField("destinationColliderRadius",
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance);
                        if (radiusField != null)
                        {
                            radiusField.SetValue(PathEstimationUtils.instance, 0f);
                        }
                    }
                }
                
                Debug.Log($"NavMesh sample successful at {hit.position}");
            }
            else
            {
                Debug.LogError($"NavMesh.SamplePosition failed for position {targetPos}. " +
                    "Target may be outside NavMesh area.");
                // Fallback: use raw position
                navController.agent.destination = targetPos;
                ShowPath.instance.SetPositionFrom(navController.agent.transform);
                ShowPath.instance.SetPositionTo(playerTarget);
            }
        }
    }

    public void StopPlayerNavigation()
    {
        isNavigatingToPlayer = false;
        playerTarget = null;
        
        if (_navMeshProxy != null)
        {
            Destroy(_navMeshProxy);
            _navMeshProxy = null;
        }
        
        if (ShowPath.instance != null)
            ShowPath.instance.ResetPath();
        
        if (PathEstimationUtils.instance != null)
            PathEstimationUtils.instance.ResetEstimation();
    }
}
