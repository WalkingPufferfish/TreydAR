using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[RequireComponent(typeof(DatabaseManager))]
public class Testing : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Drag the GameObject that has the FirebaseManager script on it into this slot.")]
    public FirebaseManager firebaseManager;

    // <<< MODIFICATION #1: Added a new public field for the environment's root transform.
    [Header("Scene References for Syncing")]
    [Tooltip("Drag the ROOT GameObject of your environment map (e.g., GridEnvironmentAnchor) here. This is needed to calculate correct local positions for endpoints.")]
    public Transform environmentRoot;

    [HideInInspector] public DatabaseManager databaseManager;
    [HideInInspector] public int selectedStartPointIndex = 0;
    [HideInInspector] public int selectedEndPointIndex = 0;
    [HideInInspector] public bool showAllPointsInEditor = false;

    private List<PathPointData> availablePoints = new List<PathPointData>();
    public IReadOnlyList<PathPointData> AvailablePoints => availablePoints.AsReadOnly();

    void Awake()
    {
        databaseManager = GetComponent<DatabaseManager>();
        if (databaseManager == null) { Debug.LogError("DB Manager missing", this); enabled = false; return; }
        if (firebaseManager == null) Debug.LogError("FirebaseManager missing", this);
        LoadPointsFromDatabase();
    }

    public void LoadPointsFromDatabase()
    {
        if (databaseManager != null)
        {
            availablePoints = databaseManager.GetAllPathPoints() ?? new List<PathPointData>();
            Debug.Log($"Testing: Loaded {availablePoints.Count} points from database.");
            selectedStartPointIndex = Mathf.Clamp(selectedStartPointIndex, 0, Mathf.Max(0, availablePoints.Count - 1));
            selectedEndPointIndex = Mathf.Clamp(selectedEndPointIndex, 0, Mathf.Max(0, availablePoints.Count - 1));
        }
    }

    public void SyncScenePointsToDatabase()
    {
        if (databaseManager == null) { Debug.LogError("Cannot sync: DatabaseManager missing."); return; }
        
        // <<< MODIFICATION #2: Added a check to ensure the environment root is assigned.
        if (environmentRoot == null)
        {
            Debug.LogError("SYNC FAILED: The 'Environment Root' transform is not assigned in the Inspector on the Testing script. Please drag your map's parent object into this slot.", this);
            return;
        }

        Debug.Log("Starting Scene->DB Sync (using LOCAL positions)...");

        List<PathPointData> pointsInDb = databaseManager.GetAllPathPoints() ?? new List<PathPointData>();
        Dictionary<string, PathPointData> dbPointsDict = pointsInDb.Where(p => p != null && !string.IsNullOrEmpty(p.Name)).ToDictionary(p => p.Name, p => p);
        
        ScenePathPoint[] scenePointComponents = FindObjectsByType<ScenePathPoint>(FindObjectsSortMode.None);
        HashSet<string> scenePointNamesProcessed = new HashSet<string>();
        int added = 0, updated = 0, skipped = 0;

        foreach (ScenePathPoint scenePoint in scenePointComponents)
        {
            if (scenePoint == null) continue;
            string name = scenePoint.pointName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || scenePointNamesProcessed.Contains(name)) { skipped++; continue; }

            // <<< MODIFICATION #3: Calculate LOCAL position instead of WORLD position.
            // This converts the point's world position into a position relative to the environmentRoot's pivot.
            Vector3 localPosition = environmentRoot.InverseTransformPoint(scenePoint.transform.position);
            
            string tag = string.IsNullOrWhiteSpace(scenePoint.pointTag) ? "Untagged" : scenePoint.pointTag.Trim();

            // Use the calculated localPosition to create the data point.
            PathPointData pointData = new PathPointData { Name = name, Position = localPosition, PointTag = tag };

            if (dbPointsDict.TryGetValue(name, out PathPointData existingDbPoint))
            {
                pointData.ID = existingDbPoint.ID;
                // Compare the new local position to the old one.
                if (!Mathf.Approximately((existingDbPoint.Position - pointData.Position).sqrMagnitude, 0f) || existingDbPoint.PointTag != pointData.PointTag)
                {
                    if (databaseManager.UpdatePathPoint(pointData)) updated++;
                }
                dbPointsDict.Remove(name);
            }
            else
            {
                if (databaseManager.AddPathPoint(pointData)) added++;
            }
            scenePointNamesProcessed.Add(name);
        }
        Debug.Log($"Sync Results: Added={added}, Updated={updated}, Skipped={skipped}");

        int deleted = 0;
        foreach (PathPointData orphanPoint in dbPointsDict.Values)
        {
            if (databaseManager.DeletePathPoint(orphanPoint.ID)) deleted++;
        }
        if (deleted > 0) Debug.Log($"Sync: Deleted {deleted} orphaned points from DB.");

        Debug.Log("--- Scene->DB Sync Complete ---");
        LoadPointsFromDatabase();
    }

    public void DeleteAllPointsFromDatabase()
    {
        if (databaseManager == null) return;
        List<PathPointData> pointsToDelete = databaseManager.GetAllPathPoints() ?? new List<PathPointData>();
        int deleteCount = 0;
        foreach (var point in pointsToDelete)
        {
            if (databaseManager.DeletePathPoint(point.ID)) deleteCount++;
        }
        Debug.LogWarning($"--- Deleted {deleteCount} of {pointsToDelete.Count} points! ---");
        LoadPointsFromDatabase();
    }

    public async Task SyncEndPointsToFirebase()
    {
        if (databaseManager == null || firebaseManager == null) return;
        if (!firebaseManager.IsInitialized) await firebaseManager.InitializeFirebase();
        if (!firebaseManager.IsInitialized) { Debug.LogError("Firebase init FAILED. Aborting sync."); return; }
        
        List<PathPointData> localEndPoints = databaseManager.GetAllEndPoints();
        Debug.Log($"Found {localEndPoints.Count} EndPoints in SQLite to sync.");
        bool success = await firebaseManager.SyncEndPointsAsync(localEndPoints);
        Debug.Log(success ? "--- EndPoint Sync to Firebase SUCCEEDED. ---" : "--- EndPoint Sync to Firebase FAILED. ---");
    }
}