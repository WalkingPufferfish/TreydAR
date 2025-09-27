using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class FirebaseManager : MonoBehaviour
{
    [Header("Faculty Database Settings")]
    public string facultyDatabaseUrl = "https://facultydatabase-3f39e-default-rtdb.asia-southeast1.firebasedatabase.app/";
    public string facultyDataRootNode = "facultyMembers";

    [Header("EndPoints Database Settings")]
    public string endPointsDatabaseUrl = "https://endpoints-7f2ab-default-rtdb.asia-southeast1.firebasedatabase.app/";
    public string endPointsRootNode = "endPoints";

    private DatabaseReference facultyDbReference;
    private DatabaseReference endPointsDbReference;
    private bool firebaseInitialized = false;
    public bool IsInitialized => firebaseInitialized;

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
                facultyDbReference = FirebaseDatabase.GetInstance(app, facultyDatabaseUrl).RootReference;
                endPointsDbReference = FirebaseDatabase.GetInstance(app, endPointsDatabaseUrl).RootReference;
                firebaseInitialized = true;
                Debug.Log("FirebaseManager: Both database connections Initialized Successfully.");
                ListenForFacultyUpdates();
            }
            else { Debug.LogError($"FirebaseManager: Could not resolve all Firebase dependencies: {dependencyStatus}"); }
        }
        catch (Exception e) { Debug.LogError($"FirebaseManager: Exception during Firebase Init: {e.Message}"); }
    }

    // <<< --- NEW METHOD TO GET ENDPOINTS FROM FIREBASE --- >>>
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
            // Go to the secondary database, to the "endPoints" node, and get all the data.
            DataSnapshot snapshot = await endPointsDbReference.Child(endPointsRootNode).GetValueAsync();
            if (snapshot.Exists && snapshot.HasChildren)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    // For each child, convert the JSON back into our PathPointData object.
                    // This uses a dictionary to be safe with the data types.
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

        // Return the list, sorted alphabetically for the dropdown.
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
            await endPointsDbReference.Child(endPointsRootNode).SetValueAsync(dataToSend);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception during SetValueAsync to secondary DB: {e.Message}");
            return false;
        }
    }

    // --- ALL FACULTY MANAGEMENT METHODS (No changes needed below this line) ---
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

    public async Task<bool> AddOrUpdateFacultyMemberAsync(FacultyMemberData facultyData, string newPlainPassword = null)
    {
        if (!firebaseInitialized) return false;
        if (!string.IsNullOrEmpty(newPlainPassword))
        {
            string salt = facultyData.FacultyID + "some_fixed_app_salt_for_demo";
            facultyData.PasswordHash = HashPassword(newPlainPassword, salt);
        }
        else if (string.IsNullOrEmpty(facultyData.PasswordHash)) return false;

        string json = JsonUtility.ToJson(facultyData);
        try
        {
            await facultyDbReference.Child(facultyDataRootNode).Child(facultyData.FacultyID).SetRawJsonValueAsync(json);
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

    public async Task<bool> DoesFacultyExistAsync(string facultyId)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return false;
        try
        {
            DataSnapshot snapshot = await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).GetValueAsync();
            return snapshot.Exists;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception checking existence for faculty {facultyId}: {e.Message}");
            return false;
        }
    }

    public async Task<FacultyMemberData> GetFacultyMemberAsync(string facultyId)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return null;
        if (localFacultyCache.TryGetValue(facultyId, out var cachedFaculty)) return cachedFaculty;
        try
        {
            DataSnapshot snapshot = await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).GetValueAsync();
            if (snapshot.Exists && snapshot.Value != null)
            {
                FacultyMemberData faculty = JsonUtility.FromJson<FacultyMemberData>(snapshot.GetRawJsonValue());
                if (faculty != null) localFacultyCache[facultyId] = faculty;
                return faculty;
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception getting faculty {facultyId} from Firebase: {e.Message}");
            return null;
        }
    }

    public async Task<List<FacultyMemberData>> GetAllFacultyMembersAsync()
    {
        if (!firebaseInitialized) return new List<FacultyMemberData>();
        try
        {
            DataSnapshot snapshot = await facultyDbReference.Child(facultyDataRootNode).GetValueAsync();
            if (!snapshot.Exists || !snapshot.HasChildren)
            {
                if (localFacultyCache.Count > 0)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        localFacultyCache.Clear();
                        OnFacultyDataUpdated?.Invoke(GetCachedFaculty());
                    });
                }
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

    public async Task<string> GetFacultyCurrentLocationNameAsync(string facultyId)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return null;
        if (localFacultyCache.TryGetValue(facultyId, out var cachedFaculty))
        {
            return cachedFaculty.CurrentLocationName;
        }
        try
        {
            DataSnapshot snapshot = await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).Child(nameof(FacultyMemberData.CurrentLocationName)).GetValueAsync();
            return snapshot.Exists ? snapshot.Value as string : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase Get Location Name Error for {facultyId}: {e.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateFacultyLocationAsync(string facultyId, string locationName)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return false;
        try
        {
            await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).Child(nameof(FacultyMemberData.CurrentLocationName)).SetValueAsync(locationName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception updating location for faculty {facultyId}: {e.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateFacultyAvailabilityAsync(string facultyId, string status)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return false;
        try
        {
            await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).Child(nameof(FacultyMemberData.AvailabilityStatus)).SetValueAsync(status);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception updating availability for faculty {facultyId}: {e.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFacultyMemberAsync(string facultyId)
    {
        if (!firebaseInitialized || string.IsNullOrEmpty(facultyId)) return false;
        try
        {
            await facultyDbReference.Child(facultyDataRootNode).Child(facultyId).RemoveValueAsync();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"FirebaseManager: Exception deleting faculty {facultyId}: {e.Message}");
            return false;
        }
    }

    private void ListenForFacultyUpdates()
    {
        if (!firebaseInitialized) return;
        facultyDbReference.Child(facultyDataRootNode).ValueChanged += HandleFacultyValueChanged;
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