
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Firebase.Database;

// Assuming FacultyMemberData class is defined elsewhere
// Assuming FirebaseManager class is defined elsewhere
// Assuming NavigationManager class is defined elsewhere
// Assuming DatabaseManager class is defined elsewhere
// Assuming RoleSelectionManager class is defined elsewhere

public class FacultyPortalManager : MonoBehaviour
{
    #region Inspector Fields
    [Header("Core References")]
    public DatabaseManager databaseManager;
    public FirebaseManager firebaseManager;
    public NavigationManager navigationManager;
    public RoleSelectionManager roleSelectionManager;

    [Header("UI Panels")]
    public GameObject loginPanel;
    public GameObject createAccountPanel; // Also used for editing
    public GameObject locationUpdatePanel;

    [Header("Login Panel Elements")]
    public TMP_InputField loginFacultyIdInput;
    public TMP_InputField loginPasswordInput;
    public TextMeshProUGUI loginStatusText;
    public Button loginButton;
    public Button showCreateAccountButton;
    public Button changeRoleButtonLogin;

    [Header("Create/Edit Account Panel Elements")]
    public TMP_InputField createFacultyIdInput;
    public TMP_InputField createFullNameInput;
    public TMP_InputField createDepartmentInput;
    public TMP_InputField createPasswordInput;
    public TMP_InputField createConfirmPasswordInput;
    [Tooltip("Dropdown for selecting Faculty/Admin position during account creation.")]
    public TMP_Dropdown createPositionDropdown;
    public TextMeshProUGUI createStatusText;
    public Button createAccountButton; // Text will change for Create/Update
    public Button backToLoginButton;

    [Header("Location Update Panel Elements")]
    public TextMeshProUGUI welcomeText;
    public TextMeshProUGUI currentLocationText;
    public TMP_Dropdown locationDropdown;
    public TMP_Dropdown roomDropdown;
    public TextMeshProUGUI locationUpdateStatusText;
    public Button updateLocationButton; // Updates both location and availability
    public Button goToWayfindingButton;
    public Button logoutButton;
    public TMP_Dropdown availabilityDropdown;
    [Tooltip("Button to edit the current faculty's account details.")]
    public Button editAccountButton;
    [Tooltip("Button to delete the current faculty's account from the database.")]
    public Button deleteAccountButton;
    #endregion

    #region Runtime State
    private Dictionary<string, List<string>> departmentRoomMap = new();
    private FacultyMemberData currentLoggedInFacultyData;
    private List<PathPointData> availableEndPoints = new List<PathPointData>();
    private readonly List<string> availabilityStatuses = new List<string> { "Select Status...", "In Office", "Meeting/OB", "WFH", "On Leave", "Out" };
    private readonly List<string> positionOptions = new List<string> { "Select Position...", "Faculty", "Admin" };
    private bool isEditingAccount = false;
    #endregion

    #region Unity Lifecycle & Activation
    void Start()
    {
        bool refsValid = true;
        if (databaseManager == null) { Debug.LogError("FacultyPortalManager: DatabaseManager missing!", this); refsValid = false; }
        if (firebaseManager == null) { Debug.LogError("FacultyPortalManager: FirebaseManager missing!", this); refsValid = false; }
        if (navigationManager == null) { Debug.LogError("FacultyPortalManager: NavigationManager missing!", this); refsValid = false; }
        if (roleSelectionManager == null) { Debug.LogError("FacultyPortalManager: RoleSelectionManager missing!", this); refsValid = false; }
        if (loginPanel == null || createAccountPanel == null || locationUpdatePanel == null) { Debug.LogError("Ref missing: UI Panel(s)", this); refsValid = false; }
        if (loginFacultyIdInput == null || loginPasswordInput == null || loginStatusText == null || loginButton == null || showCreateAccountButton == null || changeRoleButtonLogin == null) { Debug.LogError("Ref missing: Login Panel Element(s)", this); refsValid = false; }
        if (createFacultyIdInput == null || createFullNameInput == null || createDepartmentInput == null || createPasswordInput == null || createConfirmPasswordInput == null || createPositionDropdown == null || createStatusText == null || createAccountButton == null || backToLoginButton == null) { Debug.LogError("Ref missing: Create Account Panel Element(s)", this); refsValid = false; }
        if (welcomeText == null || currentLocationText == null || locationDropdown == null || locationUpdateStatusText == null || updateLocationButton == null || goToWayfindingButton == null || logoutButton == null || availabilityDropdown == null || editAccountButton == null || deleteAccountButton == null) { Debug.LogError("Ref missing: Location Update Panel Element(s)", this); refsValid = false; }

        if (!refsValid) { Debug.LogError("FacultyPortalManager disabled due to missing references."); enabled = false; return; }

        loginButton.onClick.AddListener(OnLoginButtonPressed);
        showCreateAccountButton.onClick.AddListener(OnShowCreateAccountPanelButtonPressed);
        createAccountButton.onClick.AddListener(OnCreateOrUpdateAccountButtonPressed);
        backToLoginButton.onClick.AddListener(OnBackToLoginButtonPressed);
        updateLocationButton.onClick.AddListener(OnUpdateLocationAndAvailabilityPressed);
        goToWayfindingButton.onClick.AddListener(OnGoToWayfindingPressed);
        logoutButton.onClick.AddListener(OnLogoutButtonPressed);
        changeRoleButtonLogin.onClick.AddListener(OnChangeRolePressed);
        editAccountButton.onClick.AddListener(OnEditAccountPressed);
        deleteAccountButton.onClick.AddListener(OnDeleteAccountPressed);

        SetupAvailabilityDropdown();
        SetupPositionDropdown();
        HideAllPanels();
        Debug.Log("FacultyPortalManager Initialized (Inactive).");
    }

    public void ActivateFacultyMode()
    {
        Debug.Log("FacultyPortalManager: Activating Faculty Mode.");
        currentLoggedInFacultyData = null;
        isEditingAccount = false;
        ShowLoginPanel();
    }
    #endregion

    #region Panel Visibility Control
    public void ShowLoginPanel()
    {
        isEditingAccount = false;
        HideAllPanels();
        if (loginPanel != null) loginPanel.SetActive(true);
        ClearStatusMessages();
        if (loginFacultyIdInput != null) loginFacultyIdInput.text = "";
        if (loginPasswordInput != null) loginPasswordInput.text = "";
        SetButtonInteractable(loginButton, true);
        SetButtonInteractable(showCreateAccountButton, true);
        SetButtonInteractable(changeRoleButtonLogin, true);
    }

    public void ShowCreateAccountPanel(bool forEditing = false)
    {
        HideAllPanels();
        if (createAccountPanel != null) createAccountPanel.SetActive(true);
        ClearStatusMessages();
        isEditingAccount = forEditing;
        TMP_Text createButtonText = createAccountButton?.GetComponentInChildren<TMP_Text>();

        if (forEditing && currentLoggedInFacultyData != null)
        {
            createFacultyIdInput.text = currentLoggedInFacultyData.FacultyID;
            createFacultyIdInput.interactable = false;
            createFullNameInput.text = currentLoggedInFacultyData.FullName;
            createDepartmentInput.text = currentLoggedInFacultyData.Department;
            SetDropdownSelection(createPositionDropdown, positionOptions, currentLoggedInFacultyData.Position);
            SetStatus(createStatusText, "Editing Profile. Password fields are for changing password only (leave blank to keep current).");
            if (createButtonText != null) createButtonText.text = "Update Profile";
            if (createPasswordInput != null) createPasswordInput.text = "";
            if (createConfirmPasswordInput != null) createConfirmPasswordInput.text = "";
        }
        else // Creating new account
        {
            createFacultyIdInput.text = "";
            createFacultyIdInput.interactable = true;
            createFullNameInput.text = "";
            createDepartmentInput.text = "";
            if (createPositionDropdown != null) createPositionDropdown.value = 0;
            if (createPasswordInput != null) createPasswordInput.text = "";
            if (createConfirmPasswordInput != null) createConfirmPasswordInput.text = "";
            if (createButtonText != null) createButtonText.text = "Create Account";
            SetStatus(createStatusText, ""); // Clear status for new account form
        }
        SetButtonInteractable(createAccountButton, true);
        SetButtonInteractable(backToLoginButton, true);
    }

    private void ShowLocationUpdatePanel()
    {
        HideAllPanels();
        availableEndPoints = databaseManager.GetAllEndPoints() ?? new List<PathPointData>();
        PopulateLocationDropdown();
        if (locationUpdatePanel != null) locationUpdatePanel.SetActive(true);
        ClearStatusMessages();
        SetButtonInteractable(updateLocationButton, true);
        SetButtonInteractable(editAccountButton, true);
        SetButtonInteractable(deleteAccountButton, true);
        SetButtonInteractable(goToWayfindingButton, true);
        SetButtonInteractable(logoutButton, true);
        if (locationDropdown != null) locationDropdown.interactable = true;
        if (availabilityDropdown != null) availabilityDropdown.interactable = true;
    }

    public void HideAllPanels()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (createAccountPanel != null) createAccountPanel.SetActive(false);
        if (locationUpdatePanel != null) locationUpdatePanel.SetActive(false);
    }

    private void ClearStatusMessages()
    {
        SetStatus(loginStatusText, "");
        SetStatus(createStatusText, "");
        SetStatus(locationUpdateStatusText, "");
    }
    #endregion

    #region Button Actions
    public async void OnLoginButtonPressed()
    {
        ClearStatusMessages();
        string facultyId = loginFacultyIdInput.text?.Trim();
        string password = loginPasswordInput.text; // Plain text password
        if (string.IsNullOrEmpty(facultyId) || string.IsNullOrEmpty(password)) { SetStatus(loginStatusText, "Error: Faculty ID and Password are required."); return; }

        SetButtonInteractable(loginButton, false); SetStatus(loginStatusText, "Logging in...");
        try
        {
            FacultyMemberData faculty = await firebaseManager.GetFacultyMemberAsync(facultyId);
            if (faculty != null)
            {
                // Verify password against the stored hash using FirebaseManager
                if (firebaseManager.VerifyPassword(faculty, password))
                {
                    await LoginSuccess(faculty);
                }
                else { LoginFail(facultyId, "Invalid Faculty ID or Password."); }
            }
            else { LoginFail(facultyId, "Faculty ID not found."); }
        }
        catch (Exception e) { Debug.LogError($"Login Error: {e.Message}"); SetStatus(loginStatusText, "Error during login."); LoginFail(facultyId, "Login error."); }
        finally { SetButtonInteractable(loginButton, true); }
    }

    public void OnShowCreateAccountPanelButtonPressed() { ShowCreateAccountPanel(false); }
    public void OnBackToLoginButtonPressed() { ShowLoginPanel(); }

    public async void OnCreateOrUpdateAccountButtonPressed()
    {
        ClearStatusMessages();
        string facultyId = createFacultyIdInput.text?.Trim();
        string fullName = createFullNameInput.text?.Trim();
        string department = createDepartmentInput.text?.Trim();
        string password = createPasswordInput.text; // Plain password
        string confirmPassword = createConfirmPasswordInput.text;
        string position = (createPositionDropdown.value > 0 && createPositionDropdown.value < positionOptions.Count) ? positionOptions[createPositionDropdown.value] : null;

        if (string.IsNullOrWhiteSpace(facultyId) || string.IsNullOrWhiteSpace(fullName)) { SetStatus(createStatusText, "Faculty ID and Full Name are required."); return; }
        if (string.IsNullOrEmpty(position)) { SetStatus(createStatusText, "Please select a position (Faculty/Admin)."); return; }

        bool isPasswordBeingSetOrChanged = !string.IsNullOrEmpty(password);
        if (isEditingAccount && !isPasswordBeingSetOrChanged && !string.IsNullOrEmpty(createConfirmPasswordInput.text))
        {
            SetStatus(createStatusText, "Enter new password in both fields to change it, or leave both blank to keep current password."); return;
        }

        if (isPasswordBeingSetOrChanged)
        {
            if (password.Length < 6) { SetStatus(createStatusText, "Password must be at least 6 characters."); return; }
            if (password != confirmPassword) { SetStatus(createStatusText, "Passwords do not match."); return; }
        }
        else if (!isEditingAccount) // New account
        {
            SetStatus(createStatusText, "Password is required for new accounts."); return;
        }

        SetButtonInteractable(createAccountButton, false);
        SetStatus(createStatusText, isEditingAccount ? "Updating profile..." : "Checking ID & creating...");

        try
        {
            FacultyMemberData facultyDataToSave;
            // This will be the plain password if set/changed, otherwise null. FirebaseManager handles hashing.
            string plainPasswordToSet = isPasswordBeingSetOrChanged ? password : null;

            if (isEditingAccount && currentLoggedInFacultyData != null)
            {
                // Create a new object with existing data to avoid modifying cache prematurely
                facultyDataToSave = new FacultyMemberData
                {
                    FacultyID = currentLoggedInFacultyData.FacultyID,
                    FullName = fullName, // Updated
                    Department = department, // Updated
                    Position = position, // Updated
                    // Preserve existing values that are not being changed on this form
                    CurrentLocationName = currentLoggedInFacultyData.CurrentLocationName,
                    AvailabilityStatus = currentLoggedInFacultyData.AvailabilityStatus,
                    // IMPORTANT: Pass the existing hash. If plainPasswordToSet is not null, FirebaseManager will overwrite this.
                    PasswordHash = currentLoggedInFacultyData.PasswordHash
                };
            }
            else // New account
            {
                // FirebaseManager requires a password for new accounts, ensured by prior validation.
                bool exists = await firebaseManager.DoesFacultyExistAsync(facultyId);
                if (exists) { SetStatus(createStatusText, $"Error: Faculty ID '{facultyId}' already exists."); SetButtonInteractable(createAccountButton, true); return; }

                facultyDataToSave = new FacultyMemberData
                {
                    FacultyID = facultyId,
                    FullName = fullName,
                    Department = department,
                    Position = position,
                    CurrentLocationName = null, // Default for new account
                    AvailabilityStatus = null,  // Default for new account
                    PasswordHash = null // Will be set by FirebaseManager from plainPasswordToSet
                };
            }

            // Pass facultyDataToSave and plainPasswordToSet (which can be null if not changing password on edit)
            bool success = await firebaseManager.AddOrUpdateFacultyMemberAsync(facultyDataToSave, plainPasswordToSet);
            if (success)
            {
                SetStatus(createStatusText, isEditingAccount ? "Profile updated successfully!" : "Account created! Redirecting...");
                if (isEditingAccount)
                {
                    // Re-fetch to get the latest data (especially if password hash changed)
                    currentLoggedInFacultyData = await firebaseManager.GetFacultyMemberAsync(facultyId);
                    LoginSuccess(currentLoggedInFacultyData); // Go back to location update panel
                }
                else
                {
                    Invoke(nameof(ShowLoginPanel), 1.5f);
                }
            }
            else { SetStatus(createStatusText, isEditingAccount ? "Error updating profile." : "Error saving account data."); SetButtonInteractable(createAccountButton, true); }
        }
        catch (Exception e) { Debug.LogError($"Account Operation Error: {e.Message}"); SetStatus(createStatusText, "Error performing account operation."); SetButtonInteractable(createAccountButton, true); }
    }

    public async void OnUpdateLocationAndAvailabilityPressed()
    {
        ClearStatusMessages();
        if (!EnsureLoggedIn(locationUpdateStatusText)) return;
        string selectedLocationName = null;
        if (roomDropdown != null && roomDropdown.value > 0)
        {
            selectedLocationName = roomDropdown.options[roomDropdown.value].text;
        }
        else if (locationDropdown != null && locationDropdown.value > 0)
        {
            selectedLocationName = locationDropdown.options[locationDropdown.value].text;
        }
        string selectedStatus = (availabilityDropdown.value > 0)
            ? availabilityDropdown.options[availabilityDropdown.value].text
            : null;


        SetButtonInteractable(updateLocationButton, false); SetStatus(locationUpdateStatusText, "Updating...");
        string facultyId = currentLoggedInFacultyData.FacultyID;
        bool overallSuccess = true;

        try
        {
            if (!await firebaseManager.UpdateFacultyLocationAsync(facultyId, selectedLocationName)) overallSuccess = false;
            else { if (currentLoggedInFacultyData != null) currentLoggedInFacultyData.CurrentLocationName = selectedLocationName; UpdateCurrentLocationDisplay(selectedLocationName); }
        }
        catch (Exception e) { Debug.LogError($"Update Location Error: {e.Message}"); overallSuccess = false; }

        try
        {
            if (!await firebaseManager.UpdateFacultyAvailabilityAsync(facultyId, selectedStatus)) overallSuccess = false;
            else { if (currentLoggedInFacultyData != null) currentLoggedInFacultyData.AvailabilityStatus = selectedStatus; SetAvailabilityDropdownSelection(); }
        }
        catch (Exception e) { Debug.LogError($"Update Availability Error: {e.Message}"); overallSuccess = false; }

        SetStatus(locationUpdateStatusText, overallSuccess ? "Information updated!" : "Update failed for one or more items.");
        SetButtonInteractable(updateLocationButton, true);
    }

    public void OnGoToWayfindingPressed()
    {
        Debug.Log("Faculty Portal: Switching to Wayfinding mode."); HideAllPanels();
        if (navigationManager != null) { navigationManager.ResetToInitialState(); navigationManager.ActivateStudentMode(); }
        else { Debug.LogError("Cannot switch to Wayfinding: NavigationManager ref missing!"); }
    }

    public void OnLogoutButtonPressed()
    {
        // No Firebase Auth SignOut, just clear local session data
        Debug.Log($"Faculty {currentLoggedInFacultyData?.FacultyID ?? "Unknown"} logged out (local session cleared).");
        currentLoggedInFacultyData = null; ShowLoginPanel();
    }

    public void OnChangeRolePressed()
    {
        Debug.Log("Faculty Portal: Change Role button pressed. Returning to Role Selection.");
        if (roleSelectionManager != null) { HideAllPanels(); roleSelectionManager.ShowRoleSelection(); }
        else { Debug.LogError("Cannot change role: RoleSelectionManager reference missing!"); SetStatus(loginStatusText, "Error: Cannot switch role."); }
    }

    public void OnEditAccountPressed()
    {
        if (!EnsureLoggedIn(locationUpdateStatusText)) return;
        Debug.Log($"Editing account for: {currentLoggedInFacultyData.FacultyID}");
        ShowCreateAccountPanel(true);
    }

    public async void OnDeleteAccountPressed()
    {
        if (!EnsureLoggedIn(locationUpdateStatusText)) return;
        if (!await ShowConfirmationDialog($"Delete account for {currentLoggedInFacultyData.FullName} ({currentLoggedInFacultyData.FacultyID})? This is permanent."))
        {
            SetStatus(locationUpdateStatusText, "Deletion cancelled."); return;
        }
        Debug.Log($"Attempting to delete account: {currentLoggedInFacultyData.FacultyID}");
        SetButtonInteractable(deleteAccountButton, false); SetStatus(locationUpdateStatusText, "Deleting account...");
        bool success = await firebaseManager.DeleteFacultyMemberAsync(currentLoggedInFacultyData.FacultyID);
        if (success) { SetStatus(locationUpdateStatusText, "Account deleted successfully."); OnLogoutButtonPressed(); }
        else { SetStatus(locationUpdateStatusText, "Error deleting account."); SetButtonInteractable(deleteAccountButton, true); }
    }
    #endregion

    #region Helper Functions
    private async Task<bool> ShowConfirmationDialog(string message)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.DisplayDialog("Confirm Action", message, "Yes, Delete", "No, Cancel");
#else
        Debug.LogWarning("ShowConfirmationDialog: UI Dialog not implemented for build. Auto-confirming for test. IMPLEMENT A REAL UI DIALOG.");
        await Task.Delay(100); // Simulate user thinking
        // For a real build, you would integrate a UI modal dialog here.
        // For now, returning true for testing in non-editor environments.
        return true;
#endif
    }

    private void PopulateDepartmentDropdown()
    {
        if (locationDropdown == null || departmentRoomMap == null) return;

        locationDropdown.ClearOptions();

        // Filter out keys that look like room names (contain dash or "Room")
        List<string> filteredDepartments = departmentRoomMap.Keys
            .Where(dept => !string.IsNullOrEmpty(dept) &&
                           !dept.Contains("-") &&
                           !dept.ToLowerInvariant().Contains("room"))
            .ToList();

        List<string> options = new() { "Select Department..." };
        options.AddRange(filteredDepartments);
        locationDropdown.AddOptions(options);

        locationDropdown.value = 0;
        locationDropdown.RefreshShownValue();

        roomDropdown.ClearOptions();
        roomDropdown.interactable = false;

        locationDropdown.onValueChanged.RemoveAllListeners();
        locationDropdown.onValueChanged.AddListener(OnDepartmentSelected);
    }

    private void OnDepartmentSelected(int index)
    {
        if (locationDropdown == null || roomDropdown == null) return;

        roomDropdown.ClearOptions();
        roomDropdown.interactable = false;

        if (index == 0)
        {
            SetStatus(locationUpdateStatusText, "Please select a valid department.");
            return;
        }

        string selectedDept = locationDropdown.options[index].text;
        if (!departmentRoomMap.ContainsKey(selectedDept))
        {
            SetStatus(locationUpdateStatusText, $"Department '{selectedDept}' not found.");
            return;
        }

        List<string> rooms = departmentRoomMap[selectedDept];
        if (rooms == null || rooms.Count == 0)
        {
            SetStatus(locationUpdateStatusText, $"No rooms found for {selectedDept}.");
            return;
        }

        // Populate room dropdown
        List<string> roomOptions = new() { "Select Room..." };
        roomOptions.AddRange(rooms);
        roomDropdown.AddOptions(roomOptions);
        roomDropdown.value = 0;
        roomDropdown.RefreshShownValue();
        roomDropdown.interactable = true;

        SetStatus(locationUpdateStatusText, $"Rooms for {selectedDept} loaded.");
    }

    private async Task FetchDepartmentRoomsAndPopulate()
    {
        if (firebaseManager == null || !firebaseManager.IsInitialized)
        {
            Debug.LogError("FacultyPortalManager: FirebaseManager not initialized.");
            return;
        }

        var result = await firebaseManager.FetchDepartmentRoomsAsync(); // Uses endPoints DB
        departmentRoomMap = result ?? new Dictionary<string, List<string>>();
        PopulateDepartmentDropdown();
    }

    private async Task LoginSuccess(FacultyMemberData faculty)
    {
        if (faculty == null) return; // Should not happen if called after successful login
        currentLoggedInFacultyData = faculty;

        if (welcomeText != null)
            welcomeText.text = $"Welcome, {faculty.FullName ?? "N/A"}";

        UpdateCurrentLocationDisplay(faculty.CurrentLocationName);
        PopulateLocationDropdown();
        SetAvailabilityDropdownSelection();

        // Fetch department-room mapping from FirebaseManager
        var result = await firebaseManager.FetchDepartmentRoomsAsync();
        departmentRoomMap = result ?? new Dictionary<string, List<string>>();
        PopulateDepartmentDropdown();

        if (createPositionDropdown != null)
            SetDropdownSelection(createPositionDropdown, positionOptions, faculty.Position);

        ShowLocationUpdatePanel();
    }

    private void LoginFail(string attemptedId, string message = "Error: Invalid Faculty ID or Password.")
    {
        Debug.LogWarning($"Faculty login failed for ID: {attemptedId}. Reason: {message}");
        SetStatus(loginStatusText, message); currentLoggedInFacultyData = null;
    }

    private void PopulateLocationDropdown()
    {
        if (locationDropdown == null || databaseManager == null)
        {
            Debug.LogError("PopulateLocationDropdown: Refs missing.");
            return;
        }

        try
        {
            availableEndPoints = databaseManager.GetAllEndPoints() ?? new List<PathPointData>();
        }
        catch (Exception e)
        {
            Debug.LogError($"SQLite error: {e.Message}");
            availableEndPoints = new List<PathPointData>();
        }

        // Filter out room-like entries BEFORE populating
        List<PathPointData> filteredEndPoints = availableEndPoints
            .Where(p => p != null &&
                        !string.IsNullOrEmpty(p.Name) &&
                        !p.Name.Contains("-") &&
                        !p.Name.ToLowerInvariant().Contains("room"))
            .ToList();

        locationDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
    {
        new TMP_Dropdown.OptionData("Select Location...")
    };

        int selectedIndex = 0;
        string currentLocation = currentLoggedInFacultyData?.CurrentLocationName;

        foreach (var point in filteredEndPoints)
        {
            options.Add(new TMP_Dropdown.OptionData(point.Name));
            if (!string.IsNullOrEmpty(currentLocation) &&
                point.Name.Equals(currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = options.Count - 1;
            }
        }

        if (options.Count == 1)
        {
            options.Clear();
            options.Add(new TMP_Dropdown.OptionData("No Locations Available"));
            locationDropdown.interactable = false;
        }
        else
        {
            locationDropdown.interactable = true;
        }

        locationDropdown.options = options;
        locationDropdown.value = selectedIndex;
        locationDropdown.RefreshShownValue();
    }

    private void UpdateCurrentLocationDisplay(string locationName)
    {
        if (currentLocationText != null) { currentLocationText.text = $"Current Location: {(string.IsNullOrEmpty(locationName) ? "Not Set" : locationName)}"; }
    }

    // ValidateCreateAccountInput is largely incorporated into OnCreateOrUpdateAccountButtonPressed directly.
    // Redundant method removed for brevity, as specific checks are handled within the button press logic.

    private bool EnsureLoggedIn(TextMeshProUGUI statusLabel)
    {
        if (currentLoggedInFacultyData == null || string.IsNullOrEmpty(currentLoggedInFacultyData.FacultyID))
        {
            Debug.LogError("Action requires login, but no faculty data is cached.");
            SetStatus(statusLabel, "Error: Session expired. Please log in again."); ShowLoginPanel(); return false;
        }
        return true;
    }

    private void SetStatus(TextMeshProUGUI label, string message)
    {
        if (label != null) label.text = message;
        else if (!string.IsNullOrEmpty(message)) Debug.LogWarning($"Status label null, cannot display: {message}");
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    private void SetupAvailabilityDropdown()
    {
        if (availabilityDropdown == null) return;
        availabilityDropdown.ClearOptions();
        availabilityDropdown.options = availabilityStatuses.Select(status => new TMP_Dropdown.OptionData(status)).ToList();
        availabilityDropdown.value = 0; availabilityDropdown.RefreshShownValue();
    }

    private void SetAvailabilityDropdownSelection()
    {
        if (availabilityDropdown == null || currentLoggedInFacultyData == null) return;
        string currentStatus = currentLoggedInFacultyData.AvailabilityStatus;
        SetDropdownSelection(availabilityDropdown, availabilityStatuses, currentStatus);
    }

    private void SetupPositionDropdown()
    {
        if (createPositionDropdown == null) return;
        createPositionDropdown.ClearOptions();
        createPositionDropdown.options = positionOptions.Select(pos => new TMP_Dropdown.OptionData(pos)).ToList();
        createPositionDropdown.value = 0;
        createPositionDropdown.RefreshShownValue();
    }

    private void SetDropdownSelection(TMP_Dropdown dropdown, List<string> optionsList, string valueToSelect)
    {
        if (dropdown == null) return;
        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(valueToSelect))
        {
            int index = optionsList.FindIndex(opt => opt.Equals(valueToSelect, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) { selectedIndex = index; }
            else { Debug.LogWarning($"Value '{valueToSelect}' not found in dropdown {dropdown.name}. Defaulting to prompt."); }
        }
        dropdown.value = selectedIndex;
        dropdown.RefreshShownValue();
    }
    #endregion
}
