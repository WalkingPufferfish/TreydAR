// DynamicArrowGuide.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DynamicArrowGuide : MonoBehaviour
{
    [Header("References")]
    public GameObject arrowPrefab;
    public Camera arCamera;

    [Header("Visual Settings")]
    public float arrowYOffset = 0.1f;
    public float forwardOffset = 0.5f;
    public float smoothingFactor = 0.15f;
    public float minDistanceToNextNode = 0.4f;

    private GameObject instantiatedArrow;
    private List<Vector3> currentPath;
    private int currentPathIndex = 0;
    private Vector3 smoothedArrowPosition;
    private Quaternion smoothedArrowRotation;
    private bool isInitialized = false;
    private bool isArrowVisible = false;

    public void Initialize()
    {
        if (isInitialized) return;
        if (arrowPrefab == null || arCamera == null) return;
        instantiatedArrow = Instantiate(arrowPrefab, transform);
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

    private void UpdateArrowTransform(Vector3 fromPos, Vector3 toPos)
    {
        Vector3 basePosition = arCamera.transform.position + Vector3.up * arrowYOffset;
        Vector3 targetDirection = (toPos - arCamera.transform.position); targetDirection.y = 0;
        Vector3 targetPosition = basePosition + targetDirection.normalized * forwardOffset;
        Vector3 lookDirection = (toPos - fromPos); lookDirection.y = 0;
        Quaternion targetRotation = (lookDirection.sqrMagnitude > 0.001f) ? Quaternion.LookRotation(lookDirection) : smoothedArrowRotation;

        smoothedArrowPosition = Vector3.Lerp(instantiatedArrow.transform.position, targetPosition, smoothingFactor);
        smoothedArrowRotation = Quaternion.Slerp(instantiatedArrow.transform.rotation, targetRotation, smoothingFactor);
        instantiatedArrow.transform.SetPositionAndRotation(smoothedArrowPosition, smoothedArrowRotation);
    }

    public void ClearArrow() { HideArrow(); currentPath = null; }
    private void ShowArrow() { if (instantiatedArrow != null && !isArrowVisible) { instantiatedArrow.SetActive(true); isArrowVisible = true; } }
    private void HideArrow() { if (instantiatedArrow != null && isArrowVisible) { instantiatedArrow.SetActive(false); isArrowVisible = false; } isArrowVisible = false; }
}