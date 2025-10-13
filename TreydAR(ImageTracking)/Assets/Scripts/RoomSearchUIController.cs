using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Firebase;
using Firebase.Database;

public class RoomSearchUIController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown DepartmentDropdown;
    public TMP_Dropdown RoomDropdown;
    public TMP_Text TextDisplay;
    public NavigationManager navigationManager;
    public DatabaseManager databaseManager;

    [Header("Firebase Reference")]
    public FirebaseManager firebaseManager;

    private Dictionary<string, FirebaseManager.DepartmentData> departmentData = new();
    private string selectedDepartment = "";
    private string selectedRoom = "";

    async void Start()
    {
        Debug.Log("RoomSearchUIController: Start() triggered");

        RoomDropdown.ClearOptions();
        RoomDropdown.interactable = false;

        DepartmentDropdown.onValueChanged.RemoveAllListeners();
        RoomDropdown.onValueChanged.RemoveAllListeners();

        DepartmentDropdown.onValueChanged.AddListener(OnDepartmentSelected);
        RoomDropdown.onValueChanged.AddListener(OnRoomSelected);

        departmentData = await firebaseManager.GetDepartmentRoomDataAsync();
        Debug.Log($"Fetched departments: {string.Join(", ", departmentData.Keys)}");

        PopulateDepartmentDropdown();
    }

    void PopulateDepartmentDropdown()
    {
        Debug.Log("Populating department dropdown...");
        DepartmentDropdown.ClearOptions();

        List<string> options = new List<string> { "Select Department..." };
        options.AddRange(departmentData.Keys);
        DepartmentDropdown.AddOptions(options);

        DepartmentDropdown.value = 0;
        DepartmentDropdown.RefreshShownValue();

        selectedDepartment = "";
        RoomDropdown.ClearOptions();
        RoomDropdown.interactable = false;
        TextDisplay.text = "Department...";
    }

    void OnDepartmentSelected(int index)
    {
        if (index == 0)
        {
            selectedDepartment = "";
            RoomDropdown.ClearOptions();
            RoomDropdown.interactable = false;
            TextDisplay.text = "Please select a valid department.";
            return;
        }

        selectedDepartment = DepartmentDropdown.options[index].text;
        Debug.Log($"Department selected: {selectedDepartment}");

        UpdateRoomDropdown(selectedDepartment);
        NavigateToDepartment(selectedDepartment);
    }

    void UpdateRoomDropdown(string department)
    {
        RoomDropdown.ClearOptions();
        RoomDropdown.interactable = false;

        if (string.IsNullOrEmpty(department) || !departmentData.ContainsKey(department))
        {
            TextDisplay.text = "No department selected or department not found.";
            return;
        }

        var rooms = departmentData[department].rooms ?? new List<string>();
        Debug.Log($"Selected department: {department}, Rooms count: {rooms.Count}");

        if (rooms.Count > 0)
        {
            List<string> roomOptions = new List<string> { "Select Room..." };
            roomOptions.AddRange(rooms);

            RoomDropdown.AddOptions(roomOptions);
            RoomDropdown.interactable = true;
            RoomDropdown.value = 0;
            RoomDropdown.RefreshShownValue();
            TextDisplay.text = $"Rooms for {department} loaded.";
        }
        else
        {
            TextDisplay.text = $"No rooms assigned to {department}.";
        }
    }

    void OnRoomSelected(int index)
    {
        if (index <= 0) // Handles "Select Room..."
        {
            // Optionally stop navigation if the user de-selects a room
            navigationManager?.StopNavigation();
            return;
        }
        selectedRoom = RoomDropdown.options[index].text;
        TextDisplay.text = $"Navigating to {selectedRoom}...";
        NavigateTo(selectedRoom); // Use our new unified method
    }

    void NavigateToDepartment(string department)
    {
        if (!departmentData.ContainsKey(department)) return;

        string anchorName = departmentData[department].Name;
        if (!string.IsNullOrEmpty(anchorName))
        {
            TextDisplay.text = $"Navigating to department: {department}...";
            NavigateTo(anchorName); // Use our new unified method
        }
    }

    private void NavigateTo(string destinationName)
    {
        if (databaseManager == null || navigationManager == null)
        {
            Debug.LogError("DatabaseManager or NavigationManager is not assigned!");
            return;
        }

        // 1. Get the destination's data (including its LOCAL position) from the database.
        PathPointData destinationPoint = databaseManager.GetPathPointByName(destinationName);

        if (destinationPoint != null)
        {
            // 2. Pass this data to the one, correct navigation method.
            //    NavigationManager will now handle the local-to-world conversion.
            navigationManager.StartNavigationToPoint(destinationPoint);
        }
        else
        {
            Debug.LogWarning($"Destination '{destinationName}' not found in the database.");
            TextDisplay.text = $"Destination '{destinationName}' not found.";
        }
    }
}