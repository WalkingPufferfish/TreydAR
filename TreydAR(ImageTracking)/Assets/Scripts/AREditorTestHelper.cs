// AREditorTestHelper.cs
using UnityEngine;

// This component will only exist and run in the Unity Editor. It will be stripped from real builds.
public class AREditorTestHelper : MonoBehaviour
{
    [Tooltip("Drag the GameObject that has the NavigationManager script on it here.")]
    public NavigationManager navigationManager;

    void Update()
    {
        // This #if directive ensures this code ONLY runs inside the Unity Editor.
#if UNITY_EDITOR
        // We only want to run this code when the application is actually playing.
        if (!Application.isPlaying || navigationManager == null) return;

        // --- F1 Key: Test the first image target ---
        if (Input.GetKeyDown(KeyCode.F1) && !navigationManager.IsInitialized())
        {
            // IMPORTANT: The string here must EXACTLY match the 'imageName' in your database.
            navigationManager.Test_InitializeEnvironment("AnchorEntrance");
        }

        // --- F2 Key: Test the second image target ---
        if (Input.GetKeyDown(KeyCode.F2) && !navigationManager.IsInitialized())
        {
            navigationManager.Test_InitializeEnvironment("AnchorMedina");
        }

        // --- F3 Key: Test the third image target ---
        if (Input.GetKeyDown(KeyCode.F3) && !navigationManager.IsInitialized())
        {
            navigationManager.Test_InitializeEnvironment("AnchorLibrary");
        }
        
        // --- R Key: Reset the simulation ---
        if (Input.GetKeyDown(KeyCode.R) && navigationManager.IsInitialized())
        {
            Debug.Log("--- EDITOR TEST: Resetting State ---");
            navigationManager.ResetToInitialState();
        }
#endif
    }
}