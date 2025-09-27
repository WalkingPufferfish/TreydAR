using UnityEngine;
using UnityEngine.UI; // Added for Button

public class RoleSelectionManager : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The panel containing the Student/Faculty role selection buttons.")]
    public GameObject roleSelectionPanel; // Assign your Role Selection Panel GameObject

    [Header("Manager References")]
    [Tooltip("Reference to the NavigationManager component.")]
    public NavigationManager navigationManager; // Assign NavigationManager GameObject
    [Tooltip("Reference to the FacultyPortalManager component.")]
    public FacultyPortalManager facultyPortalManager; // Assign FacultyPortalManager GameObject

    [Header("Role Selection Buttons")]
    [Tooltip("Button to select the Student role.")]
    public Button studentButton; // Assign Student Role Button
    [Tooltip("Button to select the Faculty role.")]
    public Button facultyButton; // Assign Faculty Role Button

    // --- NEW REFERENCE ---
    [Header("Navigation UI Buttons")]
    [Tooltip("The 'Back' button located on the main Navigation/Wayfinding UI that returns to Role Selection.")]
    public Button backToRoleSelectionButton; // Assign the NEW Back button here
    // --- ------------- ---

    void Start()
    {
        // --- Validate References ---
        bool refsValid = true;
        if (roleSelectionPanel == null) { Debug.LogError("RoleSelectionManager: Role Selection Panel not assigned!", this); refsValid = false; }
        if (navigationManager == null) { Debug.LogError("RoleSelectionManager: Navigation Manager not assigned!", this); refsValid = false; }
        if (facultyPortalManager == null) { Debug.LogError("RoleSelectionManager: Faculty Portal Manager not assigned!", this); refsValid = false; }
        if (studentButton == null) { Debug.LogError("RoleSelectionManager: Student Button not assigned!", this); refsValid = false; }
        if (facultyButton == null) { Debug.LogError("RoleSelectionManager: Faculty Button not assigned!", this); refsValid = false; }
        if (backToRoleSelectionButton == null) { Debug.LogError("RoleSelectionManager: Back To Role Selection Button not assigned!", this); refsValid = false; } // Essential for back flow

        if (!refsValid)
        {
            Debug.LogError("RoleSelectionManager disabled due to missing references.");
            enabled = false;
            return;
        }
        // ---------------------------

        // --- Assign button listeners ---
        studentButton.onClick.AddListener(SelectStudentRole);
        facultyButton.onClick.AddListener(SelectFacultyRole);
        backToRoleSelectionButton.onClick.AddListener(ShowRoleSelection); // Back button calls ShowRoleSelection
        // ---------------------------

        // --- Initial State ---
        ShowRoleSelection(); // Start by showing the role selection panel
        // -------------------
    }

    void OnDestroy()
    {
        // Clean up listeners
        if (studentButton != null) studentButton.onClick.RemoveListener(SelectStudentRole);
        if (facultyButton != null) facultyButton.onClick.RemoveListener(SelectFacultyRole);
        if (backToRoleSelectionButton != null) backToRoleSelectionButton.onClick.RemoveListener(ShowRoleSelection);
    }

    /// <summary>
    /// Activates the Student mode and hides other panels.
    /// </summary>
    void SelectStudentRole()
    {
        Debug.Log("Role Selected: Student");
        if (roleSelectionPanel != null) roleSelectionPanel.SetActive(false);
        facultyPortalManager?.HideAllPanels(); // Ensure faculty UI is hidden
        navigationManager?.ActivateStudentMode(); // Activate student navigation flow (shows nav UI, prompts scan)
    }

    /// <summary>
    /// Activates the Faculty mode and hides other panels.
    /// </summary>
    void SelectFacultyRole()
    {
        Debug.Log("Role Selected: Faculty");
        if (roleSelectionPanel != null) roleSelectionPanel.SetActive(false);
        navigationManager?.ResetToInitialState(); // Reset student nav state if active (hides nav UI)
        facultyPortalManager?.ActivateFacultyMode(); // Activate faculty portal flow (shows login panel)
    }

    /// <summary>
    /// Shows the Role Selection panel and resets/hides the other modes (Student Navigation, Faculty Portal).
    /// Called initially and by the "Back" button from the Navigation UI.
    /// </summary>
    public void ShowRoleSelection() // Public in case called externally, e.g., on app start delay
    {
        Debug.Log("Showing Role Selection Panel and resetting other managers.");
        // Reset/hide other managers/UI states FIRST
        navigationManager?.ResetToInitialState(); // Fully reset NavigationManager (hides its UI)
        facultyPortalManager?.HideAllPanels();    // Hide all faculty panels

        // Then, show the role selection panel
        if (roleSelectionPanel != null) roleSelectionPanel.SetActive(true);
    }
}