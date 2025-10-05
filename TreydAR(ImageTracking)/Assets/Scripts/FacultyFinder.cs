using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;

// Assuming FacultyMemberData, FirebaseManager, NavigationManager, DatabaseManager, FacultyListItemUI are defined

/// <summary>
/// Displays a filterable list of faculty members with their details and allows navigation.
/// Hides Navigation UI when active.
/// </summary>
public class FacultyFinder : MonoBehaviour
{
    #region Inspector Fields
    [Header("Core References")]
    public DatabaseManager databaseManager;
    public FirebaseManager firebaseManager;
    public NavigationManager navigationManager;

    [Header("UI Elements")]
    public GameObject facultyFinderPanel;
    public Transform facultyListContentParent;
    public GameObject facultyListItemPrefab; // IMPORTANT: Prefab needs FacultyListItemUI script and Text fields
    public Button closeFinderButton;
    [Tooltip("Input field for searching faculty by name or department.")]
    public TMP_InputField searchInput;
    [Tooltip("Optional: Text shown when no faculty match the search.")]
    public TextMeshProUGUI noResultsText;
    [Tooltip("Optional: A label/title text at the top of the faculty list panel.")]
    public TextMeshProUGUI facultyListTitleLabel;
    #endregion

    #region Runtime State
    private List<GameObject> instantiatedListItems = new List<GameObject>();
    private Dictionary<string, FacultyMemberData> fullFacultyCache = new Dictionary<string, FacultyMemberData>();
    private bool _isPanelActiveAndDataReady = false; // To control population
    #endregion

    void Start()
    {
        // --- Validate References ---
        bool refsValid = true;
        if (databaseManager == null) { Debug.LogError("FacultyFinder: DatabaseManager missing!", this); refsValid = false; }
        if (firebaseManager == null) { Debug.LogError("FacultyFinder: FirebaseManager missing!", this); refsValid = false; }
        if (navigationManager == null) { Debug.LogError("FacultyFinder: NavigationManager missing!", this); refsValid = false; }
        if (facultyFinderPanel == null) { Debug.LogError("FacultyFinder: Panel missing!", this); refsValid = false; }
        if (facultyListContentParent == null) { Debug.LogError("FacultyFinder: Content Parent missing!", this); refsValid = false; }
        if (facultyListItemPrefab == null) { Debug.LogError("FacultyFinder: List Item Prefab missing!", this); refsValid = false; }
        else if (facultyListItemPrefab.GetComponent<FacultyListItemUI>() == null) { Debug.LogError("FacultyFinder: FacultyListItemPrefab is missing the FacultyListItemUI script component!", this); refsValid = false; }
        if (searchInput == null) { Debug.LogError("FacultyFinder: Search Input missing!", this); refsValid = false; }
        if (closeFinderButton == null) { Debug.LogError("FacultyFinder: Close Finder Button missing!", this); refsValid = false; }
        // noResultsText and facultyListTitleLabel are optional
        if (!refsValid) { enabled = false; return; }
        // --- End Validation ---

        // --- Assign Listeners ---
        if (closeFinderButton != null) closeFinderButton.onClick.AddListener(HideFinder);
        if (searchInput != null) searchInput.onValueChanged.AddListener(OnSearchValueChanged);
        // ----------------------

        if (facultyFinderPanel != null) facultyFinderPanel.SetActive(false);
        if (firebaseManager != null) { firebaseManager.OnFacultyDataUpdated += HandleFacultyUpdate; }

        // Set title label text if assigned
        if (facultyListTitleLabel != null)
        {
            facultyListTitleLabel.text = "Faculty & Staff Directory"; // Or set via Inspector
        }
    }

    void OnDestroy()
    {
        // --- Unsubscribe ---
        if (firebaseManager != null) { firebaseManager.OnFacultyDataUpdated -= HandleFacultyUpdate; }
        if (searchInput != null) searchInput.onValueChanged.RemoveListener(OnSearchValueChanged);
        // -----------------
    }

    // --- Public Methods ---
    public async void ShowFinder()
    {
        if (facultyFinderPanel == null || firebaseManager == null)
        {
            Debug.LogError("FacultyFinder: Cannot show - panel or FirebaseManager is null.");
            return;
        }
        if (searchInput != null) searchInput.text = "";

        facultyFinderPanel.SetActive(true);
        navigationManager?.SetNavigationUIVisibility(false); // Hide Nav UI

        _isPanelActiveAndDataReady = false;
        ClearFacultyList();
        ShowNoResultsMessage(true, "Loading faculty data...");

        await firebaseManager.GetAllFacultyMembersAsync(); // Ensure cache is fresh

        _isPanelActiveAndDataReady = true;
        PopulateAndFilterList(); // Populate with fresh cache
    }

    public void HideFinder()
    {
        if (facultyFinderPanel != null) facultyFinderPanel.SetActive(false);
        _isPanelActiveAndDataReady = false;
        navigationManager?.SetNavigationUIVisibility(true); // Show Nav UI
    }

    // --- Internal Logic ---
    public void FetchDepartmentRooms(System.Action<Dictionary<string, List<string>>> callback)
    {
        FirebaseDatabase.DefaultInstance.GetReference("departments").GetValueAsync().ContinueWith(task => {
            if (task.IsCompleted)
            {
                var result = new Dictionary<string, List<string>>();
                DataSnapshot snapshot = task.Result;
                foreach (var dept in snapshot.Children)
                {
                    string deptName = dept.Key;
                    List<string> rooms = new();
                    foreach (var room in dept.Children)
                        rooms.Add(room.Value.ToString());

                    result[deptName] = rooms;
                }
                callback?.Invoke(result);
            }
            else
            {
                Debug.LogError("Failed to fetch departments: " + task.Exception);
                callback?.Invoke(null);
            }
        });
    }

    public class DepartmentData
    {
        public string anchor;
        public List<string> rooms;
    }

    public void FetchDepartmentRoomData(System.Action<Dictionary<string, DepartmentData>> callback)
    {
        FirebaseDatabase.DefaultInstance.GetReference("departments").GetValueAsync().ContinueWith(task => {
            var result = new Dictionary<string, DepartmentData>();
            DataSnapshot snapshot = task.Result;
            foreach (var dept in snapshot.Children)
            {
                string deptName = dept.Key;
                string anchor = dept.Child("anchor").Value?.ToString();
                List<string> rooms = new();
                foreach (var room in dept.Child("rooms").Children)
                    rooms.Add(room.Value.ToString());

                result[deptName] = new DepartmentData { anchor = anchor, rooms = rooms };
            }
            callback?.Invoke(result);
        });
    }

    private void PopulateAndFilterList()
    {
        if (firebaseManager == null || facultyListContentParent == null || facultyListItemPrefab == null) return;
        ClearFacultyList();
        fullFacultyCache = firebaseManager.GetCachedFaculty();
        if (fullFacultyCache == null || fullFacultyCache.Count == 0) { ShowNoResultsMessage(true, "No faculty data available."); return; }

        string searchTerm = searchInput?.text.Trim().ToLowerInvariant() ?? "";
        List<FacultyMemberData> filteredFaculty = fullFacultyCache.Values
            .Where(f => f != null && !string.IsNullOrEmpty(f.FacultyID) &&
                        (string.IsNullOrEmpty(searchTerm) ||
                         (f.FullName?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                         (f.Department?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                         (f.Position?.ToLowerInvariant().Contains(searchTerm) ?? false)))
            .OrderBy(f => f.FullName)
            .ToList();

        if (filteredFaculty.Count == 0) { ShowNoResultsMessage(true, string.IsNullOrEmpty(searchTerm) ? "No faculty found." : "No faculty match your search."); return; }
        ShowNoResultsMessage(false);

        foreach (FacultyMemberData faculty in filteredFaculty)
        {
            GameObject listItem = Instantiate(facultyListItemPrefab, facultyListContentParent);
            instantiatedListItems.Add(listItem);
            FacultyListItemUI itemUI = listItem.GetComponent<FacultyListItemUI>();
            if (itemUI != null)
            {
                itemUI.DeptText.text = faculty.Department ?? "N/A";
                if (itemUI.PositionText != null) itemUI.PositionText.text = faculty.Position ?? "N/A";
                itemUI.NameText.text = faculty.FullName ?? "N/A";
                itemUI.LocText.text = faculty.CurrentLocationName ?? "N/A";
                itemUI.StatusText.text = faculty.AvailabilityStatus ?? "N/A";
            }
            else { Debug.LogError("FacultyListItemPrefab missing FacultyListItemUI script!", listItem); }
            Button itemButton = listItem.GetComponent<Button>();
            if (itemButton != null)
            {
                string currentFacultyId = faculty.FacultyID;
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(async () => await OnFacultySelected(currentFacultyId));
            }
            else { Debug.LogWarning("FacultyListItemPrefab missing Button component.", listItem); }
        }
    }

    private void ClearFacultyList()
    {
        // Debug.Log("Clearing faculty list items. Current count: " + instantiatedListItems.Count);
        foreach (GameObject item in instantiatedListItems) { if (item != null) Destroy(item); }
        instantiatedListItems.Clear();
        ShowNoResultsMessage(false);
    }

    private async Task OnFacultySelected(string facultyId)
    {
        if (firebaseManager == null || databaseManager == null || navigationManager == null) return;
        string locationName = await firebaseManager.GetFacultyCurrentLocationNameAsync(facultyId);
        if (string.IsNullOrEmpty(locationName))
        {
            navigationManager?.UpdateStatus($"Info: Faculty has not set a location.");
            HideFinder(); return;
        }
        PathPointData destinationPoint = databaseManager.GetPathPointByName(locationName);
        if (destinationPoint == null)
        {
            navigationManager?.UpdateStatus($"Error: Location '{locationName}' is invalid or missing from map.");
            HideFinder(); return;
        }
        navigationManager.StartNavigationToPoint(destinationPoint);
        HideFinder();
    }

    private void HandleFacultyUpdate(Dictionary<string, FacultyMemberData> updatedFacultyData)
    {
        fullFacultyCache = updatedFacultyData ?? new Dictionary<string, FacultyMemberData>();
        if (_isPanelActiveAndDataReady && facultyFinderPanel != null && facultyFinderPanel.activeInHierarchy)
        {
            Debug.Log("FacultyFinder: Firebase data updated by listener, repopulating list.");
            PopulateAndFilterList();
        }
    }

    private void OnSearchValueChanged(string searchTerm)
    {
        if (_isPanelActiveAndDataReady) { PopulateAndFilterList(); }
    }

    private void ShowNoResultsMessage(bool show, string message = "")
    {
        if (noResultsText != null) { noResultsText.text = message; noResultsText.gameObject.SetActive(show); }
    }
}