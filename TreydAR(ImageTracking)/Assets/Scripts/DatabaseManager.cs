using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using SQLite;    

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Networking;
using System.Collections; // Required for Coroutine if used
#endif

// --- Data Structure Definition ---
// Keep PathPointData definition here as it's used by SQLite for this table.
[Table("PathPoints")]
public class PathPointData // Ensure this definition is consistent
{
    [PrimaryKey, AutoIncrement] public int ID { get; set; }
    [NotNull, Unique] public string Name { get; set; } // Must be unique for GetPathPointByName
    [NotNull] public float PosX { get; set; }
    [NotNull] public float PosY { get; set; }
    [NotNull] public float PosZ { get; set; }
    public string PointTag { get; set; } = "Untagged"; // e.g., "StartPoint", "EndPoint", "Waypoint"

    // Ignored properties are not stored in the DB but provide convenience access
    [Ignore] public Vector3 Position { get => new Vector3(PosX, PosY, PosZ); set { PosX = value.x; PosY = value.y; PosZ = value.z; } }
    [Ignore] public bool IsEndPoint => PointTag == "EndPoint"; // Example helper property
}


/// <summary>
/// Manages the local SQLite database connection and operations for PathPointData.
/// Handles platform-specific database file copying.
/// Faculty data management has been removed and moved to FirebaseManager.
/// </summary>
public class DatabaseManager : MonoBehaviour
{
    [Tooltip("The name of the database file (including extension) located in the StreamingAssets folder.")]
    public string databaseFileName = "PathfindingPoints.db";

    private string dbPath; // Runtime path to the database file
    private SQLiteConnection connection; // The active SQLite connection

    #region Connection Management
    // Ensure connection is ready when the object wakes up
    void Awake() { EnsureConnectionInitialized(); }
    // Close connection when the object is destroyed
    void OnDestroy() { CloseConnection(); }

    /// <summary>
    /// Ensures the SQLite connection is initialized. If not, calls InitializeDatabase.
    /// </summary>
    private void EnsureConnectionInitialized()
    {
        // If connection already exists, do nothing.
        if (connection != null) return;

        // Initialize the database connection.
        InitializeDatabase();

        // Log an error if initialization failed.
        if (connection == null) Debug.LogError("DatabaseManager: SQLite DB Initialization failed!");
    }

    /// <summary>
    /// Initializes the SQLite database connection. Handles copying the DB
    /// from StreamingAssets to a writable location on mobile platforms.
    /// Creates the PathPoints table if it doesn't exist.
    /// </summary>
    private void InitializeDatabase()
    {
        // Prevent double initialization.
        if (connection != null) return;

        // Path to the database in the read-only StreamingAssets folder.
        string streamingAssetsDbPath = Path.Combine(Application.streamingAssetsPath, databaseFileName);
        // Default runtime path is the StreamingAssets path (works for Editor/Standalone).
        dbPath = streamingAssetsDbPath;

        // --- Platform-specific DB path handling (Copy to writable location) ---
#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, copy DB to persistentDataPath if it doesn't exist there.
        string persistentDataPath = Path.Combine(Application.persistentDataPath, databaseFileName);
        if (!File.Exists(persistentDataPath))
        {
            Debug.Log($"Android: Database not found at '{persistentDataPath}'. Copying from StreamingAssets...");
            // Use UnityWebRequest to access StreamingAssets on Android.
            using (UnityWebRequest www = UnityWebRequest.Get(streamingAssetsDbPath))
            {
                var asyncOp = www.SendWebRequest();

                // --- WARNING: Blocking wait ---
                // This simple loop blocks the main thread. OK for initialization,
                // but for large DBs or smoother loading, use a Coroutine or async/await.
                while (!asyncOp.isDone) { System.Threading.Thread.Sleep(10); } // Small sleep to yield slightly
                // --- /Blocking wait ---

                if (www.result == UnityWebRequest.Result.Success)
                {
                    // Write the downloaded bytes to the persistent data path.
                    File.WriteAllBytes(persistentDataPath, www.downloadHandler.data);
                    Debug.Log($"Android: DB copy success to '{persistentDataPath}'.");
                }
                else
                {
                    // Log error if download/copy failed. Initialization will likely fail after this.
                    Debug.LogError($"Android DB copy FAILED! Error: {www.error}. Path: {streamingAssetsDbPath}");
                    return; // Stop initialization if copy fails.
                }
            }
        }
        else
        {
            // DB already exists in persistentDataPath, use it.
            // Debug.Log($"Android: Using existing DB at '{persistentDataPath}'.");
        }
        // Set the runtime path to the writable location on Android.
        dbPath = persistentDataPath;

#elif UNITY_IOS && !UNITY_EDITOR
        // On iOS, Application.dataPath points to the app bundle. /Raw/ is needed for loose files.
        // Copy DB from Data/Raw to persistentDataPath if it doesn't exist there.
        string sourcePath = Path.Combine(Application.dataPath, "Raw", databaseFileName); // Path inside the app bundle
        string persistentDataPath = Path.Combine(Application.persistentDataPath, databaseFileName); // Writable path

        if (!File.Exists(persistentDataPath))
        {
            Debug.Log($"iOS: Database not found at '{persistentDataPath}'. Copying from '{sourcePath}'...");
            try
            {
                // Direct file copy works for files included in 'Raw' folder on iOS build.
                File.Copy(sourcePath, persistentDataPath);
                Debug.Log($"iOS: DB copy success to '{persistentDataPath}'.");
            }
            catch (Exception e)
            {
                // Log error if copy fails (e.g., source file missing, permissions issue).
                Debug.LogError($"iOS DB copy FAILED! Error: {e.Message}");
                return; // Stop initialization if copy fails.
            }
        }
        else
        {
            // DB already exists in persistentDataPath, use it.
            // Debug.Log($"iOS: Using existing DB at '{persistentDataPath}'.");
        }
        // Set the runtime path to the writable location on iOS.
        dbPath = persistentDataPath;
#endif
        // --- End Platform Handling ---

        // Log the final path being used for the database connection.
        Debug.Log($"DatabaseManager: Using SQLite Database At Path: {dbPath}");
        try
        {
            // Create the SQLite connection object with appropriate flags.
            // ReadWrite: Allows reading and writing.
            // Create: Creates the DB file if it doesn't exist (shouldn't happen here due to copy).
            // FullMutex: Ensures thread safety if accessed from multiple threads (safer default).
            connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            // Ensure the PathPoints table exists, creating it if necessary based on the PathPointData class definition.
            connection.CreateTable<PathPointData>();

            // Faculty table creation is removed.

            Debug.Log("DatabaseManager: SQLite connection established and PathPoints table ensured.");
        }
        catch (Exception e)
        {
            // Log any errors during connection opening or table creation.
            Debug.LogError($"DatabaseManager: Failed to open/create SQLite DB connection: {e.Message}");
            connection = null; // Ensure connection is null if setup failed.
        }
    }

    /// <summary>
    /// Closes the SQLite database connection if it is open.
    /// </summary>
    private void CloseConnection()
    {
        if (connection != null)
        {
            try
            {
                connection.Close();
                Debug.Log("DatabaseManager: SQLite connection closed.");
            }
            catch (Exception e)
            {
                // Log errors during closing, though usually not critical.
                Debug.LogError($"DatabaseManager: Error closing SQLite DB connection: {e.Message}");
            }
            finally
            {
                // Ensure the connection variable is set to null after closing.
                connection = null;
            }
        }
    }
    #endregion

    #region Path Point Data Access Methods (SQLite)

    /// <summary>
    /// Retrieves ALL path points from the PathPoints table in the SQLite database.
    /// Ordered alphabetically by name.
    /// </summary>
    /// <returns>A list of PathPointData objects, or an empty list on error or if table is empty.</returns>
    public List<PathPointData> GetAllPathPoints()
    {
        EnsureConnectionInitialized(); // Ensure connection is ready.
        if (connection == null) return new List<PathPointData>(); // Return empty list if connection failed.

        try
        {
            // Query the table, order by Name, and convert to a List.
            return connection.Table<PathPointData>().OrderBy(p => p.Name).ToList();
        }
        catch (Exception e)
        {
            // Log errors during the query.
            Debug.LogError($"DatabaseManager: GetAllPathPoints failed: {e.Message}");
            return new List<PathPointData>(); // Return empty list on error.
        }
    }

    /// <summary>
    /// Retrieves only path points tagged as "EndPoint" from the SQLite database.
    /// Used by NavigationManager & FacultyPortalManager (for location dropdown).
    /// Ordered alphabetically by name.
    /// </summary>
    /// <returns>A list of EndPoint PathPointData objects, or empty list on error.</returns>
    public List<PathPointData> GetAllEndPoints()
    {
        EnsureConnectionInitialized();
        if (connection == null) return new List<PathPointData>();

        try
        {
            // Query the table, filter by PointTag, order by Name, and convert to List.
            return connection.Table<PathPointData>().Where(p => p.PointTag == "EndPoint").OrderBy(p => p.Name).ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: GetAllEndPoints failed: {e.Message}");
            return new List<PathPointData>();
        }
    }

    /// <summary>
    /// Retrieves a single path point from the SQLite database by its unique Name.
    /// Used by Testing.cs (Editor Script) & FacultyFinder.cs (to get point from location name).
    /// </summary>
    /// <param name="name">The unique name of the path point to find.</param>
    /// <returns>The PathPointData object if found, otherwise null.</returns>
    public PathPointData GetPathPointByName(string name)
    {
        EnsureConnectionInitialized();
        // Check for invalid input or connection issues.
        if (connection == null || string.IsNullOrEmpty(name)) return null;

        try
        {
            // Find the first entry where the Name matches (should be unique).
            return connection.Table<PathPointData>().FirstOrDefault(p => p.Name == name);
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: GetPathPointByName '{name}' failed: {e.Message}");
            return null; // Return null on error.
        }
    }

    /// <summary>
    /// Adds a new path point record to the SQLite database.
    /// Assumes the Name is unique (will fail if constraint violated).
    /// </summary>
    /// <param name="point">The PathPointData object to add.</param>
    /// <returns>True if insertion was successful (1 row affected), false otherwise.</returns>
    public bool AddPathPoint(PathPointData point)
    {
        EnsureConnectionInitialized();
        if (connection == null || point == null) return false;

        try
        {
            // Insert the object into the table. AutoIncrement ID is handled by SQLite.
            int rowsAffected = connection.Insert(point);
            // Log success if a row was inserted.
            if (rowsAffected > 0) Debug.Log($"DatabaseManager: Added point '{point.Name}' to SQLite.");
            else Debug.LogWarning($"DatabaseManager: AddPathPoint for '{point.Name}' affected 0 rows.");
            return rowsAffected > 0;
        }
        catch (Exception e) // Catch potential unique constraint violations or other errors.
        {
            Debug.LogError($"DatabaseManager: AddPathPoint '{point?.Name}' failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates an existing path point record in the SQLite database.
    /// Matches the record based on the point's Primary Key (ID).
    /// </summary>
    /// <param name="point">The PathPointData object with updated values (must include the correct ID).</param>
    /// <returns>True if update was successful (1 row affected), false otherwise.</returns>
    public bool UpdatePathPoint(PathPointData point)
    {
        EnsureConnectionInitialized();
        if (connection == null || point == null) return false;

        try
        {
            // Update the record based on the PrimaryKey (ID) field in the 'point' object.
            int rowsAffected = connection.Update(point);
            if (rowsAffected > 0)
            {
                // Debug.Log($"DatabaseManager: Updated point '{point.Name}' (ID: {point.ID}) in SQLite.");
            }
            else
            {
                // This usually means no record with the given ID was found.
                Debug.LogWarning($"DatabaseManager: UpdatePathPoint failed for '{point.Name}' (ID: {point.ID}). Point not found or data unchanged.");
            }
            return rowsAffected > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: UpdatePathPoint '{point?.Name}' failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a path point record from the SQLite database using its unique ID.
    /// </summary>
    /// <param name="id">The ID (PrimaryKey) of the path point to delete.</param>
    /// <returns>True if deletion was successful (1 row affected), false otherwise.</returns>
    public bool DeletePathPoint(int id)
    {
        EnsureConnectionInitialized();
        if (connection == null) return false;

        try
        {
            // Delete the record matching the primary key 'id'.
            int rowsAffected = connection.Delete<PathPointData>(id);
            if (rowsAffected > 0)
            {
                Debug.Log($"DatabaseManager: Deleted point with ID: {id} from SQLite.");
            }
            else
            {
                // This means no record with the given ID was found.
                Debug.LogWarning($"DatabaseManager: DeletePathPoint failed for ID {id}. Point not found.");
            }
            return rowsAffected > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"DatabaseManager: DeletePathPoint ID '{id}' failed: {e.Message}");
            return false;
        }
    }
    #endregion
}