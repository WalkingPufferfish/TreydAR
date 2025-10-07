// DynamicArrowGuide.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DynamicArrowGuide : MonoBehaviour
{
    [Header("References")]
    public GameObject arrowPrefab;
    public Camera arCamera;
    private NavigationManager navigationManager;

    [Header("Visual Settings")]
    public float arrowYOffset = 0.1f;
    public float forwardOffset = 0.5f;
    public float smoothingFactor = 0.15f;
    public float minDistanceToNextNode = 0.4f;

    private GameObject instantiatedArrow;
    private List<Vector3> currentPath;
    private int currentPathIndex = 0;
    private Vector3 smoothedArrowPosition; // No longer used for positioning, but kept for reference
    private Quaternion smoothedArrowRotation; // No longer used for smoothing, but kept for reference
    private bool isInitialized = false;
    private bool isArrowVisible = false;

    public void Initialize()
    {
        if (isInitialized) return;
        if (arrowPrefab == null || arCamera == null)
        {
            Debug.LogError("ArrowGuide cannot initialize: Prefab or Camera is missing!");
            return;
        }

        navigationManager = FindObjectOfType<NavigationManager>();
        // <<< MODIFICATION START: Make arrow a child of the camera for stationary behavior >>>
        // Original line: instantiatedArrow = Instantiate(arrowPrefab, transform);
        instantiatedArrow = Instantiate(arrowPrefab, arCamera.transform);

        // Set its fixed local position relative to the camera
        instantiatedArrow.transform.localPosition = new Vector3(0, arrowYOffset, forwardOffset);
        // <<< MODIFICATION END >>>

        HideArrow();
        isInitialized = true;
    }

    public void SetPath(List<Vector3> newPath)
    {
        if (!isInitialized) Initialize();
        currentPath = newPath;
        currentPathIndex = 0;
        if (currentPath != null && currentPath.Count > 1) ShowArrow();
        else HideArrow();
    }

    public void UpdateArrow(Vector3 currentUserPosition, List<Vector3> path)
    {
        if (!isArrowVisible || path == null || path.Count < 1)
        {
            HideArrow();
            return;
        }
        FindClosestPathSegment(currentUserPosition);
        Vector3 targetPoint = (currentPathIndex < 0 || currentPathIndex >= path.Count - 1) ? path.Last() : path[currentPathIndex + 1];
        UpdateArrowTransform(currentUserPosition, targetPoint);
    }

    private void FindClosestPathSegment(Vector3 userPos)
    {
        if (currentPath == null || currentPath.Count < 2) { currentPathIndex = -1; return; }
        float closestDistSq = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 p1 = currentPath[i]; Vector3 p2 = currentPath[i + 1];
            Vector3 segmentDir = p2 - p1; float segmentLenSq = segmentDir.sqrMagnitude;
            if (segmentLenSq < 0.001f) continue;
            float t = Mathf.Clamp01(Vector3.Dot(userPos - p1, segmentDir) / segmentLenSq);
            float distSq = (userPos - (p1 + t * segmentDir)).sqrMagnitude;
            if (distSq < closestDistSq) { closestDistSq = distSq; bestIndex = i; }
        }
        if (bestIndex != -1 && bestIndex < currentPath.Count - 1 && Vector3.Distance(userPos, currentPath[bestIndex + 1]) < minDistanceToNextNode)
        {
            if (bestIndex < currentPath.Count - 2) bestIndex++;
        }
        currentPathIndex = bestIndex;
    }

    // <<< MODIFICATION START: Simplified transform update for stationary arrow >>>
    private void UpdateArrowTransform(Vector3 fromPos, Vector3 toPos)
    {
        // --- 1. Calculate the raw look direction in World Space ---
        Vector3 lookDirectionWorld = (toPos - fromPos);
        lookDirectionWorld.y = 0; // Flatten the vector to act like a compass

        // --- 2. Get the User's Spawn Rotation Offset ---
        Quaternion spawnRotationOffset = Quaternion.identity;
        if (navigationManager != null)
        {
            spawnRotationOffset = navigationManager.GetUserSpawnRotation();
        }

        // --- 3. THIS IS THE CRITICAL FIX: Convert the World Direction to User's Local Direction ---
        // We rotate the world direction vector by the *inverse* of the user's spawn rotation.
        // This effectively "un-rotates" the world so that the user's original "forward" becomes the new "forward".
        Vector3 lookDirectionLocal = Quaternion.Inverse(spawnRotationOffset) * lookDirectionWorld;

        // --- 4. Create the final rotation from the corrected local direction ---
        Quaternion targetRotation;
        if (lookDirectionLocal.sqrMagnitude > 0.001f)
        {
            targetRotation = Quaternion.LookRotation(lookDirectionLocal);
        }
        else
        {
            targetRotation = instantiatedArrow.transform.localRotation; // Keep current rotation
        }

        // --- 5. Smoothly Apply the Rotation ---
        // We use localRotation because the arrow is a child of the camera.
        instantiatedArrow.transform.localRotation = Quaternion.Slerp(instantiatedArrow.transform.localRotation, targetRotation, smoothingFactor);
    }
    // <<< MODIFICATION END >>>

    public void ClearArrow() { HideArrow(); currentPath = null; }
    private void ShowArrow() { if (instantiatedArrow != null && !isArrowVisible) { instantiatedArrow.SetActive(true); isArrowVisible = true; } }
    private void HideArrow() { if (instantiatedArrow != null && isArrowVisible) { instantiatedArrow.SetActive(false); isArrowVisible = false; } isArrowVisible = false; }
}