using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Tracking;
using GAOS.ServiceLocator.Optional;
using System.Reflection;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Collections.Concurrent;

namespace GAOS.ServiceLocator
{
    internal class ServiceRegistration
    {
        // We use ServiceCoroutineRunner.RunOnMainThread for Unity operations
        // instead of SynchronizationContext to avoid potential deadlocks

        private readonly ServiceTypeInfo _typeInfo;
        private readonly object _lock = new object();
        private int _instanceCounter;

        // Track instances for both Singleton and SceneSingleton
        // For Singleton, the key will be a constant string
        private const string SINGLETON_KEY = "__singleton__";
        private const string POOLED_KEY = "__pooled__";
        private readonly ConcurrentDictionary<string, object> _instances = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<IServiceDisposable, byte> _disposingInProgress = new ConcurrentDictionary<IServiceDisposable, byte>();
        private readonly ConcurrentDictionary<object, byte> _pooledInstances = new ConcurrentDictionary<object, byte>();
        private Transform _servicePoolParent;

        public Type InterfaceType => _typeInfo.InterfaceType;
        public Type ImplementationType => _typeInfo.ImplementationType;
        public ServiceLifetime Lifetime { get; }
        public ServiceContext Context { get; }
        public ServiceType ServiceType => _typeInfo.ServiceType;
        public bool IsMonoBehaviour => ServiceType == ServiceType.MonoBehaviour;
        public bool IsScriptableObject => ServiceType == ServiceType.ScriptableObject;
        public string Name { get; }
        public object Instance => Lifetime == ServiceLifetime.Singleton ? GetInstanceFromDictionary(SINGLETON_KEY) : null;

        public bool IsInitialized
        {
            get
            {
                if (Lifetime == ServiceLifetime.Transient)
                    return true;

                if (Lifetime == ServiceLifetime.SceneSingleton)
                    return true; // Scene instances are managed separately

                var instance = GetInstanceFromDictionary(SINGLETON_KEY);
                if (instance == null)
                    return false;

                if (IsMonoBehaviour)
                    return ServiceValidator.ValidateMonoBehaviourInstance(instance as MonoBehaviour);

                return true;
            }
        }

        public bool HasError
        {
            get
            {
                try
                {
                    if (!IsInitialized)
                        return false;

                    if (Lifetime == ServiceLifetime.SceneSingleton)
                        return false; // Scene instances are managed separately

                    var instance = GetInstanceFromDictionary(SINGLETON_KEY);
                    return instance == null || 
                           (instance is IServiceDisposable disposable && disposable.IsDisposed);
                }
                catch
                {
                    return true;
                }
            }
        }

        public ServiceRegistration(ServiceTypeInfo typeInfo, ServiceLifetime? lifetime = null, ServiceContext? context = null, string name = null)
        {
            _typeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
            Lifetime = lifetime ?? typeInfo.DefaultLifetime;
            Context = context ?? typeInfo.DefaultContext;
            Name = name ?? typeInfo.DefaultName;

            ServiceValidator.ValidateServiceName(Name);

            // Initialize pool if the service is IServicePoolable
            InitializePool();
        }

        private void InitializePool()
        {
            // Check if this service type implements IServicePoolable
            if (!typeof(IServicePoolable).IsAssignableFrom(ImplementationType))
                return;

            try
            {
                // Create the parent GameObject for this service's pooled instances if it doesn't exist
                if (_servicePoolParent == null)
                {
                    // Schedule GameObject creation on the main thread
                    ServiceCoroutineRunner.RunOnMainThread(() =>
                    {
                        var poolParentName = $"{InterfaceType.Name}_{Name}";
                        var poolParentGO = new GameObject(poolParentName);
                        _servicePoolParent = poolParentGO.transform;
                        _servicePoolParent.SetParent(ServiceLocator.ServicePoolTransform);
                        
                        // Continue with pool initialization now that the parent exists
                        InitializePoolInstances();
                    });
                }
                else
                {
                    // Parent already exists, initialize pool instances directly
                    InitializePoolInstances();
                }
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Failed to initialize pool for {ImplementationType.Name}: {ex.Message}");
            }
        }
        
        private void InitializePoolInstances()
        {
            try
            {
                // Create first instance to check pool settings
                var tempInstance = this.CreateInstance(SceneManager.GetActiveScene(), POOLED_KEY);

                if (tempInstance is IServicePoolable poolable)
                {
                    int initialPoolSize = poolable.InitialPoolSize;
                    ExpendPool(initialPoolSize, poolable);
                }
                else
                {
                    ServiceDiagnostics.LogError($"Service {ImplementationType.Name} implements IServicePoolable but does not implement IServicePoolable interface");
                }
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Failed to initialize pool instances for {ImplementationType.Name}: {ex.Message}");
            }
        }

        private void ExpendPool(int expendPoolSize, IServicePoolable firstInstance)
        {
            // Check if this service type implements IServicePoolable
            if (!typeof(IServicePoolable).IsAssignableFrom(ImplementationType))
                return;
                
            try
            {
                int expandSize = expendPoolSize;
                
                if(firstInstance != null){
                    _pooledInstances.TryAdd(firstInstance, 0);
                    expandSize -= 1;
                }

                // Create initial pool instances
                for (int i = 0; i < expandSize; i++)
                {
                    var pooledInstance = this.CreateInstance(SceneManager.GetActiveScene(), POOLED_KEY);
                    _pooledInstances.TryAdd(pooledInstance, 0);
                }

                // Schedule GameObject operations on the main thread
                ServiceCoroutineRunner.RunOnMainThread(() => {
                    foreach (var instance in _pooledInstances.Keys)
                    {
                        if(instance is MonoBehaviour mb)
                        {
                            mb.gameObject.SetActive(false);
                            mb.transform.SetParent(_servicePoolParent);
                        }
                    }
                });
                    
                ServiceDiagnostics.LogInfo($"Created pool for {ImplementationType.Name} with {expandSize} instances");
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Failed to expend pool for {ImplementationType.Name}: {ex.Message}");
            }
        }
        
       
        
        private bool TryGetPooledInstance(out object instance, Scene requestingScene)
        {
            instance = null;
            
            // Check if this service type implements IServicePoolable
            if (!typeof(IServicePoolable).IsAssignableFrom(ImplementationType))
                return false;
            
            if (_pooledInstances.Count == 0)
            {
                var poolable = this.CreateInstance(requestingScene, POOLED_KEY) as IServicePoolable;
                ExpendPool(poolable.PoolExpansionSize, poolable);
            }
            
            // Try to get a pooled instance
            if (_pooledInstances.Count > 0)
            {
                // Get the first key-value pair from the pooled instances
                var kvPair = _pooledInstances.FirstOrDefault();
                
                // The key in the KeyValuePair is the instance
                instance = kvPair.Key;
                
                if (instance != null && _pooledInstances.TryRemove(instance, out _))
                {
                    // Call OnTakeFromPool if the instance implements IServicePoolable
                    if (instance is IServicePoolable poolable)
                    {
                        poolable.OnTakeFromPool();
                    }
                    
                    // Reset disposal state if the instance implements IServiceDisposable
                    if (instance is IServiceDisposable disposable)
                    {
                        disposable.ResetDisposalState(out bool isReset);
                        if (!isReset)
                        {
                            ServiceDiagnostics.LogWarning($"Failed to reset disposal state for service {ImplementationType.Name}");
                        }
                        else
                        {
                            ServiceDiagnostics.LogInfo($"Reset disposal state for service {ImplementationType.Name}");
                        }
                    }
                    
                    // Set the scene association for scene-specific services
                    if ((Lifetime == ServiceLifetime.SceneSingleton || Lifetime == ServiceLifetime.SceneTransient) 
                        && requestingScene.IsValid())
                    {
                        // Track the new scene association for this service
                        ServiceDiagnostics.LogInfo($"Associating pooled {Lifetime} service {ImplementationType.Name} with scene {requestingScene.name}");
                    }
                    
                    // Activate the GameObject if it's a MonoBehaviour
                    if (instance is MonoBehaviour mono)
                    {
                        mono.transform.SetParent(null);
                        GameObject.DontDestroyOnLoad(mono.gameObject);
                        mono.gameObject.name = GetInstanceKey(requestingScene, false);
                        mono.gameObject.SetActive(true);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        private object GetInstanceFromDictionary(string key)
        {
            // ConcurrentDictionary.TryGetValue is thread-safe
            return _instances.TryGetValue(key, out var instance) ? instance : null;
        }

        private void SetInstanceInDictionary(string key, object instance, Scene scene)
        {
            lock (_lock)
            {
                // Check if the instance is being disposed
                if (instance is IServiceDisposable disposableService && _disposingInProgress.ContainsKey(disposableService))
                {
                    ServiceDiagnostics.LogWarning(
                        $"Trying to set instance that is being disposed for service: {ImplementationType.Name} with key {key}");
                    return;
                }

                // Store in instance dictionary
                _instances.AddOrUpdate(key, instance, (_, __) => instance);
                IncrementInstanceCounter();
                ServiceDiagnostics.LogInfo($"Activated instance for {ImplementationType.Name} with key {key} in scene {scene.name}");

            
            }
        }

        private bool TryGetInstanceFromDictionary(string key, out object instance)
        {
            lock (_lock)
            {
                if (_instances.TryGetValue(key, out instance))
                {
                    // If the instance is being disposed, don't return it
                    if (instance is IServiceDisposable disposable && 
                        (_disposingInProgress.ContainsKey(disposable) || disposable.IsDisposed))
                    {
                        instance = null;
                        return false;
                    }
                    return true;
                }
                instance = null;
                return false;
            }
        }
       private async Task DestroyUnityObjectAsync(object instance)
        {
            // Create a TaskCompletionSource to wait for Unity operations
            var tcs = new TaskCompletionSource<bool>();
            
            // Use ServiceCoroutineRunner to ensure we're on the main thread
            // This avoids deadlocks by using the queuing mechanism of ServiceCoroutineRunner
            ServiceCoroutineRunner.RunOnMainThread(() => {
                try
                {
                    Debug.Log("Destroying instance on main thread");
                    // Unity operations run on main thread
                    if (ServiceType == ServiceType.MonoBehaviour)
                    {
                        var mb = instance as MonoBehaviour;
                        if (mb != null && mb.gameObject != null)
                        {
                            // Use DestroyImmediate for immediate cleanup
                            UnityEngine.Object.DestroyImmediate(mb.gameObject);
                        }
                    }
                    else if (ServiceType == ServiceType.ScriptableObject)
                    {
                        var so = instance as ScriptableObject;
                        if (so != null)
                        {
                            UnityEngine.Object.DestroyImmediate(so);
                        }
                    }
                    
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    // Make sure we don't leave the task hanging if there's an error
                    tcs.SetException(ex);
                }
            });

            // Wait for the Unity operations to complete
            await tcs.Task;
        }

        public async Task ClearInstanceAsync(string key)
        {
            try
            {
                // Check if instance exists
                if (_instances.TryGetValue(key, out var instance))
                { 
                    await ClearInstanceAsync(instance);
                }
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Error clearing instance for key {key}: {ex.Message}");
            }

        }


        public async Task ClearInstanceAsync(object instance, string key = null)
        {
            try
            {
                if (key == null)
                {
                    key = _instances.FirstOrDefault(kvp => kvp.Value == instance).Key;
                }
                Debug.Log($"Clearing instance for key: {key}");

                // Check if instance is both disposable and poolable
                bool isDisposable = instance is IServiceDisposable;
                bool isPoolable = instance is IServicePoolable;

                if (isDisposable)
                {
                    var disposable = instance as IServiceDisposable;
                    _disposingInProgress.TryAdd(disposable, 0);
                    
                    // First let the service do its own cleanup
                    await disposable.OnSystemDisposeAsync();

                    if(disposable.IsDisposed)
                    {
                        ServiceDiagnostics.LogInfo($"Service {ImplementationType.Name} disposed successfully");
                    }
                    else
                    {
                        ServiceDiagnostics.LogError($"Service {ImplementationType.Name} failed to dispose");
                    }
                    
                    // If it's also poolable, return it to the pool instead of destroying it
                    if (isPoolable)
                    {
                        var poolable = instance as IServicePoolable;
                        poolable.OnReturnToPool();
                        
                        // For MonoBehaviour instances, disable the GameObject and move it to the service pool
                        if (instance is MonoBehaviour mono)
                        {
                            // Clear scene association for scene-specific services
                            if (Lifetime == ServiceLifetime.SceneSingleton || Lifetime == ServiceLifetime.SceneTransient)
                            {
                                ServiceDiagnostics.LogInfo($"Clearing scene association for {Lifetime} service {ImplementationType.Name}");
                            }
                            
                            mono.gameObject.SetActive(false);
                            GameObject.DontDestroyOnLoad(mono.gameObject);
                            mono.gameObject.name = POOLED_KEY;
                            mono.transform.SetParent(_servicePoolParent);
                        }
                        
                        // Add to pooled instances
                        _pooledInstances.TryAdd(instance, 0);
                        
                        ServiceDiagnostics.LogInfo($"Returned disposable service instance to pool for key: {key}");
                    }
                    else
                    {
                        // If not poolable, destroy the GameObject
                        await DestroyUnityObjectAsync(instance);
                    }
                    
                    _disposingInProgress.TryRemove(disposable, out _);
                    
                    // Make sure the instance is removed from instances
                    _instances.TryRemove(key, out _);
                }
                else if (isPoolable)
                {
                    // Handle poolable non-disposable service as before
                    var poolable = instance as IServicePoolable;
                    poolable.OnReturnToPool();
                    
                    // For MonoBehaviour instances, disable the GameObject and move it to the service pool
                    if (instance is MonoBehaviour mono)
                    {
                        // Clear scene association for scene-specific services
                        if (Lifetime == ServiceLifetime.SceneSingleton || Lifetime == ServiceLifetime.SceneTransient)
                        {
                            ServiceDiagnostics.LogInfo($"Clearing scene association for {Lifetime} service {ImplementationType.Name}");
                        }
                        
                        mono.gameObject.SetActive(false);
                        GameObject.DontDestroyOnLoad(mono.gameObject);
                        mono.gameObject.name = POOLED_KEY;
                        mono.transform.SetParent(_servicePoolParent);
                    }
                    
                    // Add to pooled instances
                    _pooledInstances.TryAdd(instance, 0);
                    
                    ServiceDiagnostics.LogInfo($"Returned service instance to pool for key: {key}");
                    
                    // Remove from active instances
                    _instances.TryRemove(key, out _);
                }
                else if (instance is UnityEngine.Object unityObj)
                {
                    // For non-poolable, non-disposable instances, destroy normally
                    await DestroyUnityObjectAsync(instance);
                    _instances.TryRemove(key, out _);
                    ServiceDiagnostics.LogInfo($"Destroyed service instance for key: {key}");
                }
                else
                {
                    // For regular C# objects, just remove from instances
                    _instances.TryRemove(key, out _);
                    ServiceDiagnostics.LogInfo($"Cleared non-disposable service instance for key: {key}");
                }
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Error clearing instance for key {key}: {ex.Message}");
            }
        }



        public bool BelongsToScene(object serviceInstance, Scene scene)
        {
            if (Lifetime == ServiceLifetime.SceneSingleton)
            {
                if( TryGetInstanceFromDictionary(scene.name, out var instance))
                {
                    return serviceInstance == instance;
                }
                return false;
            }
            else if (Lifetime == ServiceLifetime.SceneTransient)
            {
                // ConcurrentDictionary.Keys is thread-safe for reading
                return _instances.Keys.Any(key => key.StartsWith($"{scene.name}_") && TryGetInstanceFromDictionary(key, out var instance) && instance == serviceInstance);
            }
            return false;
        }

        public async Task ClearSceneInstancesAsync(Scene scene)
        {
            if (Lifetime != ServiceLifetime.SceneSingleton && Lifetime != ServiceLifetime.SceneTransient)
            {
                throw new InvalidOperationException("ClearSceneInstancesAsync can only be used with SceneSingleton or SceneTransient lifetime");
            }

            if (Lifetime == ServiceLifetime.SceneSingleton)
            {
                await ClearInstanceAsync(scene.name);
            }
            else if (Lifetime == ServiceLifetime.SceneTransient)
            {
                List<Task> disposalTasks = new();
                List<string> keysToDispose = new();

                // ConcurrentDictionary.Keys is thread-safe for reading
                keysToDispose = _instances.Keys
                    .Where(key => key.StartsWith($"{scene.name}_"))
                    .ToList();

                // Dispose each instance
                foreach (var key in keysToDispose)
                {
                    disposalTasks.Add(ClearInstanceAsync(key));
                }

                if (disposalTasks.Count > 0)
                {
                    await Task.WhenAll(disposalTasks);
                }
                
                // Also remove any pooled instances that were created for this scene
                // Create a list of instances to remove to avoid modifying during enumeration
                var pooledInstancesToRemove = new List<object>();
                
                foreach (var instance in _pooledInstances.Keys)
                {
                    // For MonoBehaviour instances, check if they were created for this scene
                    if (instance is MonoBehaviour mb && mb.gameObject != null)
                    {
                        // If the instance name is prefixed with the scene name, it belongs to this scene
                        string instanceName = mb.gameObject.name;
                        if (instanceName.StartsWith($"{scene.name}_") || 
                            (instanceName == POOLED_KEY && mb.gameObject.scene == scene))
                        {
                            pooledInstancesToRemove.Add(instance);
                        }
                    }
                }
                
                // Now remove and destroy the pooled instances
                foreach (var instance in pooledInstancesToRemove)
                {
                    if (_pooledInstances.TryRemove(instance, out _))
                    {
                        if (instance is MonoBehaviour mb)
                        {
                            await DestroyUnityObjectAsync(mb);
                        }
                    }
                }
            }
        }

        public async Task ClearAllInstancesAsync()
        {
            List<Task> disposalTasks = new();

            // ConcurrentDictionary.Keys is thread-safe for reading
            foreach (var key in _instances.Keys.ToList())
            {
                disposalTasks.Add(ClearInstanceAsync(key));
            }
            
            if (disposalTasks.Count > 0)
            {
                await Task.WhenAll(disposalTasks);
            }
        }

        public object GetInstance(Scene requestingScene)
        {
            // Check for async dependencies - this was removed when implementing pooling
            if (_typeInfo.IsAsyncService || _typeInfo.HasAsyncDependency)
            {
                throw ServiceDiagnostics.CreateAsyncAccessViolationException(
                    ImplementationType,
                    "Service has async dependencies in its chain. Use GetAsync() and consider making all types in the dependency chain IServiceInitializable to properly handle async initialization.");
            }
            
            object instance = null;
            // get existing instance
            string key = GetInstanceKey(requestingScene);

            if(Lifetime == ServiceLifetime.Singleton || Lifetime == ServiceLifetime.SceneSingleton)
            {
                instance = GetInstanceFromDictionary(key);
                if(instance != null)
                {
                    return instance;
                }
            }
            
            
            // For transient services, always create a new instance or get from pool
            if(Lifetime == ServiceLifetime.Transient || Lifetime == ServiceLifetime.SceneTransient)
            {
                key = GetInstanceKey(requestingScene, true);
            }
            
            // Try to get a pooled instance first
            if (TryGetPooledInstance(out var pooledInstance, requestingScene))
            {
                instance = pooledInstance;
                
            }


            if(instance == null) instance = CreateInstance(requestingScene, key);

            
            
            SetInstanceInDictionary(key, instance, requestingScene);


            return instance;
        }

        public async Task<object> GetInstanceAsync(Scene requestingScene)
        {
           
            // Prevent async retrieval of sync services
            if (!_typeInfo.IsAsyncService && _typeInfo.HasAsyncDependency)
            {
                var message = $"Cannot use GetAsyncService for non-async service {ImplementationType.Name}. Use GetService instead for synchronous services.";
                ServiceDiagnostics.LogError(message);
                throw new InvalidOperationException(message);
            }
            
            object instance = null;
            // get existing instance
            string key = GetInstanceKey(requestingScene);

            if(Lifetime == ServiceLifetime.Singleton || Lifetime == ServiceLifetime.SceneSingleton)
            {
                instance = GetInstanceFromDictionary(key);

                if(instance != null)
                {
                    return instance;
                }
            }
            
            
            // Try to get a pooled instance first
            if (TryGetPooledInstance(out var pooledInstance, requestingScene))
            {
                instance = pooledInstance;
            }
            

            if(instance == null) instance = CreateInstance(requestingScene, key);

            
            
            SetInstanceInDictionary(key, instance, requestingScene);


            await InitializeAsync(instance, key);

            return instance;
        }

        private string GetInstanceKey(Scene requestingScene, bool generateKey = false)
        {
            
            string GenerateInstanceKey(bool increment = false)
            {
                lock (_lock)
                {
                    var nextInstanceCounter = _instanceCounter + (increment ? 1 : 0);
                    return $"{Name}_{nextInstanceCounter}";
                }
            }


            switch (Lifetime)
            {
                case ServiceLifetime.Singleton:
                    return SINGLETON_KEY;
                case ServiceLifetime.SceneSingleton:
                    return requestingScene.name;
                case ServiceLifetime.Transient:
                    return   GenerateInstanceKey(generateKey) ;
                case ServiceLifetime.SceneTransient:
                    var key = $"{requestingScene.name}_{GenerateInstanceKey(generateKey)}";
                    return  key;
                default:
                    throw new InvalidOperationException($"Unsupported service lifetime: {Lifetime}");
            }
        }





        public string GetNextInstanceName()
        {
            lock (_lock)
            {
                return $"{Name}_{_instanceCounter + 1}";
            }
        }

        public void IncrementInstanceCounter()
        {
            lock (_lock)
            {
                _instanceCounter++;
            }
        }





        /// <summary>
        /// Gets all service instances managed by this registration.
        /// </summary>
        /// <returns>A dictionary of all instance keys and their corresponding service instances.</returns>
        public Dictionary<string, object> GetAllInstances()
        {
            // ConcurrentDictionary can be safely converted to Dictionary
            return _instances.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private object CreateInstance(Scene scene, string key)
        {
            try
            {
                object instance = null;
                switch (ServiceType)
                {
                    case ServiceType.MonoBehaviour:
                        // Ensure MonoBehaviour creation happens on the main thread
                        if (!ServiceCoroutineRunner.IsMainThread())
                        {
                            // The call would throw an exception if not on the main thread
                            // Instead, we'll create a TaskCompletionSource and execute on the main thread
                            var tcs = new TaskCompletionSource<object>();
                            
                            ServiceCoroutineRunner.RunOnMainThread(() => {
                                try
                                {
                                    // Try to find an existing instance first
                                    if (Lifetime == ServiceLifetime.Singleton || Lifetime == ServiceLifetime.SceneSingleton)
                                        instance = MonoServiceFactory.FindSceneInstance(ImplementationType, key, scene.name);
                                    
                                    // If not found, create a new one
                                    if (instance == null)
                                        instance = MonoServiceFactory.CreateInstance(ImplementationType, key);
                                    
                                    tcs.SetResult(instance);
                                }
                                catch (Exception ex)
                                {
                                    tcs.SetException(ex);
                                }
                            });
                            
                            // Wait for the result synchronously - this is safe because we're not on the main thread
                            instance = tcs.Task.GetAwaiter().GetResult();
                        }
                        else
                        {
                            // Already on main thread, create directly
                            if (Lifetime == ServiceLifetime.Singleton || Lifetime == ServiceLifetime.SceneSingleton)
                                instance = MonoServiceFactory.FindSceneInstance(ImplementationType, key, scene.name);
                            
                            if (instance == null)
                                instance = MonoServiceFactory.CreateInstance(ImplementationType, key);
                        }
                        break;
                    
                    case ServiceType.ScriptableObject:
                        // Create a ScriptableObject instance
                        if(Lifetime == ServiceLifetime.Singleton)
                            instance = SOServiceFactory.FindResourceInstance(ImplementationType, key);
                       
                        if(instance == null)
                            instance = SOServiceFactory.CreateInstance(ImplementationType, key);

                        break;
                    
                    case ServiceType.Regular:
                        // Create a regular C# class instance
                        instance = RegularServiceFactory.GetInstance(ImplementationType, key);
                        break;
                    
                    default:
                        throw new ServiceInitializationException(
                            $"Unsupported service type: {ServiceType} for {ImplementationType.Name}"
                        );
                }

                if (instance == null)
                {
                    throw new ServiceInitializationException($"Failed to create instance of service {ImplementationType.Name}");
                }

                ServiceDiagnostics.LogServiceInstanceCreation(ImplementationType, key, scene.name);
                
                return instance;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ServiceInitializationException)
            {
                throw ex.InnerException;
            }
            catch (ServiceInitializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ServiceLocatorException(
                    $"Error creating service instance {ImplementationType.Name}: {ex.Message}",
                    ex
                );
            }
        }
        
        private async Task InitializeAsync(object instance, string key)
        {
            if (instance is IServiceInitializable asyncInit && !asyncInit.IsInitialized)
            {
                await asyncInit.InitializeAsync();
            }
        }

        /// <summary>
        /// Cleans up the pooled instances and pool parent GameObject when a service is unregistered.
        /// This should only be called when the service is being completely unregistered.
        /// </summary>
        public async Task CleanupServicePool()
        {
            // Clear all pooled instances
            _pooledInstances.Clear();
            
            // Clean up the service pool parent GameObject if it exists
            if (_servicePoolParent != null)
            {
                var tcs = new TaskCompletionSource<bool>();
                
                ServiceCoroutineRunner.RunOnMainThread(() => {
                    try
                    {
                        GameObject.DestroyImmediate(_servicePoolParent.gameObject);
                        _servicePoolParent = null;
                        ServiceDiagnostics.LogInfo($"Destroyed pool parent for service {ImplementationType.Name}");
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        ServiceDiagnostics.LogError($"Error destroying pool parent for service {ImplementationType.Name}: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                await tcs.Task;
            }
        }

       
    }
} 