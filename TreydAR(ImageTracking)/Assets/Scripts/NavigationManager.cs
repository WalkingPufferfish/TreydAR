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

    // <<< LEGACY >>>: Your original targetImageName is preserved but hidden from the Inspector.
    // The new 'imageTargetDatabase' list below now controls the logic.
    [HideInInspector]
    public string targetImageName = "";

    [Header("UI References")]
    public TMP_Text textDisplay;

    [Header("Navigation Marker")]
    public GameObject navigationMarkerPrefab;
    private GameObject currentMarker;

    // <<< ADDED >>>: This new list is the core of the multi-target system.
    [Header("Multi-Target Setup")]
    [Tooltip("Define all your image targets and their corresponding anchor points within the map.")]
    public List<ImageTargetData> imageTargetDatabase;

    [Header("Environment Setup")]
    [Tooltip("Drag the DISABLED Environment GameObject from your HIERARCHY here.")]
    public GameObject environmentSceneObject;
    public float environmentVerticalOffset = 0.0f;
    public GameObject ScanGuide_Image;
    public CanvasGroup scanGuideCanvasGroup;

    [Header("Navigation Control")]
    public TMP_Dropdown destinationDropdown;
    public float recalculateDistanceThreshold = 1.0f;
    public float updateInterval = 0.25f;

    [Header("UI Feedback")]
    public TextMeshProUGUI statusText;
    public GameObject navigationUIParent;
    [Tooltip("The button that appears to let the user confirm the map's placement.")]
    public Button confirmPlacementButton;

    [Header("Faculty Finder Integration")]
    public Button findFacultyButton;
    public FacultyFinder facultyFinderInstance;

    [Header("Events")]
    public UnityEvent OnNavigationStarted, OnNavigationStopped, OnEnvironmentInitialized;
    #endregion

    #region Private State
    private Transform environmentOriginTransform;
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

    // <<< ADDED >>>: A public method for the test helper script to check the script's state.
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
        if (environmentSceneObject != null)
        {
            environmentSceneObject.SetActive(false);
        }

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


    // A Coroutine to manage the fade out animation for Scan Guide
    private System.Collections.IEnumerator FadeOutScanGuide(float duration = 0.5f)
    {
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

    // <<< MODIFIED >>>: This method is updated to find the correct data from the database
    // before calling the new core logic method. It no longer calls your old InitializeEnvironment.
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

    // <<< ADDED >>>: The new public method for the Editor Test Helper script to call.
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
        // <<< NEW, CORRECTED CODE >>>

        // 1. Get the camera's forward direction.
        Vector3 cameraForward = arCamera.transform.forward;

        // 2. Remove any vertical component (the tilt) by setting its Y value to 0.
        cameraForward.y = 0;

        // 3. Create a new "flat" rotation that only looks along the horizontal plane.
        // We normalize the vector to ensure it's a valid direction.
        Quaternion flatRotation = Quaternion.LookRotation(cameraForward.normalized);

        // 4. Create the fake pose using the original position but the new FLAT rotation.
        Pose fakeRealWorldPose = new Pose(arCamera.transform.position + arCamera.transform.forward * 3, flatRotation);
        InitializeEnvironmentLogic(fakeRealWorldPose, targetData);
    }

    // <<< REVISED & IMPROVED METHOD >>>
    // This private method contains the core logic for alignment and setup.
    // It is called by both the real AR confirmation and the editor test.
    private void InitializeEnvironmentLogic(Pose realWorldImagePose, ImageTargetData targetData)
    {
        if (environmentSceneObject == null) { UpdateStatus("Error: Environment Scene Object Missing!"); return; }
        if (anchorManager == null) { UpdateStatus("Error: Anchor Manager Missing!"); return; }
        if (targetData.anchorPointInMap == null) { UpdateStatus("Error: Target data's Anchor Point is not set!"); return; }

        UpdateStatus("Initializing Environment...");

        // --- START OF IMPROVED CALCULATION ---

        // 1. Get the real-world pose of the detected image. We use its position directly.
        //    For rotation, we flatten it to ensure the map doesn't tilt with the camera.
        Vector3 realWorldPosition = realWorldImagePose.position;
        Quaternion realWorldFlatRotation = Quaternion.Euler(0, realWorldImagePose.rotation.eulerAngles.y, 0);
        Pose realWorldAnchorPose = new Pose(realWorldPosition, realWorldFlatRotation);

        // 2. Get the virtual anchor's pose *relative to its parent map*.
        //    This is the "offset" of your anchor point from the map's origin (0,0,0).
        Pose virtualAnchorOffset = new Pose(targetData.anchorPointInMap.localPosition, targetData.anchorPointInMap.localRotation);

        // 3. Calculate the pose of the MAP'S ORIGIN by transforming the real-world pose
        //    by the *inverse* of the virtual anchor's offset.
        //
        //    Think of it like this:
        //    "Start at the real-world image's location (realWorldAnchorPose),
        //    then apply the transformation that takes you from the virtual anchor back to the map's origin (virtualAnchorOffset.Inverse())."
        //
        //    This is the key step to ensure the virtual anchor inside the map aligns perfectly with the real-world image.
        Pose mapOriginPose = realWorldAnchorPose.GetTransformedBy(virtualAnchorOffset.Inverse());

        // --- END OF IMPROVED CALCULATION ---

        // 4. Create the ARAnchor at the calculated map origin pose.
        GameObject anchorGO = new GameObject("EnvironmentAnchor");
        anchorGO.transform.SetPositionAndRotation(mapOriginPose.position, mapOriginPose.rotation);
        currentAnchor = anchorGO.AddComponent<ARAnchor>();

        if (currentAnchor != null)
        {
            // 5. Parent the environment to the anchor. It should now be perfectly aligned.
            environmentSceneObject.transform.SetParent(currentAnchor.transform, false); // 'false' ensures local transforms are preserved
            environmentSceneObject.transform.localPosition = Vector3.zero;
            environmentSceneObject.transform.localRotation = Quaternion.identity;
            Debug.Log($"Map aligned via '{targetData.imageName}' and anchored successfully at calculated origin.");
        }
        else
        {
            Debug.LogError("Failed to add ARAnchor component! Environment will not be placed.");
            UpdateStatus("Error: Could not anchor the environment.");
            Destroy(anchorGO);
            return;
        }

        environmentSceneObject.SetActive(true);
        environmentOriginTransform = environmentSceneObject.transform;

        NavMeshSurface navMeshSurface = environmentSceneObject.GetComponent<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("Runtime NavMesh bake completed on scene object.");
        }
        else
        {
            Debug.LogError("FATAL: NavMeshSurface component not found on the environment object. Navigation will fail.");
        }

        instantiatedPathVisualizer = environmentSceneObject.GetComponentInChildren<PathVisualizer>();
        if (arrowGuide == null) arrowGuide = environmentSceneObject.GetComponentInChildren<DynamicArrowGuide>();
        if (instantiatedPathVisualizer == null) { ResetToInitialState(); return; }
        if (arrowGuide != null) arrowGuide.Initialize();

        isInitialized = true;
        UpdateStatus("Environment Ready. Select Destination.");
        OnEnvironmentInitialized.Invoke();
        SetNavigationUIVisibility(true);
    }

    // <<< LEGACY >>>: Your original InitializeEnvironment method is preserved here exactly as you provided it.
    // It is no longer called by the main logic flow.
    void InitializeEnvironment(ARTrackedImage trackedImage)
    {
        if (environmentSceneObject == null) { UpdateStatus("Error: Environment Scene Object Missing!"); return; }
        if (anchorManager == null) { UpdateStatus("Error: Anchor Manager Missing!"); return; }

        UpdateStatus("Initializing Environment...");

        Vector3 anchorPosition = trackedImage.transform.position + (Vector3.up * environmentVerticalOffset);
        float yRotation = trackedImage.transform.rotation.eulerAngles.y;
        Quaternion anchorRotation = Quaternion.Euler(0, yRotation, 0);

        // This is the universally compatible way to create an anchor
        GameObject anchorGO = new GameObject("EnvironmentAnchor");
        anchorGO.transform.SetPositionAndRotation(anchorPosition, anchorRotation);
        currentAnchor = anchorGO.AddComponent<ARAnchor>();

        if (currentAnchor != null)
        {
            environmentSceneObject.transform.SetParent(currentAnchor.transform);
            environmentSceneObject.transform.localPosition = Vector3.zero;
            environmentSceneObject.transform.localRotation = Quaternion.identity;
            Debug.Log("Successfully created an ARAnchor and parented environment.");
        }
        else
        {
            Debug.LogError("Failed to add ARAnchor component! Environment will not be placed.");
            UpdateStatus("Error: Could not anchor the environment.");
            Destroy(anchorGO);
            return;
        }

        environmentSceneObject.SetActive(true);
        environmentOriginTransform = environmentSceneObject.transform;

        NavMeshSurface navMeshSurface = environmentSceneObject.GetComponent<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("Runtime NavMesh bake completed on scene object.");
        }
        else
        {
            Debug.LogError("FATAL: NavMeshSurface component not found on the environment object. Navigation will fail.");
            UpdateStatus("Error: Navigation setup invalid.");
            return;
        }

        instantiatedPathVisualizer = environmentSceneObject.GetComponentInChildren<PathVisualizer>();
        if (arrowGuide == null) arrowGuide = environmentSceneObject.GetComponentInChildren<DynamicArrowGuide>();
        if (instantiatedPathVisualizer == null) { ResetToInitialState(); return; }
        if (arrowGuide != null) arrowGuide.Initialize();

        isInitialized = true;
        UpdateStatus("Environment Ready. Select Destination.");
        OnEnvironmentInitialized.Invoke();
        SetNavigationUIVisibility(true);
    }

    public void ResetToInitialState()
    {
        StopNavigation();

        // <<< MODIFIED >>>: Logic updated slightly to safely un-parent the scene object before destroying the anchor.
        if (environmentSceneObject != null)
        {
            environmentSceneObject.transform.SetParent(null, true);

            NavMeshSurface navMeshSurface = environmentSceneObject.GetComponent<NavMeshSurface>();
            if (navMeshSurface != null)
            {
                navMeshSurface.RemoveData();
            }
            environmentSceneObject.SetActive(false);
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
        await LoadDestinations();
        if (availableDestinations.Count > 0) UpdateStatus("Scan a target image on campus.");
    }
    async Task LoadDestinations()
    {
        destinationDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Select Destination...") // Null choice
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

    // <<< MODIFIED >>>: The logic is updated to check against the imageTargetDatabase list.
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        if (!isStudentModeActive || isInitialized) return;

        ARTrackedImage imageToUse = null;

        var allTrackedImages = eventArgs.added.Concat(eventArgs.updated);
        foreach (var trackedImage in allTrackedImages)
        {
            if (trackedImage.trackingState == TrackingState.Tracking &&
                imageTargetDatabase.Any(data => data.imageName == trackedImage.referenceImage.name))
            {
                imageToUse = trackedImage;
                break;
            }
        }

        if (imageToUse != null)
        {
            detectedImageForPlacement = imageToUse;
            if (confirmPlacementButton != null && !confirmPlacementButton.gameObject.activeInHierarchy)
            {
                confirmPlacementButton.gameObject.SetActive(true);
                UpdateStatus($"Image '{imageToUse.referenceImage.name}' found! Position your camera and press the button.");
            }
        }

        if (detectedImageForPlacement != null)
        {
            bool wasLost = eventArgs.removed.Any(removedImage => removedImage.trackableId == detectedImageForPlacement.trackableId);

            if (wasLost || detectedImageForPlacement.trackingState != TrackingState.Tracking)
            {
                detectedImageForPlacement = null;
                if (confirmPlacementButton != null)
                {
                    confirmPlacementButton.gameObject.SetActive(false);
                }
                UpdateStatus("Scan Target Image...");
            }
        }
    }

    void HandleDestinationSelection(int index)
    {
        selectedDestinationIndex = index;

        // Skip placeholder
        if (index == 0 || destinationDropdown.options[index].text == "Select Department")
        {
            StopNavigation();
            return;
        }

        // Adjust index to match availableDestinations
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
        if (!isStudentModeActive || !isInitialized || !navigationActive) return;
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval) { timeSinceLastUpdate = 0f; UpdateUserPositionAndPath(); }
    }
    void UpdateUserPositionAndPath()
    {
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
                instantiatedPathVisualizer?.DrawPath(navMeshPath.corners);
                arrowGuide?.SetPath(currentPathCorners);
                UpdateStatus($"Navigating to {GetSelectedDestinationName()}");
            }
            else
            {
                currentPathCorners.Clear();
                instantiatedPathVisualizer?.ClearPath();
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
        if (!isInitialized || destinationData == null) { StopNavigation(); return; }
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
    /// <summary>
    /// Called by RoomSearchUIController when a room is selected from the dropdown.
    /// Finds the corresponding anchor and places a navigation marker.
    /// </summary>
    public void NavigateToRoom(string roomName)
    {
        Debug.Log("Navigating to room: " + roomName);

        // 1. Look up the room's anchor or endpoint
        Transform targetAnchor = FindRoomAnchor(roomName);
        if (targetAnchor == null)
        {
            Debug.LogWarning($"Room anchor not found for: {roomName}");
            if (textDisplay != null)
                textDisplay.text = $"Room '{roomName}' not found.";
            return;
        }

        // 2. Trigger AR guidance (e.g., place marker or path)
        PlaceNavigationMarker(targetAnchor.position);
        currentTargetPosition = targetAnchor.position;
        navigationActive = true;
        forcePathRecalculation = true;

        // 3. Optionally update UI or feedback
        if (textDisplay != null)
            textDisplay.text = $"Navigating to {roomName}...";
    }

    /// <summary>
    /// Finds a room anchor by name. Assumes GameObjects are named after room names.
    /// </summary>
    private Transform FindRoomAnchor(string roomName)
    {
        GameObject anchorObj = GameObject.Find(roomName);
        return anchorObj != null ? anchorObj.transform : null;
    }

    /// <summary>
    /// Places or updates the navigation marker at the target position.
    /// </summary>
    private void PlaceNavigationMarker(Vector3 position)
    {
        if (navigationMarkerPrefab == null)
        {
            Debug.LogWarning("Navigation marker prefab not assigned.");
            return;
        }

        if (currentMarker != null)
            Destroy(currentMarker);

        currentMarker = Instantiate(navigationMarkerPrefab, position, Quaternion.identity);
    }

    public void StopNavigation()
    {
        navigationActive = false;
        forcePathRecalculation = false;
        if (currentPathCorners != null) currentPathCorners.Clear();
        instantiatedPathVisualizer?.ClearPath();
        arrowGuide?.ClearArrow();
        if (isInitialized && isStudentModeActive) UpdateStatus("Navigation Stopped.");
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

    // <<< LEGACY >>>: Your original IsTargetImage method is preserved here exactly as you provided it.
    // It is no longer called by the main logic flow.
    bool IsTargetImage(ARTrackedImage image) => string.IsNullOrEmpty(targetImageName) || image.referenceImage.name == targetImageName;

    string GetSelectedDestinationName() => selectedDestinationIndex > 0 && selectedDestinationIndex - 1 < availableDestinations.Count ? availableDestinations[selectedDestinationIndex - 1].Name : "Target";
    #endregion
}