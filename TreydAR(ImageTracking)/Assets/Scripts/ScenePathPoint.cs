using UnityEngine;

/// <summary>
/// Attach this component to GameObjects in the scene that represent
/// points (Start, End, Waypoint) to be synced with the DatabaseManager.
/// The Testing script will find these components.
/// </summary>
public class ScenePathPoint : MonoBehaviour
{
    [Tooltip("Unique name for this point. This will be used as the 'Name' field in the database.")]
    public string pointName = "New Scene Point"; // Provide a default

    [Tooltip("Tag indicating the point's purpose (e.g., 'StartPoint', 'EndPoint', 'Waypoint', 'Untagged'). Case-sensitive.")]
    public string pointTag = "Untagged"; // Default tag

    // You can add Gizmos drawing here later if you want visual cues in the Scene view
    // void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.blue; // Example color
    //     Gizmos.DrawWireSphere(transform.position, 0.3f);
    //     // Optionally draw the name
    //     #if UNITY_EDITOR
    //     UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, pointName);
    //     #endif
    // }
}