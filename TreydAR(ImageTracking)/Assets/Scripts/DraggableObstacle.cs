// DraggableObstacle.cs
using UnityEngine;
using UnityEngine.AI; // Required for NavMeshObstacle

[RequireComponent(typeof(NavMeshObstacle))]
public class DraggableObstacle : MonoBehaviour
{
    // This script is now primarily a marker component.
    // The required NavMeshObstacle component handles all the pathfinding interactions.
    // No code is needed here for basic carving functionality.
}