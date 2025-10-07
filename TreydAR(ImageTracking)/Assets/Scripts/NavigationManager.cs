// NavigationManager.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Threading.Tasks;
using System.Collections;

public class NavigationManager : MonoBehaviour
{
    #region Inspector Fields
    [Header("Core Components")]
    public Camera arCamera;
    public DynamicArrowGuide arrowGuide;
    public FirebaseManager firebaseManager;

    [Header("AR Foundation")]
    public ARTrackedImageManager trackedImageManager;
    public ARAnchorManager anchorManager;

    [HideInInspector]
    public string targetImageName = "";

    [Header("Navigation Marker")]
    [Tooltip("How quickly the map corrects its position when recalibrating. Lower is smoother.")]
    [Range(0.01f, 0.2f)]
    public float recalibrationSmoothing = 0.1f;

    [Header("Multi-Target Setup")]
    [Tooltip("Define all your image targets and their corresponding anchor points within the map.")]
    public List<ImageTargetData> imageTargetDatabase;

    [Header("Environment Setup")]
    [Tooltip("Drag the DISABLED Environment PREFAB from your PROJECT folder here.")]
    public GameObject environmentSceneObject;
    public float environmentVerticalOffset = 0.0f;
    public GameObject ScanGuide_Image;
    public CanvasGroup scanGuideCanvasGroup;

    [Header("Navigation Control")]
    public TMP_Dropdown destinationDropdown;
    public float recalculateDistanceThreshold = 1.0f;
    public float updateInterval = 0.25f;
    [Tooltip("The distance (in meters) from the destination to trigger the 'arrived' message.")]
    public float arrivalDistanceThreshold = 1.5f;

    [Header("UI Feedback")]
    public TextMeshProUGUI statusText;
    public GameObject navigationUIParent;
    [Tooltip("The button that appears to let the user confirm the map's placement.")]
    public Button confirmPlacementButton;
    public Image recalibrationIndicator;
    public TextMeshProUGUI distanceText;

    [Header("Faculty Finder Integration")]
    public Button findFacultyButton;
    public FacultyFinder facultyFinderInstance;

    [Header("Events")]
    public UnityEvent OnNavigationStarted, OnNavigationStopped, OnEnvironmentInitialized;
    #endregion

    #region Private State
    private Transform environmentOriginTransform;
    private GameObject instantiatedEnvironment;
    private Quaternion userSpawnRotation = Quaternion.identity;
    private PathVisualizer instantiatedPathVisualizer;
    private Vector3 currentTargetPosition;
    private Vector3 lastPathCalculationPosition;
    private List<Vector3> currentPathCorners = new List<Vector3>();
    private List<PathPointData> availableDestinations = new List<PathPointData>();
    private int selectedDestinationIndex = 0;
    private float timeSinceLastUpdate = 0f;
    private bool navigationActive = false;
    private bool isInitialized = false;
    private bool isStudentModeActive = false;
    private NavMeshPath navMeshPath;
    private bool forcePathRecalculation = false;

    private ARTrackedImage detectedImageForPlacement;
    private ARAnchor currentAnchor;
    #endregion

    public bool IsInitialized()
    {
        return isInitialized;
    }

    void Awake()
    {
        if (trackedImageManager == null) trackedImageManager = FindObjectOfType<ARTrackedImageManager>();
        if (anchorManager == null) anchorManager = FindObjectOfType<ARAnchorManager>();
    }

    void Start()
    {
        if (firebaseManager == null) { Debug.LogError("FirebaseManager not assigned!", this); enabled = false; return; }

        if (confirmPlacementButton != null)
        {
            confirmPlacementButton.gameObject.SetActive(false);
            confirmPlacementButton.onClick.AddListener(OnConfirmPlacementButtonPressed);
        }
        else
        {
            Debug.LogError("Confirm Placement Button is not assigned in the Inspector!", this);
        }

        navMeshPath = new NavMeshPath();
        SetNavigationUIVisibility(false);
        if (destinationDropdown != null)
        {
            destinationDropdown.ClearOptions();
            destinationDropdown.options.Add(new TMP_Dropdown.OptionData("---"));
            destinationDropdown.interactable = false;
        }
        if (findFacultyButton != null && facultyFinderInstance != null)
        {
            findFacultyButton.onClick.AddListener(facultyFinderInstance.ShowFinder);
        }
    }

    private IEnumerator FadeOutScanGuide(float duration = 0.5f)
    {
        if (scanGuideCanvasGroup == null) yield break;
        float elapsed = 0f;
        float startAlpha = scanGuideCanvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            scanGuideCanvasGroup.alpha = newAlpha;
            yield return null;
        }

        scanGuideCanvasGroup.alpha = 0f;
        scanGuideCanvasGroup.interactable = false;
        scanGuideCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FlashIndicator()
    {
        if (recalibrationIndicator == null) yield break;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 4f;
            recalibrationIndicator.color = new Color(recalibrationIndicator.color.r, recalibrationIndicator.color.g, recalibrationIndicator.color.b, Mathf.Lerp(0, 1, t));
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 2f;
            recalibrationIndicator.color = new Color(recalibrationIndicator.color.r, recalibrationIndicator.color.g, recalibrationIndicator.color.b, Mathf.Lerp(1, 0, t));
            yield return null;
        }
    }

    public void OnConfirmPlacementButtonPressed()
    {
        if (detectedImageForPlacement != null && detectedImageForPlacement.trackingState == TrackingState.Tracking)
        {
            ImageTargetData targetData = imageTargetDatabase.FirstOrDefault(data => data.imageName == detectedImageForPlacement.referenceImage.name);

            if (targetData != null && targetData.anchorPointInMap != null)
            {
                InitializeEnvironmentLogic(new Pose(detectedImageForPlacement.transform.position, detectedImageForPlacement.transform.rotation), targetData);
                if (confirmPlacementButton != null)
                {
                    confirmPlacementButton.gameObject.SetActive(false);
                }

                if (ScanGuide_Image != null)
                {
                    StartCoroutine(FadeOutScanGuide());
                }
            }
            else
            {
                UpdateStatus($"Error: Scanned image '{detectedImageForPlacement.referenceImage.name}' is not defined in the database.");
            }
        }
        else
        {
            UpdateStatus("Target image lost. Please scan again.");
            if (confirmPlacementButton != null)
            {
                confirmPlacementButton.gameObject.SetActive(false);
            }
            detectedImageForPlacement = null;
        }
    }

    public void Test_InitializeEnvironment(string imageName)
    {
        if (isInitialized) return;
        Debug.Log($"--- EDITOR TEST: Simulating scan for '{imageName}' ---");
        ImageTargetData targetData = imageTargetDatabase.FirstOrDefault(data => data.imageName == imageName);
        if (targetData == null || targetData.anchorPointInMap == null)
        {
            Debug.LogError($"Test failed: Image name '{imageName}' not found in the database or its anchor point is null.");
            return;
        }

        Vector3 cameraForward = arCamera.transform.forward;
        cameraForward.y = 0;
        Quaternion flatRotation = Quaternion.LookRotation(cameraForward.normalized);
        Pose fakeRealWorldPose = new Pose(arCamera.transform.position + arCamera.transform.forward * 3, flatRotation);
        InitializeEnvironmentLogic(fakeRealWorldPose, targetData);
    }

    private void InitializeEnvironmentLogic(Pose realWorldImagePose, ImageTargetData targetData)
    {
        if (environmentSceneObject == null) { UpdateStatus("Error: Environment Prefab Missing!"); return; }
        if (anchorManager == null) { UpdateStatus("Error: Anchor Manager Missing!"); return; }
        if (targetData.anchorPointInMap == null) { UpdateStatus("Error: Target data's Anchor Point is not set!"); return; }

        UpdateStatus("Initializing Environment...");

        instantiatedEnvironment = Instantiate(environmentSceneObject);

        // <<< THIS IS THE CRITICAL ADDITION >>>
        // Record the user's horizontal rotation at the moment of spawning.
        userSpawnRotation = Quaternion.Euler(0, arCamera.transform.eulerAngles.y, 0);
        // <<< END ADDITION >>>

        Vector3 realWorldPosition = realWorldImagePose.position;
        Quaternion realWorldFlatRotation = Quaternion.Euler(0, realWorldImagePose.rotation.eulerAngles.y, 0);
        Pose realWorldAnchorPose = new Pose(realWorldPosition, realWorldFlatRotation);

        Pose virtualAnchorOffset = new Pose(targetData.anchorPointInMap.localPosition, targetData.anchorPointInMap.localRotation);

        Pose mapOriginPose = realWorldAnchorPose.GetTransformedBy(virtualAnchorOffset.Inverse());

        GameObject anchorGO = new GameObject("EnvironmentAnchor");
        anchorGO.transform.SetPositionAndRotation(mapOriginPose.position, mapOriginPose.rotation);
        currentAnchor = anchorGO.AddComponent<ARAnchor>();

        if (currentAnchor != null)
        {
            instantiatedEnvironment.transform.SetParent(currentAnchor.transform, false);
            instantiatedEnvironment.transform.localPosition = Vector3.zero;
            instantiatedEnvironment.transform.localRotation = Quaternion.identity;
            Debug.Log($"Map aligned via '{targetData.imageName}' and anchored successfully at calculated origin.");
        }
        else
        {
            Debug.LogError("Failed to add ARAnchor component! Environment will not be placed.");
            UpdateStatus("Error: Could not anchor the environment.");
            Destroy(anchorGO);
            Destroy(instantiatedEnvironment);
            return;
        }

        instantiatedEnvironment.SetActive(true);
        environmentOriginTransform = instantiatedEnvironment.transform;

        NavMeshSurface navMeshSurface = instantiatedEnvironment.GetComponent<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("Runtime NavMesh bake completed on scene object.");

            Transform mapVisuals = environmentOriginTransform.Find("[MapVisuals]");
            if (mapVisuals != null)
            {
                // Get ALL Renderer components on the visuals object and all of its children.
                Renderer[] allRenderers = mapVisuals.GetComponentsInChildren<Renderer>();

                // Loop through each renderer and disable it.
                foreach (Renderer renderer in allRenderers)
                {
                    renderer.enabled = false;
                }
                Debug.Log($"Map visuals hidden by disabling {allRenderers.Length} renderers.");
            }
            else
            {
                Debug.LogWarning("Could not find '[MapVisuals]' child to hide. The whole map will be visible.");
            }
        }
        else
        {
            Debug.LogError("FATAL: NavMeshSurface component not found on the environment object. Navigation will fail.");
        }

        // <<< MODIFICATION: Wrapped in platform-dependent compilation block >>>
#if UNITY_EDITOR
            instantiatedPathVisualizer = instantiatedEnvironment.GetComponentInChildren<PathVisualizer>();
#endif

        if (arrowGuide == null) arrowGuide = instantiatedEnvironment.GetComponentInChildren<DynamicArrowGuide>();

        if (instantiatedPathVisualizer == null && Application.isEditor) { Debug.LogWarning("PathVisualizer not found. Debug line will not be drawn."); }

        if (arrowGuide != null)
        {
            arrowGuide.arCamera = this.arCamera;
            arrowGuide.Initialize();
        }

        isInitialized = true;
        UpdateStatus("Environment Ready. Select Destination.");
        OnEnvironmentInitialized.Invoke();
        SetNavigationUIVisibility(true);

        StartCoroutine(FlashIndicator());
    }

    public Quaternion GetUserSpawnRotation()
    {
        return userSpawnRotation;
    }



    void InitializeEnvironment(ARTrackedImage trackedImage)
    {
        // Legacy method, no changes needed as it's not called.
    }

    public void ResetToInitialState()
    {
        StopNavigation();

        if (instantiatedEnvironment != null)
        {
            Destroy(instantiatedEnvironment);
            instantiatedEnvironment = null;
        }

        if (currentAnchor != null)
        {
            Destroy(currentAnchor.gameObject);
            currentAnchor = null;
        }

        if (confirmPlacementButton != null)
        {
            confirmPlacementButton.gameObject.SetActive(false);
        }
        detectedImageForPlacement = null;

        environmentOriginTransform = null;
        isInitialized = false;
        isStudentModeActive = false;
        SetNavigationUIVisibility(false);
        UpdateStatus("");
    }

    void OnEnable()
    {
        if (trackedImageManager != null) trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        if (destinationDropdown != null) destinationDropdown.onValueChanged.AddListener(HandleDestinationSelection);
    }
    void OnDisable()
    {
        if (trackedImageManager != null) trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        if (destinationDropdown != null) destinationDropdown.onValueChanged.RemoveListener(HandleDestinationSelection);
        if (navigationActive) StopNavigation();
    }
    public async void ActivateStudentMode()
    {
        if (isStudentModeActive) return;
        isStudentModeActive = true;
        SetNavigationUIVisibility(true);
        UpdateStatus("Loading Destinations...");
        availableDestinations = await firebaseManager.GetAllEndPointsAsync();
        await LoadDestinations();
        if (availableDestinations.Count > 0) UpdateStatus("Scan a target image on campus.");
    }
    async Task LoadDestinations()
    {
        if (destinationDropdown == null) return;
        destinationDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Select Destination...")
        };

        if (availableDestinations != null && availableDestinations.Count > 0)
        {
            options.AddRange(availableDestinations.Select(d => new TMP_Dropdown.OptionData(d.Name)));
        }
        else
        {
            options.Add(new TMP_Dropdown.OptionData("No Destinations Found"));
            UpdateStatus("Error: Could not load destinations.");
        }

        destinationDropdown.options = options;
        destinationDropdown.value = 0;
        destinationDropdown.RefreshShownValue();
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        var allTrackedImages = eventArgs.added.Concat(eventArgs.updated);

        foreach (var trackedImage in allTrackedImages)
        {
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                ImageTargetData targetData = imageTargetDatabase.FirstOrDefault(data => data.imageName == trackedImage.referenceImage.name);

                if (targetData != null)
                {
                    if (!isInitialized)
                    {
                        detectedImageForPlacement = trackedImage;
                        if (confirmPlacementButton != null && !confirmPlacementButton.gameObject.activeInHierarchy)
                        {
                            confirmPlacementButton.gameObject.SetActive(true);
                            UpdateStatus($"Image '{trackedImage.referenceImage.name}' found! Press button to place map.");
                        }
                    }
                    else
                    {
                        RecalibrateMapPosition(trackedImage, targetData);
                    }
                }
            }
        }

        if (!isInitialized && detectedImageForPlacement != null)
        {
            bool wasLost = eventArgs.removed.Any(removedImage => removedImage.trackableId == detectedImageForPlacement.trackableId);
            if (wasLost || detectedImageForPlacement.trackingState != TrackingState.Tracking)
            {
                detectedImageForPlacement = null;
                if (confirmPlacementButton != null) confirmPlacementButton.gameObject.SetActive(false);
                UpdateStatus("Scan Target Image...");
            }
        }
    }

    private void RecalibrateMapPosition(ARTrackedImage trackedImage, ImageTargetData targetData)
    {
        if (currentAnchor == null) return;

        Pose realWorldAnchorPose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);
        realWorldAnchorPose.rotation = Quaternion.Euler(0, realWorldAnchorPose.rotation.eulerAngles.y, 0);

        Pose virtualAnchorOffset = new Pose(targetData.anchorPointInMap.localPosition, targetData.anchorPointInMap.localRotation);

        Pose correctMapOriginPose = realWorldAnchorPose.GetTransformedBy(virtualAnchorOffset.Inverse());

        currentAnchor.transform.position = Vector3.Lerp(currentAnchor.transform.position, correctMapOriginPose.position, recalibrationSmoothing);
        currentAnchor.transform.rotation = Quaternion.Slerp(currentAnchor.transform.rotation, correctMapOriginPose.rotation, recalibrationSmoothing);

        UpdateStatus($"Recalibrating map using '{targetData.imageName}'...");
        StartCoroutine(FlashIndicator());
    }

    void HandleDestinationSelection(int index)
    {
        selectedDestinationIndex = index;

        if (index == 0)
        {
            StopNavigation();
            return;
        }

        int adjustedIndex = index - 1;

        if (adjustedIndex >= 0 && adjustedIndex < availableDestinations.Count)
        {
            StartNavigationToPoint(availableDestinations[adjustedIndex]);
        }
        else
        {
            Debug.LogWarning($"Invalid destination index: {index}");
            StopNavigation();
        }
    }

    void Update()
    {
        if (!isStudentModeActive || !isInitialized) return;

        if (navigationActive)
        {
            timeSinceLastUpdate += Time.deltaTime;
            if (timeSinceLastUpdate >= updateInterval)
            {
                timeSinceLastUpdate = 0f;
                UpdateUserPositionAndPath();
            }

            if (currentPathCorners != null && currentPathCorners.Count > 0)
            {
                Vector3 userPos = arCamera.transform.position;
                Vector3 finalDestination = currentTargetPosition;

                float distanceToTarget = Vector3.Distance(userPos, finalDestination);

                if (distanceText != null)
                {
                    distanceText.text = $"{distanceToTarget:F1}m";
                }

                if (distanceToTarget < arrivalDistanceThreshold)
                {
                    string destinationName = GetSelectedDestinationName();
                    StopNavigation();
                    UpdateStatus($"You have arrived at {destinationName}!");
                }
            }
        }
    }

    void UpdateUserPositionAndPath()
    {
        if (environmentOriginTransform == null) return;
        Vector3 userPos = arCamera.transform.position;
        bool pathIsEmpty = currentPathCorners == null || currentPathCorners.Count == 0;
        bool userHasMoved = Vector3.Distance(userPos, lastPathCalculationPosition) > recalculateDistanceThreshold;
        bool needsRecalculation = forcePathRecalculation || pathIsEmpty || userHasMoved;
        if (needsRecalculation)
        {
            if (NavMesh.CalculatePath(userPos, currentTargetPosition, NavMesh.AllAreas, navMeshPath) && navMeshPath.status == NavMeshPathStatus.PathComplete)
            {
                currentPathCorners = navMeshPath.corners.ToList();
                lastPathCalculationPosition = userPos;

                // <<< MODIFICATION: Wrapped in platform-dependent compilation block >>>
#if UNITY_EDITOR
                    instantiatedPathVisualizer?.DrawPath(navMeshPath.corners);
#endif

                arrowGuide?.SetPath(currentPathCorners);
                UpdateStatus($"Navigating to {GetSelectedDestinationName()}");
            }
            else
            {
                currentPathCorners.Clear();

                // <<< MODIFICATION: Wrapped in platform-dependent compilation block >>>
#if UNITY_EDITOR
                    instantiatedPathVisualizer?.ClearPath();
#endif

                arrowGuide?.ClearArrow();
                UpdateStatus($"Cannot find path to {GetSelectedDestinationName()}");
            }
            forcePathRecalculation = false;
        }
        if (currentPathCorners != null && currentPathCorners.Count > 0) { arrowGuide?.UpdateArrow(userPos, currentPathCorners); }
    }
    public void StartNavigationToPoint(PathPointData destinationData)
    {
        Debug.Log($"Starting navigation to: {destinationData.Name}");
        if (!isInitialized || destinationData == null || environmentOriginTransform == null) { StopNavigation(); return; }
        Vector3 destinationWorldPosition = environmentOriginTransform.TransformPoint(destinationData.Position);
        if (NavMesh.SamplePosition(destinationWorldPosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            currentTargetPosition = hit.position;
            navigationActive = true;
            forcePathRecalculation = true;
            OnNavigationStarted.Invoke();
        }
        else
        {
            UpdateStatus($"Error: Location '{destinationData.Name}' is unreachable.");
            StopNavigation();
        }
    }

    public void StopNavigation()
    {
        navigationActive = false;
        forcePathRecalculation = false;
        if (currentPathCorners != null) currentPathCorners.Clear();

        // <<< MODIFICATION: Wrapped in platform-dependent compilation block >>>
#if UNITY_EDITOR
            instantiatedPathVisualizer?.ClearPath();
#endif

        arrowGuide?.ClearArrow();
        if (isInitialized && isStudentModeActive) UpdateStatus("Navigation Stopped.");

        if (distanceText != null)
        {
            distanceText.text = "";
        }

        if (destinationDropdown != null) destinationDropdown.value = 0;
        OnNavigationStopped.Invoke();
    }
    #region Helper Methods
    public void SetNavigationUIVisibility(bool isVisible)
    {
        if (navigationUIParent != null) navigationUIParent.SetActive(isVisible);
        if (destinationDropdown != null) destinationDropdown.interactable = isVisible && isInitialized;
        if (findFacultyButton != null) findFacultyButton.interactable = isVisible && isInitialized && facultyFinderInstance != null;
    }
    public void UpdateStatus(string message) { if (statusText != null) { statusText.text = message; } }

    bool IsTargetImage(ARTrackedImage image) => string.IsNullOrEmpty(targetImageName) || image.referenceImage.name == targetImageName;

    string GetSelectedDestinationName() => selectedDestinationIndex > 0 && selectedDestinationIndex - 1 < availableDestinations.Count ? availableDestinations[selectedDestinationIndex - 1].Name : "Target";
    #endregion
}