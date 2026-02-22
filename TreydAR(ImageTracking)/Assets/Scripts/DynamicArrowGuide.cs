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
    public float arrowYOffset = 0f; // Set to 0 for vertical centering
    public float forwardOffset = 1.0f; // A comfortable distance in front of the camera
    public float smoothingFactor = 0.15f;
    public float minDistanceToNextNode = 1.0f;

    private GameObject instantiatedArrow;
    private List<Vector3> currentPath;
    private int currentPathIndex = 0;
    private Vector3 smoothedArrowPosition; // Original variable, kept for reference
    private Quaternion smoothedArrowRotation; // Original variable, kept for reference
    private bool isInitialized = false;
    private bool isArrowVisible = false;

    public void Initialize()
    {
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

        isInitialized = true;
        isArrowVisible = true;
    }

    public void SetPath(List<Vector3> newPath)
    {
        currentPath = newPath;
        currentPathIndex = 0;
        if (currentPath != null && currentPath.Count > 1)
        {
            ShowArrow();
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
            return;
        }

        // This finds the closest segment and advances the index if needed
        FindClosestPathSegment(currentUserPosition);

        // This determines the next corner to point towards
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

        // Logic to advance to the next segment when the user gets close to the next corner
        if (bestIndex != -1 && bestIndex < currentPath.Count - 1 && Vector3.Distance(userPos, currentPath[bestIndex + 1]) < minDistanceToNextNode)
        {
            if (bestIndex < currentPath.Count - 2) bestIndex++;
        }
        currentPathIndex = bestIndex;
    }

    private void UpdateArrowTransform(Vector3 fromPos, Vector3 toPos)
    {
        // This is the final, correct, simpler rotation logic
        if (instantiatedArrow == null) return;

        Vector3 arrowWorldPosition = instantiatedArrow.transform.position;
        Vector3 lookDirection = (toPos - arrowWorldPosition);
        lookDirection.y = 0;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            instantiatedArrow.transform.rotation = Quaternion.Slerp(instantiatedArrow.transform.rotation, targetRotation, smoothingFactor);
        }
    }

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