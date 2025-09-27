using UnityEngine;
using TMPro; // Required for TextMeshProUGUI

/// <summary>
/// Holds references to the UI elements within a Faculty List Item prefab.
/// Attach this script to the root of your FacultyListItem prefab.
/// </summary>
public class FacultyListItemUI : MonoBehaviour
{
    // Assign these TextMeshProUGUI components in the prefab's Inspector
    [Tooltip("Text field for displaying the faculty member's full name.")]
    public TextMeshProUGUI NameText;

    [Tooltip("Text field for displaying the faculty member's department.")]
    public TextMeshProUGUI DeptText;

    [Tooltip("Text field for displaying the faculty member's current location.")]
    public TextMeshProUGUI LocText;

    [Tooltip("Text field for displaying the faculty member's availability status.")]
    public TextMeshProUGUI StatusText;

    [Tooltip("Text field for displaying the faculty member's position (Faculty/Admin).")] // <<< NEW
    public TextMeshProUGUI PositionText; // <<< NEW FIELD

}