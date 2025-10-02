using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RoomSearchUIController : MonoBehaviour
{
    public TMP_Dropdown DepartmentDropdown; 
    public Button FindFaculty_Button;
    public TMP_Dropdown Dropdown; // Room dropdown
    public TMP_Text TextDisplay;
    public FacultyFinder FindFaculty;
    public NavigationManager navigationManager;

    private Dictionary<string, List<string>> departmentRooms = new();
    private string selectedDepartment = "";

    void Start()
    {
        DepartmentDropdown.onValueChanged.AddListener(OnDepartmentSelected);
        Dropdown.onValueChanged.AddListener(OnRoomSelected);

        FindFaculty.FetchDepartmentRooms(OnDepartmentDataReceived);
    }

    void OnDepartmentSelected(int index)
    {
        selectedDepartment = DepartmentDropdown.options[index].text;
        UpdateRoomDropdown(selectedDepartment);
    }

    void OnDepartmentDataReceived(Dictionary<string, List<string>> data)
    {
        departmentRooms = data;

        // Populate department dropdown
        DepartmentDropdown.ClearOptions();
        DepartmentDropdown.AddOptions(new List<string>(departmentRooms.Keys));

        // Optionally preload first department
        if (departmentRooms.Count > 0)
        {
            selectedDepartment = DepartmentDropdown.options[0].text;
            UpdateRoomDropdown(selectedDepartment);
        }

    }

    void OpenDepartmentSelector()
    {
        // TODO: Replace with actual department selection UI
        // For now, cycle through departments for testing
        List<string> departments = new List<string>(departmentRooms.Keys);
        int currentIndex = departments.IndexOf(selectedDepartment);
        int nextIndex = (currentIndex + 1) % departments.Count;
        selectedDepartment = departments[nextIndex];

        UpdateRoomDropdown(selectedDepartment);
    }

    void UpdateRoomDropdown(string department)
    {
        if (!departmentRooms.ContainsKey(department)) return;

        Dropdown.ClearOptions();
        Dropdown.AddOptions(departmentRooms[department]);
        TextDisplay.text = $"Rooms for {department} loaded.";
    }

    void OnRoomSelected(int index)
    {
        string selectedRoom = Dropdown.options[index].text;
        TextDisplay.text = $"Selected: {selectedRoom}";
        navigationManager.NavigateToRoom(selectedRoom);
    }
}