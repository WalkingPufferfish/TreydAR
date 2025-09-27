// PathVisualizer.cs
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("Visual Settings")]
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    public Color lineColor = Color.cyan;
    public float yOffset = 0.02f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        SetupLineRenderer();
    }

    void SetupLineRenderer()
    {
        if (lineRenderer == null) return;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        if (lineMaterial != null) lineRenderer.material = lineMaterial;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    public void DrawPath(Vector3[] pathCorners)
    {
        if (lineRenderer == null || pathCorners == null || pathCorners.Length < 2)
        {
            ClearPath();
            return;
        }

        Vector3[] positions = pathCorners.Select(p => p + Vector3.up * yOffset).ToArray();
        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
        lineRenderer.enabled = true;
    }

    public void ClearPath()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }
}