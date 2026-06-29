using UnityEngine;
using UnityEngine.UI;

public class NavigationUIExtension : MonoBehaviour
{
    public static NavigationUIExtension instance;
    private NavigationUIController _cachedNavUI;
    private float _startingDistance = 0f;
    private bool _estimationStarted = false;
    
    private void Awake()
    {
        if (instance == null) 
        {
            instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }
    }
    
    private void Start()
    {
        // Cache once at start instead of every frame
        _cachedNavUI = FindObjectOfType<NavigationUIController>();
        if (_cachedNavUI == null)
        {
            Debug.LogWarning("NavigationUIExtension: NavigationUIController not found!");
            return;
        }
        
        // Hook into stop button to intercept during player navigation
        if (_cachedNavUI.stopButton != null)
        {
            // Use true to ensure it finds the button even if stopButton is currently inactive
            Button stopBtn = _cachedNavUI.stopButton.GetComponentInChildren<Button>(true);
            if (stopBtn != null)
            {
                stopBtn.onClick.AddListener(OnStopButtonClicked);
            }
        }
    }

    private void OnStopButtonClicked()
    {
        // If navigating to player, stop player navigation
        if (NavigationControllerExtension.instance != null && 
            NavigationControllerExtension.instance.isNavigatingToPlayer)
        {
            if (PlayerNavigationController.instance != null)
            {
                PlayerNavigationController.instance.StopNavigation();
            }
        }
    }
    
    private void Update()
    {
        if (NavigationControllerExtension.instance != null && 
            NavigationControllerExtension.instance.isNavigatingToPlayer)
        {
            if (_cachedNavUI != null && PlayerNavigationController.instance != null)
            {
                float distance = PlayerNavigationController.instance.GetDistanceToTarget();
                
                // Update distance text
                if (_cachedNavUI.remainingDistance != null)
                    _cachedNavUI.remainingDistance.text = 
                        distance.ToString("F0") + " m remaining";
                
                // Update progress slider manually
                if (_cachedNavUI.navigationProgressSlider != null)
                {
                    Slider slider = _cachedNavUI.navigationProgressSlider
                        .GetComponentInChildren<Slider>();
                        
                    if (slider != null)
                    {
                        // Initialize starting distance
                        if (!_estimationStarted || distance > _startingDistance)
                        {
                            _startingDistance = distance;
                            _estimationStarted = true;
                        }
                        
                        if (_startingDistance > 0)
                        {
                            float progress = (_startingDistance - distance) / _startingDistance;
                            slider.value = Mathf.Clamp01(progress + 0.03f);
                        }
                    }
                }
                
                // Update _PathLength material property to prevent stretched arrows
                var showPath = ShowPath.instance;
                if (showPath != null)
                {
                    var lineRenderer = showPath.GetComponent<LineRenderer>();
                    if (lineRenderer != null && lineRenderer.material != null)
                    {
                        lineRenderer.material.SetFloat("_PathLength", distance);
                    }
                }
            }
        }
    }
    
    public void StartPlayerNavigation(string playerName)
    {
        if (_cachedNavUI == null) 
            _cachedNavUI = FindObjectOfType<NavigationUIController>();
            
        if (_cachedNavUI != null)
        {
            _cachedNavUI.navigationProgressSlider.SetActive(true);
            _cachedNavUI.stopButton.SetActive(true);
        }
        else
        {
            Debug.LogError("NavigationUIController not found in scene!");
        }
    }
    
    public void StopPlayerNavigation()
    {
        _estimationStarted = false;
        _startingDistance = 0f;
        
        if (_cachedNavUI == null)
            _cachedNavUI = FindObjectOfType<NavigationUIController>();
            
        if (_cachedNavUI != null)
        {
            _cachedNavUI.navigationProgressSlider.SetActive(false);
            _cachedNavUI.stopButton.SetActive(false);
        }
    }
}
