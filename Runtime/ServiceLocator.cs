using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Tracking;
using GAOS.ServiceLocator.Optional;
using GAOS.Logger;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Thread-safe service locator for managing services of different types:
    /// - Regular C# services
    /// - MonoBehaviour services
    /// - ScriptableObject services
    /// 
    /// Service lifecycle rules:
    /// - Runtime and RuntimeAndEditor services are cleared when:
    ///   * Entering play mode (fresh start)
    ///   * Exiting play mode (cleanup)
    /// - EditorOnly services persist their state between play sessions
    /// - Services follow their respective Unity lifecycle based on their type
    /// </summary>
    public static class ServiceLocator
    {
      
        private static readonly ConcurrentDictionary<ServiceKey, ServiceRegistration> _registrations = new ConcurrentDictionary<ServiceKey, ServiceRegistration>();
          
        private static volatile bool _isInitialized;
        private static ServiceTypeCache _typeCache;
        private static Transform _servicePoolTransform;
        private static ServiceCoroutineRunner _serviceCoroutineRunner;
        internal static bool SkipEditorContextValidation { get; private set; }
        
        // Cache the active scene to prevent threading issues
        private static Scene _cachedActiveScene;
        private static readonly object _sceneCacheLock = new object();

        // Service Pool GameObject Transform for pooled instances
        internal static Transform ServicePoolTransform => _servicePoolTransform;
        
        internal static ServiceCoroutineRunner ServiceCoroutineRunner => _serviceCoroutineRunner;
        
      
        private static ServiceTypeCache TypeCache
        {
            get
            {
                if (_typeCache == null)
                {
                    _typeCache = Resources.Load<ServiceTypeCache>("ServiceTypeCache");
                }
                return _typeCache;
            }
        }

        // For test use only - allows runtime context services in editor assemblies during testing
        internal static void SetSkipEditorContextValidation(bool skip)
        {
            SkipEditorContextValidation = skip;
        }

     

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            if (_isInitialized) return;
            
            // Register the logging system first
            GLog.RegisterSystem( ServiceLocatorLogSystem.Instance);
            
            ServiceDiagnostics.LogInfo("Initializing ServiceLocator");
            
            // Access the singleton instances (they'll be created if they don't exist)
            _serviceCoroutineRunner = ServiceCoroutineRunner.Instance;
            _servicePoolTransform = ServicePool.Instance.transform;
            
            _isInitialized = true;
            
            // Initialize the cached scene
            if (ServiceCoroutineRunner.IsMainThread())
            {
                _cachedActiveScene = SceneManager.GetActiveScene();
            }
            
            // Subscribe to scene events
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            // Subscribe to active scene changed
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            
            // Auto-register services marked with ServiceAttribute
            AutoRegisterServices();
            
            ServiceDiagnostics.LogInfo("ServiceLocator initialized successfully");
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Update our cached active scene
            lock (_sceneCacheLock)
            {
                _cachedActiveScene = newScene;
                ServiceDiagnostics.LogInfo($"Updated cached active scene: {newScene.name}");
            }
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // Start a coroutine on the singleton instance
            _serviceCoroutineRunner.StartCoroutine(CleanupSceneRoutine(scene));
        }

        // Get the active scene safely (thread-safe)
        internal static Scene GetActiveSceneSafe()
        {
            if (ServiceCoroutineRunner.IsMainThread())
            {
                // If on main thread, we can get the current active scene directly
                return SceneManager.GetActiveScene();
            }
            
            // If not on main thread, use the cached scene
            lock (_sceneCacheLock)
            {
                if (!_cachedActiveScene.IsValid())
                {
                    // If the cached scene is invalid, log a warning
                    ServiceDiagnostics.LogWarning("Cached active scene is invalid when accessed from background thread");
                }
                return _cachedActiveScene;
            }
        }

        private static IEnumerator CleanupSceneRoutine(Scene scene)
        {
            List<Task> disposalTasks = new();
            
            // Gather tasks as before
            foreach (var registration in _registrations.Values)
            {
                if (registration.Lifetime == ServiceLifetime.SceneSingleton || 
                     registration.Lifetime == ServiceLifetime.SceneTransient)
                {
                    disposalTasks.Add(registration.ClearSceneInstancesAsync(scene));
                }
            }
            
            // Wait for tasks to complete while yielding back to Unity
            while (disposalTasks.Any(t => !t.IsCompleted))
            {
                yield return null;
            }
            
            // Handle any exceptions
            foreach (var task in disposalTasks.Where(t => t.IsFaulted))
            {
                ServiceDiagnostics.LogError($"Error during scene cleanup: {task.Exception}");
            }
            
            ServiceDiagnostics.LogInfo($"Completed cleanup for scene: {scene.name}");
        }

        

        private struct ServiceKey
        {
            public Type InterfaceType { get; }
            public string Name { get; }

            public ServiceKey(Type interfaceType, string name)
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("Service name cannot be null or empty", nameof(name));

                InterfaceType = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));
                Name = name;
            }
        }


        /// <summary>
        /// Auto-registers all types marked with the ServiceAttribute in the specified assembly
        /// </summary>
        public static void AutoRegisterServices()
        {
            GLog.Info<ServiceLocatorLogSystem>($"Starting AutoRegisterServices. Cache has {TypeCache.ServiceTypes.Count} types");
            var registeredTypes = new HashSet<Type>();

            foreach (var info in TypeCache.ServiceTypes)
            {
                if (info == null)
                {
                    GLog.Warning<ServiceLocatorLogSystem>("Null type info found in registry");
                    continue;
                }

                try
                {
                    var type = info.ImplementationType;
                    GLog.Info<ServiceLocatorLogSystem>($"Processing type info: ServiceType={info.ServiceType}, ImplementationType={info.ImplementationTypeName}");
                    
                    if (type == null)
                    {
                        GLog.Error<ServiceLocatorLogSystem>($"Failed to resolve type: {info.ImplementationTypeName} from {info.ImplementationTypeAssemblyQualifiedName}");
                        continue;
                    }

                    var key = new ServiceKey(info.InterfaceType, info.DefaultName);
                    // ConcurrentDictionary is thread-safe for reads
                    if (_registrations.TryGetValue(key, out var existing))
                    {
                        if (existing.Context == ServiceContext.EditorOnly)
                        {
                            GLog.Info<ServiceLocatorLogSystem>($"Keeping existing EditorOnly service: {type.Name} with name {info.DefaultName}");
                            continue;
                        }
                    }

                    var registration = new ServiceRegistration(info);
                    _registrations.AddOrUpdate(key, registration, (_, __) => registration);
                           
                    GLog.Info<ServiceLocatorLogSystem>($"Registered MonoBehaviour service: {type.Name} with name {info.DefaultName}");
                    
                    registeredTypes.Add(type);
                }
                catch (Exception ex)
                {
                    GLog.Error<ServiceLocatorLogSystem>($"Error registering service {info.ImplementationTypeName}: {ex}");
                }
            }

            GLog.Info<ServiceLocatorLogSystem>($"AutoRegisterServices completed. Registered {registeredTypes.Count} types");
        }

        /// <summary>
        /// Internal method for registering a service type with its implementation.
        /// This method is intended for test usage only through ITestServiceRegistration.
        /// </summary>
        /// <param name="serviceType">The service interface type</param>
        /// <param name="implementationType">The implementation type</param>
        /// <param name="name">The service name</param>
        /// <param name="lifetime">The service lifetime (Singleton, SceneSingleton, Transient, or SceneTransient)</param>
        /// <param name="context">The service context</param>
        internal static void Register(Type serviceType, Type implementationType, string name, ServiceLifetime lifetime = ServiceLifetime.Singleton, ServiceContext context = ServiceContext.Runtime)
        {
            ServiceValidator.ValidateServiceName(name);
            ServiceValidator.ValidateInterfaceImplementation(serviceType, implementationType);

            var typeInfo = TypeCache.GetTypeInfo(implementationType);
            if (typeInfo == null)
            {
                ServiceDiagnostics.LogWarning($"Service type {implementationType.Name} not found in typecache. Using runtime reflection.");
                typeInfo = ServiceTypeInfo.CreateInstance(serviceType, implementationType, lifetime, context, name);
            }

            var key = new ServiceKey(serviceType, name);
            var registration = new ServiceRegistration(typeInfo, lifetime, context, name);

            // Check if service is already registered
            if (_registrations.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException($"Service of type {serviceType.Name} with name '{name}' is already registered");
            }
            else
            {
                // New registration
                _registrations.TryAdd(key, registration);
                ServiceDiagnostics.LogServiceRegistration(serviceType, name, lifetime);
            }
        }

        /// <summary>
        /// Internal method for registering a service type with its implementation.
        /// This method is intended for test usage only through ITestServiceRegistration.
        /// </summary>
        internal static void Register<TService, TImplementation>(string name, ServiceLifetime lifetime = ServiceLifetime.Singleton, ServiceContext context = ServiceContext.Runtime)
            where TImplementation : class, TService
        {
            Register(typeof(TService), typeof(TImplementation), name, lifetime, context);
        }

       


        /// <summary>
        /// Gets a service of the specified type and name.
        /// </summary>
        public static TService GetService<TService>(string name, Component requestingComponent = null) where TService : class
        {
            if (requestingComponent == null)
            {
                // Check if we have a registration for this service and whether it's scene-dependent
                var key = new ServiceKey(typeof(TService), name);
                if (_registrations.TryGetValue(key, out var registration))
                {
                    // Only use active scene for scene-dependent services (SceneSingleton or SceneTransient)
                    if (registration.Lifetime == ServiceLifetime.SceneSingleton || 
                        registration.Lifetime == ServiceLifetime.SceneTransient)
                    {
                        return GetService<TService>(name, GetActiveSceneSafe());
                    }
                    
                    // For regular services, use a default "empty" scene - cast to Scene to avoid ambiguity
                    Scene emptyScene = default(Scene);
                    return GetService<TService>(name, emptyScene);
                }
                
                // If no registration found, use cached active scene as fallback
                return GetService<TService>(name, GetActiveSceneSafe());
            }
            
            // If we have a component, use its scene directly
            return GetService<TService>(name, requestingComponent.gameObject.scene);
        }

        /// <summary>
        /// Gets a service of the specified type and name from a specific scene.
        /// </summary>
        private static TService GetService<TService>(string name, Scene requestingScene) where TService : class
        {
            var instance = GetService(typeof(TService), name, requestingScene);
            
            // Add a safety check before casting
            if (instance is TService typedInstance)
            {
                return typedInstance;
            }
            
            // If we get here, the cast would fail - throw a more descriptive exception
            throw new InvalidCastException(
                $"Service instance of type '{instance?.GetType().Name ?? "null"}' cannot be cast to '{typeof(TService).Name}'. " +
                $"Check that the service implementation correctly implements the requested interface."
            );
        }

        /// <summary>
        /// Gets a service of the specified type and name.
        /// </summary>
        public static object GetService(Type serviceType, string name, Component requestingComponent = null)
        {
            if (requestingComponent == null)
            {
                // Check if we have a registration for this service and whether it's scene-dependent
                var key = new ServiceKey(serviceType, name);
                if (_registrations.TryGetValue(key, out var registration))
                {
                    // Only use active scene for scene-dependent services (SceneSingleton or SceneTransient)
                    if (registration.Lifetime == ServiceLifetime.SceneSingleton || 
                        registration.Lifetime == ServiceLifetime.SceneTransient)
                    {
                        return GetService(serviceType, name, GetActiveSceneSafe());
                    }
                    
                    // For regular services, use a default "empty" scene - cast to Scene to avoid ambiguity
                    Scene emptyScene = default(Scene);
                    return GetService(serviceType, name, emptyScene);
                }
                
                // If no registration found, use cached active scene as fallback
                return GetService(serviceType, name, GetActiveSceneSafe());
            }
            
            // If we have a component, use its scene directly
            return GetService(serviceType, name, requestingComponent.gameObject.scene);
        }

        /// <summary>
        /// Gets a service of the specified type and name from a specific scene.
        /// </summary>
        private static object GetService(Type serviceType, string name, Scene requestingScene)
        {
            ServiceValidator.ValidateServiceName(name);
            
            var key = new ServiceKey(serviceType, name);
            ServiceRegistration registration = null;

            // ConcurrentDictionary is thread-safe, so we don't need a lock
            bool registrationFound = _registrations.TryGetValue(key, out registration);
            
            if (!registrationFound || registration == null)
            {
                var message = $"Service of type {serviceType.Name} with name '{name}' is not registered";
                ServiceDiagnostics.LogServiceResolution(serviceType, name, false);
                ServiceDiagnostics.NotifyServiceNotFound(serviceType, name, message);
                throw new InvalidOperationException(message);
            }
            
            object normalInstance = registration.GetInstance(requestingScene);
            
            
            if (normalInstance == null)
            {
                throw new InvalidOperationException($"Failed to get instance of service {serviceType.Name} with name '{name}'");
            }

            ServiceDiagnostics.LogServiceResolution(serviceType, name, true);
            return normalInstance;
        }

        /// <summary>
        /// Gets all registered service names for a specific type
        /// </summary>
        public static IEnumerable<string> GetServiceNames<TService>() where TService : class
        {
            return GetServiceNames(typeof(TService));
        }

        /// <summary>
        /// Gets all registered service names for a specific type
        /// </summary>
        public static IEnumerable<string> GetServiceNames(Type serviceType)
        {
            // ConcurrentDictionary is thread-safe for reads
            return _registrations
                .Where(kvp => kvp.Key.InterfaceType == serviceType)
                .Select(kvp => kvp.Key.Name)
                .ToList();
        }

     

        
        private static IEnumerator UnregisterServiceCoroutineImpl(Type serviceType, string name)
        {
            var task = UnregisterService(serviceType, name);
            
            // Wait for the task to complete
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            // Handle exceptions
            if (task.IsFaulted && task.Exception != null)
            {
                ServiceDiagnostics.LogError($"Error unregistering service {serviceType.Name} with name '{name}': {task.Exception.InnerException?.Message ?? task.Exception.Message}");
                throw task.Exception;
            }
        }

        /// <summary>
        /// Unregisters a service
        /// </summary>
        internal static async Task UnregisterService(Type serviceType, string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Service name cannot be null or empty", nameof(name));

            var key = new ServiceKey(serviceType, name);
            ServiceRegistration registration;
            
            if (!_registrations.TryRemove(key, out registration))
            {
                throw new InvalidOperationException($"Service {serviceType.Name} with name '{name}' is not registered");
            }

            try
            {
                await registration.ClearAllInstancesAsync();
                    
                
                // Clean up the service pool if it implements IServicePoolable
                if (typeof(IServicePoolable).IsAssignableFrom(registration.ImplementationType))
                {
                    await registration.CleanupServicePool();
                }
                
                ServiceDiagnostics.LogInfo($"Unregistered service {serviceType.Name} with name '{name}'");
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogError($"Error while unregistering service {serviceType.Name} with name '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unregisters all services
        /// </summary>
        internal static async Task Clear()
        {
            List<Task> disposalTasks = new();
            
            // ConcurrentDictionary is thread-safe for reads, no lock needed
            // Make a copy of the registrations to avoid modification during enumeration
            var registrationsToDispose = _registrations.ToArray();

            // Use separate tasks for scene-related and non-scene-related services
            List<Task> sceneRelatedTasks = new();
            List<Task> nonSceneRelatedTasks = new();

            foreach (var registration in registrationsToDispose)
            {
               await UnregisterService(registration.Key.InterfaceType, registration.Key.Name);
            }


            // ConcurrentDictionary is thread-safe for operations
            _registrations.Clear();
        }

       


        /// <summary>
        /// Clears all services except editor-only services.
        /// This includes:
        /// - All Runtime and RuntimeAndEditor ScriptableObject services
        /// - All regular C# services
        /// - All MonoBehaviour services
        /// 
        /// EditorOnly services are preserved to maintain editor tool state between play sessions.
        /// For SO services, their Cleanup method is called before removal.
        /// </summary>
        internal static async void ClearNonEditorServices()
        {
            List<Task> disposalTasks = new();

            // ConcurrentDictionary is thread-safe for reads
            var servicesToClear = _registrations
                .Where(kvp => kvp.Value.Context != ServiceContext.EditorOnly)
                .Select(kvp => kvp.Key)
                .ToList();


            foreach (var key in servicesToClear)
            {
                await UnregisterService(key.InterfaceType, key.Name);
            }
        }

        /// <summary>
        /// Gets information about all registered services
        /// </summary>
        internal static IEnumerable<ServiceRegistrationInfo> GetRegisteredServices()
        {
            // ConcurrentDictionary is thread-safe for reads
            return _registrations.Select(kvp =>
            {
                var stats = ServiceTracker.GetStats(kvp.Value.ImplementationType);
                return new ServiceRegistrationInfo
                {
                    Name = kvp.Key.Name,
                    InterfaceType = kvp.Value.InterfaceType,
                    ImplementationType = kvp.Value.ImplementationType,
                    ServiceType = kvp.Value.ServiceType,
                    Lifetime = kvp.Value.Lifetime,
                    Context = kvp.Value.Context,
                    IsInitialized = kvp.Value.IsInitialized,
                    HasError = kvp.Value.Lifetime != ServiceLifetime.Transient && kvp.Value.HasError,
                    TotalInstancesCreated = stats.TotalCreated,
                    ActiveInstanceCount = stats.ActiveInstanceCount,
                    LastInstanceCreated = stats.LastCreated
                };
            }).ToList();
        }

        /// <summary>
        /// Information about a registered service
        /// </summary>
        internal class ServiceRegistrationInfo
        {
            public string Name { get; set; }
            public Type InterfaceType { get; set; }
            public Type ImplementationType { get; set; }
            public ServiceType ServiceType { get; set; }
            public ServiceLifetime Lifetime { get; set; }
            public ServiceContext Context { get; set; }
            public bool IsInitialized { get; set; }
            public bool HasError { get; set; }
            public int TotalInstancesCreated { get; set; }
            public int ActiveInstanceCount { get; set; }
            public DateTime LastInstanceCreated { get; set; }
        }



        /// <summary>
        /// Gets a service asynchronously, handling any async initialization
        /// </summary>
        public static Task<T> GetAsyncService<T>(string name, Component requestingComponent = null) where T : class
        {
            if (requestingComponent == null)
            {
                // Check if we have a registration for this service and whether it's scene-dependent
                var key = new ServiceKey(typeof(T), name);
                if (_registrations.TryGetValue(key, out var registration))
                {
                    // Only use active scene for scene-dependent services (SceneSingleton or SceneTransient)
                    if (registration.Lifetime == ServiceLifetime.SceneSingleton || 
                        registration.Lifetime == ServiceLifetime.SceneTransient)
                    {
                        return GetAsyncService<T>(name, GetActiveSceneSafe());
                    }
                    
                    // For regular services, use a default "empty" scene - cast to Scene to avoid ambiguity
                    Scene emptyScene = default(Scene);
                    return GetAsyncService<T>(name, emptyScene);
                }
                
                // If no registration found, use cached active scene as fallback
                return GetAsyncService<T>(name, GetActiveSceneSafe());
            }
            
            // If we have a component, use its scene directly 
            return GetAsyncService<T>(name, requestingComponent.gameObject.scene);
        }

        /// <summary>
        /// Gets a service asynchronously from a specific scene
        /// </summary>
        private static async Task<T> GetAsyncService<T>(string name, Scene requestingScene) where T : class
        {
            ServiceValidator.ValidateServiceName(name);
            
            var serviceType = typeof(T);
            var key = new ServiceKey(serviceType, name);
            
            // ConcurrentDictionary is thread-safe for reads
            if (!_registrations.TryGetValue(key, out var registration))
            {
                var message = $"Service of type {typeof(T).Name} with name '{name}' is not registered";
                ServiceDiagnostics.LogServiceResolution(serviceType, name, false);
                ServiceDiagnostics.NotifyServiceNotFound(typeof(T), name, message);
                throw new InvalidOperationException(message);
            }

            try
            {
                var instance = await registration.GetInstanceAsync(requestingScene);
                ServiceDiagnostics.LogServiceResolution(serviceType, name, true);
                return (T)instance;
            }
            catch (Exception ex)
            {
                ServiceDiagnostics.LogServiceResolution(serviceType, name, false);
                throw new ServiceLocatorException($"Error resolving service {registration.ImplementationType.Name}", ex);
            }
        }

        /// <summary>
        /// Releases a service instance and properly disposes it.
        /// WARNING: This is a non-blocking operation. The service will be queued for release on the main thread.
        /// If you need to ensure the service is released before continuing, use ReleaseServiceInstanceCoroutine instead.
        /// </summary>
        /// <param name="name">The service name</param>
        /// <param name="service">The service instance to release</param>
        /// <returns>True if the operation was queued successfully</returns>
        /// <exception cref="ArgumentNullException">Thrown if service is null</exception>
        public static void ReleaseServiceInstance<T>(string name, object service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if(ServiceCoroutineRunner.IsMainThread())
            {
                ReleaseServiceInstanceAsync<T>(name, service).ConfigureAwait(false);
            }else               
            {
                // Queue the release operation to be executed on the main thread
                // This prevents deadlocks when releasing services from the main thread
                ServiceCoroutineRunner.RunOnMainThread(() => 
                {
                    // Start the release operation as a fire-and-forget task
                    ReleaseServiceInstanceAsync<T>(name, service).ConfigureAwait(false);
                    
                });
            }
        }
        
      

        /// <summary>
        /// Releases a service instance and properly disposes it asynchronously.
        /// 
        /// This is the recommended approach when you need to wait for or track the completion
        /// of a service disposal operation. The returned Task can be awaited to ensure 
        /// the service is fully disposed before proceeding.
        /// 
        /// For fire-and-forget disposal where completion tracking isn't needed, 
        /// you can use ReleaseServiceInstance instead.
        /// </summary>
        /// <typeparam name="T">The service interface type</typeparam>
        /// <param name="name">The service name</param>
        /// <param name="service">The service instance to release</param>
        /// <returns>True if the service was found and released, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown if service is null</exception>
        public static async Task<bool> ReleaseServiceInstanceAsync<T>(string name, object service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            ServiceRegistration targetRegistration = null;

            // Find the registration and instance key for this service
            // ConcurrentDictionary is thread-safe for reads
            foreach (var kvp in _registrations)
            {
                var registration = kvp.Value;

                if(kvp.Key.Name != name) continue;

                if(registration.InterfaceType != typeof(T)) continue;
                

                if (registration.ImplementationType == service.GetType())
                {
                    targetRegistration = registration;
                    break;
                }

                if (targetRegistration != null)
                    break;
            }

            if (targetRegistration == null)
            {
                ServiceDiagnostics.LogWarning($"Service instance of type {service.GetType().Name} was not found in any registration");
                return false;
            }

            // We found the registration and key, now clear the instance
            await targetRegistration.ClearInstanceAsync(service);
            return true;
        }


        
        public static bool CheckServiceBelongsToScene<T>(object service, string name, Scene scene)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var key = new ServiceKey(typeof(T), name);
            _registrations.TryGetValue(key, out var registration);
            
            if (registration == null)
                return false;

            return registration.BelongsToScene(service, scene);
        }

     
    }
} 