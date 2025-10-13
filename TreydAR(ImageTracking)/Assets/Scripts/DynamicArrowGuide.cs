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
    private Vector3 smoothedArrowPosition; // Original variable, kept for reference
    private Quaternion smoothedArrowRotation; // Original variable, kept for reference
    private bool isInitialized = false;
    private bool isArrowVisible = false;

    // <<< MODIFICATION: Initialize now creates a visible arrow on demand >>>
    public void Initialize()
    {
        // Add a check to prevent re-initializing if it already exists
        if (isInitialized && instantiatedArrow != null)
        {
            return;
        }

        if (arrowPrefab == null || arCamera == null)
        {
            Debug.LogError("ArrowGuide cannot initialize: Prefab or Camera is missing!");
            return;
        }

        navigationManager = FindObjectOfType<NavigationManager>();

        instantiatedArrow = Instantiate(arrowPrefab, arCamera.transform);
        instantiatedArrow.transform.localPosition = new Vector3(0, arrowYOffset, forwardOffset);

        // HideArrow(); // This is no longer called here. The arrow is visible on creation.

        isInitialized = true;
        isArrowVisible = true; // Set to true as it's now visible by default when created.
    }

    public void SetPath(List<Vector3> newPath)
    {
        // if (!isInitialized) Initialize(); // Initialize is now called by NavigationManager
        currentPath = newPath;
        currentPathIndex = 0;
        if (currentPath != null && currentPath.Count > 1)
        {
            ShowArrow(); // This will ensure it's active if it was previously hidden
        }
        else
        {
            HideArrow();
        }
    }

    public void UpdateArrow(Vector3 currentUserPosition, List<Vector3> path)
    {
        if (!isArrowVisible || path == null || path.Count < 1)
        {
            // HideArrow(); // Let ClearArrow handle destruction
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

    private void UpdateArrowTransform(Vector3 fromPos, Vector3 toPos)
    {
        // This is the final, correct, simpler rotation logic
        Vector3 arrowWorldPosition = instantiatedArrow.transform.position;
        Vector3 lookDirection = (toPos - arrowWorldPosition);
        lookDirection.y = 0;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            instantiatedArrow.transform.rotation = Quaternion.Slerp(instantiatedArrow.transform.rotation, targetRotation, smoothingFactor);
        }
    }

    // <<< MODIFICATION: ClearArrow now destroys the arrow instance >>>
    public void ClearArrow()
    {
        if (instantiatedArrow != null)
        {
            Destroy(instantiatedArrow);
            instantiatedArrow = null;
        }
        isInitialized = false;
        isArrowVisible = false;
        currentPath = null;
    }

    private void ShowArrow() { if (instantiatedArrow != null && !isArrowVisible) { instantiatedArrow.SetActive(true); isArrowVisible = true; } }
    private void HideArrow() { if (instantiatedArrow != null && isArrowVisible) { instantiatedArrow.SetActive(false); isArrowVisible = false; } }
}