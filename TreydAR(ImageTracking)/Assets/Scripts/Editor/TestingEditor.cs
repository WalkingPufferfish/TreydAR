using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(Testing))]
public class TestingEditor : Editor
{
    private string[] startPointNames = new string[0];
    private string[] endPointNames = new string[0];

    public override void OnInspectorGUI()
    {
        Testing testingScript = (Testing)target;
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Database Point Management", EditorStyles.boldLabel);

        if (GUILayout.Button("Reload Points from Database"))
        {
            testingScript.LoadPointsFromDatabase();
            LoadAndFilterPointNames(testingScript.AvailablePoints);
            Repaint();
        }

        LoadAndFilterPointNames(testingScript.AvailablePoints);

        // Dropdown code
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Editor Start Point Ref", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        int currentStartIndex = Mathf.Clamp(testingScript.selectedStartPointIndex, 0, Mathf.Max(0, startPointNames.Length - 1));
        int newStartIndex = EditorGUILayout.Popup(currentStartIndex, startPointNames);
        if (newStartIndex != testingScript.selectedStartPointIndex) { SetIndex(testingScript, true, newStartIndex); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Editor End Point Ref", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
        int currentEndIndex = Mathf.Clamp(testingScript.selectedEndPointIndex, 0, Mathf.Max(0, endPointNames.Length - 1));
        int newEndIndex = EditorGUILayout.Popup(currentEndIndex, endPointNames);
        if (newEndIndex != testingScript.selectedEndPointIndex) { SetIndex(testingScript, false, newEndIndex); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Database Operations", EditorStyles.boldLabel);

        if (GUILayout.Button("Sync ScenePathPoints -> Database"))
        {
            testingScript.SyncScenePointsToDatabase();
            Repaint();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Sync EndPoints (SQLite -> Firebase)"))
        {
            if (EditorUtility.DisplayDialog(
                "Confirm Firebase Sync",
                "This will replace ALL endpoints in Firebase with the EndPoints from the local SQLite database. Are you sure?",
                "Yes, Sync to Firebase",
                "Cancel"))
            {
                EditorCoroutineHelper.RunTask(async () =>
                {
                    await testingScript.SyncEndPointsToFirebase();
                    EditorUtility.DisplayDialog("Sync Complete", "The EndPoint sync to Firebase has finished. Check the console for details.", "OK");
                });
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("DELETE ALL Points from Database"))
        {
            if (EditorUtility.DisplayDialog("Confirm Deletion", "Are you sure you want to delete ALL points?", "Yes, Delete All", "Cancel"))
            {
                testingScript.DeleteAllPointsFromDatabase();
                Repaint();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void SetIndex(Testing script, bool isStart, int index)
    {
        Undo.RecordObject(script, $"Change Editor {(isStart ? "Start" : "End")} Point Index");
        if (isStart) script.selectedStartPointIndex = index;
        else script.selectedEndPointIndex = index;
        EditorUtility.SetDirty(script);
    }

    private void LoadAndFilterPointNames(IReadOnlyList<PathPointData> allPoints)
    {
        if (allPoints != null)
        {
            startPointNames = allPoints.Where(p => p != null && p.PointTag == "StartPoint").OrderBy(p => p.Name).Select(p => p.Name).ToArray();
            endPointNames = allPoints.Where(p => p != null && p.PointTag == "EndPoint").OrderBy(p => p.Name).Select(p => p.Name).ToArray();
        }
    }
}