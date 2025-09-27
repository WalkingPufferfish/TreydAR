using System;
using System.Threading.Tasks;
using UnityEditor;

public static class EditorCoroutineHelper
{
    public static void RunTask(Func<Task> taskFactory)
    {
        var task = taskFactory();
        EditorApplication.update += () => Progress(task);
    }

    private static void Progress(Task task)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                UnityEngine.Debug.LogException(task.Exception);
            }
            EditorApplication.update -= () => Progress(task);
        }
    }
}