using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Singleton MonoBehaviour for running coroutines needed by the ServiceLocator
    /// </summary>
    public class ServiceCoroutineRunner : MonoBehaviour
    {
        private static ServiceCoroutineRunner _instance;
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly object _queueLock = new object();
        
        /// <summary>
        /// Singleton instance with automatic creation if needed
        /// </summary>
        public static ServiceCoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindObjectOfType<ServiceCoroutineRunner>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var go = new GameObject("ServiceCoroutineRunner");
                        _instance = go.AddComponent<ServiceCoroutineRunner>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Another instance exists, destroy this one
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            // Process all queued actions
            ProcessMainThreadActions();
        }
        
        /// <summary>
        /// Queue an action to be executed on the main thread
        /// </summary>
        /// <param name="action">The action to execute</param>
        public void QueueOnMainThread(Action action)
        {
            if (action == null)
                return;
                
            lock (_queueLock)
            {
                _mainThreadActions.Enqueue(action);
            }
        }
        
        /// <summary>
        /// Process all queued main thread actions
        /// </summary>
        private void ProcessMainThreadActions()
        {
            // If no actions, early out
            if (_mainThreadActions.Count == 0)
                return;
                
            // Get all current actions to avoid infinite loops if new actions are added during execution
            Action[] actionsToRun;
            lock (_queueLock)
            {
                actionsToRun = new Action[_mainThreadActions.Count];
                for (int i = 0; i < actionsToRun.Length; i++)
                {
                    actionsToRun[i] = _mainThreadActions.Dequeue();
                }
            }
            
            // Execute all actions
            foreach (var action in actionsToRun)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing queued action: {ex}");
                }
            }
        }
        
        /// <summary>
        /// Run an action on the main thread. If already on the main thread, run immediately,
        /// otherwise queue for the next Update.
        /// 
        /// This is part of the public API that can be used by developers to ensure Unity operations
        /// happen on the main thread, preventing threading exceptions when manipulating Unity objects.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <example>
        /// // Example of safely modifying a GameObject from a background thread
        /// ServiceCoroutineRunner.RunOnMainThread(() => {
        ///     gameObject.SetActive(false);
        ///     // Other Unity operations...
        /// });
        /// </example>
        public static void RunOnMainThread(Action action)
        {
            if (action == null)
                return;
                
            // If we're already on the main thread, run immediately
            if (IsMainThread())
            {
                action();
            }
            else
            {
                // Queue to run on the main thread
                Instance.QueueOnMainThread(action);
            }
        }
        
        /// <summary>
        /// Release a service instance safely using a coroutine.
        /// This avoids deadlocks when releasing services that need to be destroyed on the main thread.
        /// </summary>
        /// <typeparam name="T">The service interface type</typeparam>
        /// <param name="name">The service name</param>
        /// <param name="instance">The service instance to release</param>
        /// <returns>A coroutine that performs the release operation</returns>
        internal static Coroutine ReleaseServiceSafely<T>(string name, object instance)
        {
            return Instance.StartCoroutine(Instance.ReleaseServiceInstanceCoroutine<T>(name, instance));
        }
        
        private IEnumerator ReleaseServiceInstanceCoroutine<T>(string name, object instance)
        {
            // Start the async operation
            var task = ServiceLocator.ReleaseServiceInstanceAsync<T>(name, instance);
            
            // Wait for it to complete
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Handle exceptions
            if (task.IsFaulted && task.Exception != null)
            {
                Debug.LogError($"Error releasing service instance: {task.Exception.InnerException?.Message ?? task.Exception.Message}");
            }
        }
        
        /// <summary>
        /// Check if the current thread is the main Unity thread.
        /// 
        /// This is part of the public API that developers can use to validate thread context
        /// before making Unity API calls that require the main thread. Use this to prevent
        /// "Access to Unity engine components from background thread" exceptions.
        /// </summary>
        /// <returns>True if the current thread is the main thread</returns>
        /// <example>
        /// // Example of safely checking before accessing Unity APIs
        /// if (ServiceCoroutineRunner.IsMainThread())
        /// {
        ///     // Safe to directly manipulate Unity objects
        ///     gameObject.SetActive(true);
        /// }
        /// else
        /// {
        ///     // Schedule work on main thread instead
        ///     ServiceCoroutineRunner.RunOnMainThread(() => gameObject.SetActive(true));
        /// }
        /// </example>
        public static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == 1;
        }
    }
}
