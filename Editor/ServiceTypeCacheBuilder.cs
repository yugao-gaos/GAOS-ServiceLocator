using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Optional;
namespace GAOS.ServiceLocator.Editor
{
    [InitializeOnLoad]
    public static class ServiceTypeCacheBuilder
    {
        private const string REGISTRY_PATH = "Assets/Resources/ServiceTypeCache.asset";
        private const string AUTO_REBUILD_PREF = "GAOS.ServiceLocator.AutoRebuildRegistry";
        private static bool _isRebuilding;
        private static bool _isUnitTest;

        // Event for cache updates
        public static event Action OnTypeRegistryRebuilt;

        // Validation cache structure
        public class DependencyNode
        {
            public Type Type { get; set; }
            public List<DependencyNode> Dependencies { get; set; } = new List<DependencyNode>();
            public bool IsInterface { get; set; }
            public bool IsImplementation { get; set; }
        }

        public class ServiceValidationData
        {
            public bool HasCircularDependency { get; set; }
            public bool IsImplementationDependency { get; set; }
            public string DependencyChain { get; set; }
            public DependencyNode DependencyTree { get; set; }  // Full tree for circular dependency detection
            public List<(string message, UnityEditor.MessageType type)> ValidationMessages { get; set; }
        }

        private static readonly Dictionary<Type, ServiceValidationData> _validationCache 
            = new Dictionary<Type, ServiceValidationData>();

        public static bool IsValidationCacheAvailable => _validationCache.Count > 0;

        public static ServiceValidationData GetValidationData(Type type)
        {
            return _validationCache.TryGetValue(type, out var data) ? data : null;
        }

        private static readonly List<Assembly> _assemblies = new();

        static ServiceTypeCacheBuilder()
        {
            if(ServiceEditorValidator.EnableLogging)
                Debug.Log("[ServiceLocator] Initializing ServiceTypeCacheBuilder");
            RefreshAssemblies();
            
            // Always unsubscribe first to prevent duplicate subscriptions
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            // Then subscribe
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnAfterAssemblyReload()
        {
            // Skip rebuild if just entering/exiting play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
                
            if (EditorPrefs.GetBool(AUTO_REBUILD_PREF, true) && !_isRebuilding)
            {
                if(ServiceEditorValidator.EnableLogging)
                    Debug.Log("[ServiceLocator] Scheduling type cache rebuild after assembly reload...");
                // Always unsubscribe before subscribing to prevent duplicates
                EditorApplication.delayCall -= RebuildTypeCache;
                EditorApplication.delayCall += RebuildTypeCache;
            }
        }

        [MenuItem("GAOS/Service Locator/Rebuild Type Cache")]
        public static void RebuildTypeCache()
        {
            RebuildTypeCache(false);
        }

        public static ServiceTypeCache RebuildTypeCache(bool isUnitTest, bool enableLogging = true)
        {
            if (_isRebuilding)
            {
                Debug.LogWarning("[ServiceLocator] Type cache rebuild already in progress, please wait...");
                return null;
            }

            ServiceLocator.Clear();

            _isUnitTest = isUnitTest;
            _isRebuilding = true;
            ServiceEditorValidator.EnableLogging = enableLogging; // Set logging control

            try
            {
                var typeCache = GetOrCreateRegistry();
                typeCache.Clear();

                if(ServiceEditorValidator.EnableLogging)
                    Debug.Log($"[ServiceLocator] Starting type cache rebuild. Current cache has {typeCache.ServiceTypes.Count} types");

                RefreshAssemblies();

                int typesProcessed = 0;
                int typesAdded = 0;

                var serviceTypes = FindServiceTypes();
                foreach (var type in serviceTypes)
                {
                    try
                    {
                        typesProcessed++;
                        var typeInfo = CreateTypeInfo(type);
                        if (typeInfo != null)
                        {
                            typeCache.AddTypeInfo(typeInfo);
                            typesAdded++;

                            if(ServiceEditorValidator.EnableLogging)
                                Debug.Log($"[ServiceLocator] Added service: {type.Name}\n" +
                                    $"- Interface: {typeInfo.InterfaceTypeName}\n" +
                                    $"- Type: {typeInfo.ServiceType}\n" +
                                    $"- Lifetime: {typeInfo.DefaultLifetime}\n" +
                                    $"- Context: {typeInfo.DefaultContext}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ServiceLocator] Error processing type {type.Name}: {ex}");
                    }
                }

                // Validate and cache all service types
                ValidateAndCacheServiceTypes(typeCache);

                EditorUtility.SetDirty(typeCache);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if(ServiceEditorValidator.EnableLogging)
                    Debug.Log($"[ServiceLocator] Type cache rebuild completed:\n" +
                        $"- Types processed: {typesProcessed}\n" +
                        $"- Types added: {typesAdded}\n" +
                        $"- Final cache count: {typeCache.ServiceTypes.Count}\n" +
                        $"- Cache saved at: {REGISTRY_PATH}");

                // Notify listeners that cache has been rebuilt
                OnTypeRegistryRebuilt?.Invoke();
                return typeCache;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServiceLocator] Error rebuilding type cache: {ex}");
                return null;
            }
            finally
            {
                _isRebuilding = false;
                _isUnitTest = false;
                ServiceEditorValidator.EnableLogging = true; // Reset logging control
            }
        }

        private static void RefreshAssemblies()
        {
            _assemblies.Clear();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var assemblyName = assembly.GetName().Name;

                    // Skip Unity engine assemblies
                    if (assemblyName.StartsWith("Unity"))
                        continue;

                    // Skip system assemblies
                    if (assemblyName.StartsWith("System"))
                        continue;

                    // Skip Microsoft assemblies
                    if (assemblyName.StartsWith("Microsoft"))
                        continue;

                    // Skip mscorlib
                    if (assemblyName == "mscorlib")
                        continue;

                    // Get a type from the assembly to determine its type
                    var sampleType = assembly.GetTypes().FirstOrDefault() ?? typeof(object);
                    var assemblyType = ServiceEditorValidator.DetermineAssemblyType(sampleType);

                    // If in unit test mode, only include test assemblies
                    if (_isUnitTest)
                    {
                        if (assemblyType == ServiceAssemblyType.Test)
                        {
                            _assemblies.Add(assembly);
                            if(ServiceEditorValidator.EnableLogging)
                                Debug.Log($"[ServiceLocator] Including test assembly: {assemblyName}");
                        }
                        continue;
                    }

                    // In normal mode, skip test assemblies
                    if (assemblyType == ServiceAssemblyType.Test)
                    {
                        if(ServiceEditorValidator.EnableLogging)
                            Debug.Log($"[ServiceLocator] Excluding test assembly from initial scan: {assemblyName}");
                        continue;
                    }

                    _assemblies.Add(assembly);
                }
                catch (Exception)
                {
                    // Skip assemblies that can't be loaded
                }
            }
        }

        private static IEnumerable<Type> FindServiceTypes()
        {
            var serviceTypes = new List<Type>();

            foreach (var assembly in _assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null);

                    serviceTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServiceLocator] Error scanning assembly {assembly.GetName().Name}: {ex}");
                }
            }

            return serviceTypes;
        }

        private static ServiceTypeInfo CreateTypeInfo(Type type)
        {
            var attribute = type.GetCustomAttribute<ServiceAttribute>();
            if (attribute == null) return null;

            // Validate service type
            if (attribute.ServiceInterface != null && !attribute.ServiceInterface.IsAssignableFrom(type))
            {
                Debug.LogError($"[ServiceLocator] Service {type.Name} does not implement interface {attribute.ServiceInterface.Name}");
                return null;
            }

            // Determine service type
            ServiceType serviceType;
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                serviceType = ServiceType.MonoBehaviour;
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                serviceType = ServiceType.ScriptableObject;
            }
            else
            {
                serviceType = ServiceType.Regular;
            }

            // Create type info with assembly type determined at editor time
            var assemblyType = ServiceEditorValidator.DetermineAssemblyType(type);
            var hasDefaultConstructor = type.GetConstructor(Type.EmptyTypes) != null;

            // Create type info with additional dependencies from attribute
            return new ServiceTypeInfo(
                attribute.ServiceInterface,
                type,
                serviceType,
                attribute.Lifetime,
                attribute.Context,
                attribute.Name,
                hasDefaultConstructor,
                assemblyType,
                attribute.AdditionalDependencies
            );
        }

        private static ServiceTypeCache GetOrCreateRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ServiceTypeCache>(REGISTRY_PATH);
            if (registry != null) return registry;

            // Create new registry
            registry = ScriptableObject.CreateInstance<ServiceTypeCache>();
            
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(REGISTRY_PATH);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
            AssetDatabase.SaveAssets();
            return registry;
        }

        [MenuItem("GAOS/Service Locator/Toggle Auto-Rebuild")]
        public static void ToggleAutoRebuild()
        {
            bool current = EditorPrefs.GetBool(AUTO_REBUILD_PREF, true);
            EditorPrefs.SetBool(AUTO_REBUILD_PREF, !current);
            if(ServiceEditorValidator.EnableLogging)
                Debug.Log($"[ServiceLocator] Auto-rebuild is now {(!current ? "enabled" : "disabled")}");
        }

        [MenuItem("GAOS/Service Locator/Toggle Auto-Rebuild", true)]
        public static bool ValidateToggleAutoRebuild()
        {
            Menu.SetChecked("GAOS/Service Locator/Toggle Auto-Rebuild", 
                EditorPrefs.GetBool(AUTO_REBUILD_PREF, true));
            return true;
        }

        private static void ValidateAndCacheServiceTypes(ServiceTypeCache typeCache)
        {
            _validationCache.Clear();

            // First pass: Build dependency trees and detect circular dependencies
            foreach (var serviceInfo in typeCache.ServiceTypes)
            {
                if (serviceInfo?.ImplementationType == null) continue;

                var validationData = new ServiceValidationData
                {
                    ValidationMessages = new List<(string, UnityEditor.MessageType)>()
                };

                // Check if service itself is async
                bool isAsyncService = typeof(IServiceInitializable).IsAssignableFrom(serviceInfo.ImplementationType);
                serviceInfo.IsAsyncService = isAsyncService;

                if (isAsyncService)
                {
                    validationData.ValidationMessages.Add((
                        $"Service {serviceInfo.ImplementationType.Name} implements IServiceInitializable",
                        UnityEditor.MessageType.Info
                    ));

                    if(ServiceEditorValidator.EnableLogging)
                        Debug.Log($"Service {serviceInfo.ImplementationType.Name} implements IServiceInitializable");
                }

                // Check for circular and async dependencies
                var result = ServiceEditorValidator.ValidateDependencies(
                    serviceInfo.ImplementationType, typeCache);

                serviceInfo.HasAsyncDependency = result.hasAsyncDependency;

                // Add validation message for async dependency without IServiceInitializable
                // Only add warning if service is not async itself but has async dependencies
                if (result.hasAsyncDependency && !isAsyncService)
                {
                    var message = $"Service {serviceInfo.ImplementationType.Name} has async dependencies but does not implement IServiceInitializable.\n" +
                                $"All services with async dependencies must implement IServiceInitializable.\n" +
                                $"Dependency chain: {result.dependencyChain}";
                    
                    validationData.ValidationMessages.Add((message, UnityEditor.MessageType.Error));
                }

                validationData.HasCircularDependency = result.hasCircular;
                validationData.IsImplementationDependency = result.isImplementationDependency;
                validationData.DependencyChain = result.dependencyChain;

                if (result.hasCircular)
                {
                    var message = result.isImplementationDependency ?
                        $"Implementation circular dependency detected.\n" +
                        $"Dependency chain: {result.dependencyChain}\n" +
                        $"ERROR: Service depends on concrete implementations which creates a strong circular dependency.\n" +
                        $"This is not allowed and must be refactored to use interfaces instead." :
                        $"Interface circular dependency detected.\n" +
                        $"Dependency chain: {result.dependencyChain}\n" +
                        $"WARNING: Ensure these services don't use each other during initialization.\n" +
                        $"If this is intended, you can suppress the runtime exception, but make sure to:\n" +
                        $"1. Don't use dependencies during initialization\n" +
                        $"2. Implement proper disposal to break circular references\n" +
                        $"Consider using events/messaging for better decoupling.";

                    validationData.ValidationMessages.Add((
                        message,
                        result.isImplementationDependency ? UnityEditor.MessageType.Error : UnityEditor.MessageType.Warning
                    ));
                }

                // Build dependency tree
                validationData.DependencyTree = ServiceEditorValidator.GetDependencyTree(
                    serviceInfo.ImplementationType, typeCache, includeInterfaceImplementations: true);

                _validationCache[serviceInfo.ImplementationType] = validationData;
            }

        }
    }
} 