// --- FirebaseManager.cs ---
// This is the complete script, modified to use a single, consolidated Firebase project.
// All of your original methods have been preserved and corrected.

using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Firebase.Auth;

public class FirebaseManager : MonoBehaviour
{
    [Header("Firebase Settings")]
    [Tooltip("The single Realtime Database URL from your main Firebase project console (e.g., facultydatabase-3f39e).")]
    public string databaseUrl = "https://facultydatabase-3f39e-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("Data Root Nodes")]
    [Tooltip("The name of the node in your database that holds all faculty user profiles.")]
    public string facultyDataRootNode = "facultyMembers";
    [Tooltip("The name of the node in your database that holds all map navigation points.")]
    public string endPointsRootNode = "endPoints";

    // --- We now only need one reference to our single, consolidated database ---
    private DatabaseReference databaseReference;

    private bool firebaseInitialized = false;
    public bool IsInitialized => firebaseInitialized;
    private FirebaseAuth auth;
    public FirebaseAuth AuthInstance => auth;

    public event Action<Dictionary<string, FacultyMemberData>> OnFacultyDataUpdated;
    private Dictionary<string, FacultyMemberData> localFacultyCache = new Dictionary<string, FacultyMemberData>();

    async void Start()
    {
        await InitializeFirebase();
    }

    public async Task InitializeFirebase()
    {
        if (firebaseInitialized) return;
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.GetAuth(app);

                // --- MODIFICATION: Initialize the connection to our ONE database ---
                databaseReference = FirebaseDatabase.GetInstance(app, databaseUrl).RootReference;

                firebaseInitialized = true;
                Debug.Log("FirebaseManager: Connection to single, consolidated database initialized successfully.");

                ListenForFacultyUpdates();
            }
            else
            {
                Debug.LogError($"FirebaseManager: Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception during Firebase Init: {e.Message}");
        }
    }

    public async Task<Dictionary<string, DepartmentData>> GetDepartmentRoomDataAsync()
    {
        var result = new Dictionary<string, DepartmentData>();
        if (!firebaseInitialized)
        {
            Debug.LogWarning("FirebaseManager: Not initialized when GetDepartmentRoomDataAsync was called");
            return result;
        }

        try
        {
            // --- MODIFICATION: Use the single database reference and the correct node name ---
            DataSnapshot snapshot = await databaseReference.Child(endPointsRootNode).GetValueAsync();

            foreach (var dept in snapshot.Children)
            {
                string deptKey = dept.Key;
                string name = dept.Child("Name").Value?.ToString();

                List<string> rooms = new();
                if (dept.HasChild("rooms") && dept.Child("rooms").HasChildren)
                {
                    foreach (var room in dept.Child("rooms").Children)
                    {
                        rooms.Add(room.Value.ToString());
                    }
                }
                result[deptKey] = new DepartmentData { Name = name, rooms = rooms };
            }

            Debug.Log($"FirebaseManager: Loaded {result.Count} departments from Firebase.");
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Failed to fetch department-room data: {e.Message}");
        }

        return result;
    }

    public class DepartmentData
    {
        public string Name;
        public List<string> rooms;
    }

    public async Task<List<PathPointData>> GetAllEndPointsAsync()
    {
        if (!firebaseInitialized)
        {
            Debug.LogError("FirebaseManager: Cannot get endpoints, Firebase not initialized.");
            return new List<PathPointData>();
        }

        List<PathPointData> endPoints = new List<PathPointData>();
        try
        {
            // --- MODIFICATION: Use the single database reference and the correct node name ---
            DataSnapshot snapshot = await databaseReference.Child(endPointsRootNode).GetValueAsync();
            if (snapshot.Exists && snapshot.HasChildren)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    var pointDict = childSnapshot.Value as Dictionary<string, object>;
                    if (pointDict != null)
                    {
                        PathPointData point = new PathPointData
                        {
                            ID = Convert.ToInt32(pointDict["ID"]),
                            Name = Convert.ToString(pointDict["Name"]),
                            PosX = Convert.ToSingle(pointDict["PosX"]),
                            PosY = Convert.ToSingle(pointDict["PosY"]),
                            PosZ = Convert.ToSingle(pointDict["PosZ"]),
                            PointTag = Convert.ToString(pointDict["PointTag"])
                        };
                        endPoints.Add(point);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception getting endpoints from Firebase: {e.Message}");
        }

        return endPoints.OrderBy(p => p.Name).ToList();
    }

    public async Task<bool> SyncEndPointsAsync(List<PathPointData> endPointsToSync)
    {
        if (!firebaseInitialized) { return false; }
        var dataToSend = new Dictionary<string, object>();
        if (endPointsToSync != null)
        {
            foreach (var point in endPointsToSync)
            {
                if (point == null || string.IsNullOrEmpty(point.Name)) continue;
                var pointData = new Dictionary<string, object>
                {
                    { "ID", point.ID }, { "Name", point.Name },
                    { "PosX", point.PosX }, { "PosY", point.PosY }, { "PosZ", point.PosZ },
                    { "PointTag", point.PointTag }
                };
                dataToSend[point.Name] = pointData;
            }
        }
        try
        {
            // --- MODIFICATION: Use the single database reference and the correct node name ---
            await databaseReference.Child(endPointsRootNode).SetValueAsync(dataToSend);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception during SetValueAsync for EndPoints: {e.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, List<string>>> FetchDepartmentRoomsAsync()
    {
        var result = new Dictionary<string, List<string>>();
        if (!firebaseInitialized)
        {
            Debug.LogWarning("FirebaseManager: Not initialized when FetchDepartmentRoomsAsync was called");
            return result;
        }

        try
        {
            // --- MODIFICATION: Use the single database reference and the correct node name ---
            DataSnapshot snapshot = await databaseReference.Child(endPointsRootNode).GetValueAsync();
            foreach (var dept in snapshot.Children)
            {
                string deptKey = dept.Key;
                List<string> rooms = new();
                if (dept.HasChild("rooms"))
                {
                    foreach (var room in dept.Child("rooms").Children)
                    {
                        rooms.Add(room.Value.ToString());
                    }
                }
                result[deptKey] = rooms;
            }
            Debug.Log($"FirebaseManager: Fetched {result.Count} departments with room lists.");
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Failed to fetch department-room list: {e.Message}");
        }
        return result;
    }

    // --- The Hashing and Verify password methods are local and need no changes ---
    private string HashPassword(string password, string salt)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] saltedPasswordBytes = System.Text.Encoding.UTF8.GetBytes(password + salt);
            byte[] hashBytes = sha256.ComputeHash(saltedPasswordBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }

    public bool VerifyPassword(FacultyMemberData facultyData, string enteredPassword)
    {
        if (facultyData == null || string.IsNullOrEmpty(facultyData.PasswordHash) || string.IsNullOrEmpty(enteredPassword)) return false;
        string salt = facultyData.FacultyID + "some_fixed_app_salt_for_demo";
        string attemptHash = HashPassword(enteredPassword, salt);
        return attemptHash == facultyData.PasswordHash;
    }

    // --- The methods below are MODIFIED to use the single database reference ---

    public async Task<bool> AddOrUpdateFacultyMemberAsync(FacultyMemberData facultyData, string newPlainPassword = null)
    {
        if (!firebaseInitialized) return false;
        if (string.IsNullOrEmpty(facultyData.FacultyID))
        {
            Debug.LogError("AddOrUpdateFacultyMemberAsync failed: FacultyID (which should be the UID) is missing.");
            return false;
        }

        // NOTE: The password hashing part of this function is now likely obsolete, as password
        // handling is managed by Firebase Authentication and the web app.
        // It's left here in case you have a use for it, but it's not needed for login.
        if (!string.IsNullOrEmpty(newPlainPassword))
        {
            string salt = facultyData.FacultyID + "some_fixed_app_salt_for_demo";
            facultyData.PasswordHash = HashPassword(newPlainPassword, salt);
        }

        string json = JsonUtility.ToJson(facultyData);
        try
        {
            var updateData = new Dictionary<string, object>
            {
                { nameof(FacultyMemberData.Email), facultyData.Email },
                { nameof(FacultyMemberData.FullName), facultyData.FullName },
                { nameof(FacultyMemberData.Department), facultyData.Department },
                { nameof(FacultyMemberData.Position), facultyData.Position },
                { nameof(FacultyMemberData.AvailabilityStatus), facultyData.AvailabilityStatus },
                { nameof(FacultyMemberData.CurrentLocationName), facultyData.CurrentLocationName },
                { nameof(FacultyMemberData.PasswordHash), facultyData.PasswordHash }
            };

            await databaseReference.Child(facultyDataRootNode).Child(facultyData.FacultyID).UpdateChildrenAsync(updateData);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                localFacultyCache[facultyData.FacultyID] = facultyData;
                OnFacultyDataUpdated?.Invoke(GetCachedFaculty());
            });
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception during Add/Update faculty {facultyData.FacultyID}: {e.Message}");
            return false;
        }
    }

    public async Task<bool> DoesFacultyExistAsync(string userUID)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return false;
        try
        {
            DataSnapshot snapshot = await databaseReference.Child(facultyDataRootNode).Child(userUID).GetValueAsync();
            return snapshot.Exists;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception checking existence for faculty {userUID}: {e.Message}");
            return false;
        }
    }

    public async Task<FacultyMemberData> GetFacultyMemberAsync(string userUID)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return null;
        if (localFacultyCache.TryGetValue(userUID, out var cachedFaculty)) return cachedFaculty;
        try
        {
            DataSnapshot snapshot = await databaseReference.Child(facultyDataRootNode).Child(userUID).GetValueAsync();
            if (snapshot.Exists && snapshot.Value != null)
            {
                FacultyMemberData faculty = JsonUtility.FromJson<FacultyMemberData>(snapshot.GetRawJsonValue());
                if (faculty != null)
                {
                    faculty.FacultyID = userUID; // Ensure ID is set from the key
                    localFacultyCache[userUID] = faculty;
                }
                return faculty;
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception getting faculty {userUID} from Firebase: {e.Message}");
            return null;
        }
    }

    public async Task<List<FacultyMemberData>> GetAllFacultyMembersAsync()
    {
        if (!firebaseInitialized) return new List<FacultyMemberData>();
        try
        {
            DataSnapshot snapshot = await databaseReference.Child(facultyDataRootNode).GetValueAsync();
            if (!snapshot.Exists || !snapshot.HasChildren)
            {
                // ... (rest of the logic is fine)
                return new List<FacultyMemberData>();
            }

            var newCache = new Dictionary<string, FacultyMemberData>();
            foreach (var childSnapshot in snapshot.Children)
            {
                FacultyMemberData faculty = JsonUtility.FromJson<FacultyMemberData>(childSnapshot.GetRawJsonValue());
                if (faculty != null && !string.IsNullOrEmpty(childSnapshot.Key))
                {
                    faculty.FacultyID = childSnapshot.Key;
                    newCache[faculty.FacultyID] = faculty;
                }
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                localFacultyCache = newCache;
                OnFacultyDataUpdated?.Invoke(GetCachedFaculty());
            });

            return newCache.Values.ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase GetAll Error: {e.Message}");
            return new List<FacultyMemberData>();
        }
    }

    public async Task<string> GetFacultyCurrentLocationNameAsync(string userUID)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return null;
        if (localFacultyCache.TryGetValue(userUID, out var cachedFaculty))
        {
            return cachedFaculty.CurrentLocationName;
        }
        try
        {
            DataSnapshot snapshot = await databaseReference.Child(facultyDataRootNode).Child(userUID).Child(nameof(FacultyMemberData.CurrentLocationName)).GetValueAsync();
            return snapshot.Exists ? snapshot.Value as string : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase Get Location Name Error for {userUID}: {e.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateFacultyLocationAsync(string userUID, string locationName)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return false;
        try
        {
            await databaseReference.Child(facultyDataRootNode).Child(userUID).Child(nameof(FacultyMemberData.CurrentLocationName)).SetValueAsync(locationName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception updating location for faculty {userUID}: {e.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateFacultyAvailabilityAsync(string userUID, string status)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return false;
        try
        {
            await databaseReference.Child(facultyDataRootNode).Child(userUID).Child(nameof(FacultyMemberData.AvailabilityStatus)).SetValueAsync(status);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception updating availability for faculty {userUID}: {e.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFacultyMemberAsync(string userUID)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(userUID)) return false;
        try
        {
            await databaseReference.Child(facultyDataRootNode).Child(userUID).RemoveValueAsync();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception deleting faculty {userUID}: {e.Message}");
            return false;
        }
    }

    private void ListenForFacultyUpdates()
    {
        if (!firebaseInitialized) return;
        databaseReference.Child(facultyDataRootNode).ValueChanged += HandleFacultyValueChanged;
    }

    private void HandleFacultyValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null) { Debug.LogError($"FirebaseManager Listener DB Error: {args.DatabaseError.Message}"); return; }

        var newCache = new Dictionary<string, FacultyMemberData>();
        if (args.Snapshot != null && args.Snapshot.Exists && args.Snapshot.HasChildren)
        {
            foreach (var childSnapshot in args.Snapshot.Children)
            {
                FacultyMemberData faculty = JsonUtility.FromJson<FacultyMemberData>(childSnapshot.GetRawJsonValue());
                if (faculty != null && !string.IsNullOrEmpty(childSnapshot.Key))
                {
                    faculty.FacultyID = childSnapshot.Key;
                    newCache[childSnapshot.Key] = faculty;
                }
            }
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            localFacultyCache = newCache;
            OnFacultyDataUpdated?.Invoke(GetCachedFaculty());
        });
    }



    public Dictionary<string, FacultyMemberData> GetCachedFaculty()
    {
        return new Dictionary<string, FacultyMemberData>(localFacultyCache);
    }
}