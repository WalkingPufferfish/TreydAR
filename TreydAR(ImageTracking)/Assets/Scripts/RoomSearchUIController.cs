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

    [Header("Firebase Reference")]
    public FirebaseManager firebaseManager;

    private Dictionary<string, FirebaseManager.DepartmentData> departmentData = new();
    private string selectedDepartment = "";
    private string selectedRoom = "";

    async void Start()
    {
        Debug.Log("RoomSearchUIController: Start() triggered");

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

        selectedDepartment = DepartmentDropdown.options[1].text; // First actual department
        UpdateRoomDropdown(selectedDepartment);
        NavigateToDepartment(selectedDepartment);
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
        if (index < 0 || index >= RoomDropdown.options.Count) return;

        if (index == 0)
        {
            selectedRoom = "";
            TextDisplay.text = "Please select a valid room.";
            return;
        }

        selectedRoom = RoomDropdown.options[index].text;
        Debug.Log($"Room selected: {selectedRoom}");
        TextDisplay.text = $"Selected room: {selectedRoom}";
        navigationManager.NavigateToRoom(selectedRoom);
    }

    void NavigateToDepartment(string department)
    {
        if (!departmentData.ContainsKey(department)) return;

        string anchorName = departmentData[department].Name;
        if (!string.IsNullOrEmpty(anchorName))
        {
            Debug.Log($"Navigating to department anchor: {anchorName}");
            TextDisplay.text = $"Navigating to department: {department}";
            navigationManager.NavigateToRoom(anchorName);
        }
    }
}