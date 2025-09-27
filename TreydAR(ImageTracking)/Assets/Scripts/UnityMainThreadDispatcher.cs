using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A utility class to dispatch actions to be executed on the Unity main thread.
/// Create an instance of this dispatcher (e.g., attach to a persistent GameObject)
/// early in your application's lifecycle.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            // Try to find an existing instance in the scene
            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (_instance == null)
            {
                // Create a new GameObject and add the dispatcher component
                GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
                _instance = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(dispatcherObject); // Make it persistent
                Debug.Log("UnityMainThreadDispatcher instance created.");
            }
        }
        return _instance;
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            // If another instance already exists, destroy this one
            Debug.LogWarning("Another instance of UnityMainThreadDispatcher found. Destroying this one.");
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Enqueues an IEnumerator to be started as a Coroutine on the main thread.
    /// </summary>
    /// <param name="coroutine">The IEnumerator to start.</param>
    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(() => {
                StartCoroutine(coroutine);
            });
        }
    }
}