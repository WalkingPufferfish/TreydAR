// Assets/Scripts/Data/FacultyMemberData.cs

using System;

/// <summary>
/// Data structure representing a faculty member's profile information for Firebase Realtime Database.
/// </summary>
[System.Serializable]
public class FacultyMemberData
{
    public string FacultyID; // Used as the key in RTDB
    public string FullName;
    public string Department;
    public string CurrentLocationName;
    public string AvailabilityStatus;
    public string Position;
    public string PasswordHash; 

    public FacultyMemberData() { }
}