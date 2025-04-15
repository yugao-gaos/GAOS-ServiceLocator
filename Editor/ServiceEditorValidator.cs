using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using GAOS.ServiceLocator.Optional;
namespace GAOS.ServiceLocator.Editor
{
    [InitializeOnLoad]
    public static class ServiceEditorValidator
    {
        
        public static bool EnableLogging { get; set; } = true;
        static ServiceEditorValidator()
        {
            // Always unsubscribe first to prevent duplicate handlers
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Then subscribe
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // Clear non-editor services before entering play mode
                    ServiceLocator.ClearNonEditorServices();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    // Clear non-editor services when exiting play mode
                    ServiceLocator.ClearNonEditorServices();
                    break;
            }
        }

        /// <summary>
        /// Checks if an assembly is a test assembly based on its name
        /// </summary>
        private static bool IsTestAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;
            
            return assemblyName.EndsWith("Tests") ||
                   assemblyName.EndsWith("Tests.Runtime") ||
                   assemblyName.EndsWith("Tests.Editor") ||
                   assemblyName.Contains(".TestUtils.") ||
                   assemblyName.Contains(".TestTools.");
        }

        /// <summary>
        /// Determines the assembly type of a given type during editor time validation
        /// </summary>
        internal static ServiceAssemblyType DetermineAssemblyType(Type type)
        {
            var assembly = type.Assembly;
            var assemblyName = assembly.GetName().Name;

            // Check for test assembly first (overrides editor)
            if (IsTestAssembly(assemblyName))
            {
                return ServiceAssemblyType.Test;
            }

            // Then check for editor assembly
            if (assemblyName.Contains("Editor") || 
                assembly == typeof(UnityEditor.Editor).Assembly ||
                assembly.GetReferencedAssemblies().Any(a => a.Name == "UnityEditor" || a.Name == "UnityEditor.CoreModule"))
            {
                return ServiceAssemblyType.Editor;
            }

            // Default to runtime
            return ServiceAssemblyType.Runtime;
        }

        /// <summary>
        /// Performs static analysis to detect circular dependencies between services
        /// </summary>
        /// <param name="serviceType">The service type to check</param>
        /// <param name="typeCache">The service type cache containing all service information</param>
        /// <returns>A tuple containing whether circular dependencies were found and the dependency chain if any</returns>
        public static (bool hasCircular, string dependencyChain, bool isImplementationDependency, bool hasAsyncDependency) ValidateDependencies(Type serviceType, ServiceTypeCache typeCache)
        {
            var visited = new HashSet<Type>();  // Types fully processed in current path
            var path = new List<Type>();        // Current dependency path for display
            var inPath = new HashSet<Type>();   // Current dependency path for cycle detection
            var processedInterfacesInPath = new HashSet<Type>();  // Track processed interfaces in current path only
            bool foundCircular = false;
            bool isImplementationDependency = false;
            bool foundImplementationCircular = false; 
            bool hasAsyncDependency = false; // Track if we found an implementation circular dependency
            string capturedChain = null;  // Store the complete chain when first detected

            string BuildDependencyChainString()
            {
                return string.Join(" → ", path.Select(t => {
                    var asImpl = typeCache.ServiceTypes.FirstOrDefault(s => s.ImplementationType == t);
                    var asInterface = typeCache.ServiceTypes.FirstOrDefault(s => s.InterfaceType == t);
                    string name;
                    
                    if (asImpl != null)
                    {
                        name = $"{asImpl.InterfaceType.Name} ({t.Name})";
                    }
                    else if (asInterface != null)
                    {
                        name = t.Name;
                    }
                    else
                    {
                        name = t.Name;
                    }

                    // Add [Async] tag if type is async
                    if (typeof(IServiceInitializable).IsAssignableFrom(t))
                    {
                        name = $"{name} [Async]";
                    }
                    
                    return name;
                }));
            }

            void TravoseDependencies(Type currentType, bool isImplementationOfInterface = false)
            {
                // If we've seen this type in current path, we found a cycle
                if (inPath.Contains(currentType))
                {
                    // Add the current type to complete the cycle in display
                    path.Add(currentType);
                    foundCircular = true;

                    // Capture the complete chain when we first detect the cycle
                    capturedChain = BuildDependencyChainString();

                    // Check if any type in the cycle is referenced by concrete implementation
                    var pathArray = path.ToArray();
                    for (int i = 0; i < pathArray.Length - 1; i++)
                    {
                        var current = pathArray[i];
                        var next = pathArray[i + 1];
                        
                        var currentDeps = GetDirectDependencies(current, typeCache);
                        if (currentDeps.Any(d => !d.IsInterface && d == next))
                        {
                            isImplementationDependency = true;
                            foundImplementationCircular = true;
                            break;
                        }
                    }
                    return;
                }

                // If we've already fully processed this type in a non-cyclic path, skip
                if (visited.Contains(currentType) && !inPath.Contains(currentType))
                {
                    return;
                }

                try
                {
                    // Add to current path and tracking sets
                    inPath.Add(currentType);
                    path.Add(currentType);

                    // Get dependencies
                    var dependencies = GetDirectDependencies(currentType, typeCache);
                    
                    if(hasAsyncDependency == false && typeof(IServiceInitializable).IsAssignableFrom(currentType))
                    {
                        hasAsyncDependency = true;
                        if(EnableLogging)
                        {
                            capturedChain = BuildDependencyChainString();
                            Debug.Log($"Found async dependency: {currentType.Name} depenenencychain: {capturedChain}");
                        }
                    }

                    // Process each dependency
                    foreach (var dependency in dependencies)
                    {
                        // If we found an implementation circular dependency, we can stop
                        if (foundImplementationCircular) return;

                        if (dependency.IsInterface)
                        {
                            // If we've seen this interface in the current path, it's a circular dependency
                            if (processedInterfacesInPath.Contains(dependency))
                            {
                                // Add the interface to complete the cycle
                                path.Add(dependency);
                                foundCircular = true;

                                // Capture the chain
                                capturedChain = BuildDependencyChainString();

                                // Remove the interface we just added for display
                                path.RemoveAt(path.Count - 1);
                                continue;
                            }

                            processedInterfacesInPath.Add(dependency);

                            // Get all implementations of this interface
                            var implementations = typeCache.ServiceTypes
                                .Where(t => t.InterfaceType == dependency)
                                .Select(t => t.ImplementationType)
                                .ToList();

                            // Check each implementation
                            foreach (var implType in implementations)
                            {
                                if (foundImplementationCircular) return;
                                TravoseDependencies(implType, true);
                            }

                            processedInterfacesInPath.Remove(dependency);
                        }
                        else
                        {
                            TravoseDependencies(dependency, isImplementationOfInterface);
                        }
                    }

                    // Mark as fully processed for this path
                    visited.Add(currentType);
                }
                finally
                {
                    // Always clean up the current path when backtracking
                    if (!foundCircular || (foundCircular && !isImplementationDependency))
                    {
                        inPath.Remove(currentType);
                        path.RemoveAt(path.Count - 1);
                    }

                    if(EnableLogging)
                    {
                        if(hasAsyncDependency && !typeof(IServiceInitializable).IsAssignableFrom(currentType))
                        {
                            var message = $"Service {currentType.Name} has async dependencies but does not implement IServiceInitializable. All services with async dependencies must implement IServiceInitializable.";
                            Debug.LogError(message);
                        }
                    }
                }
            }

            // Start with the service type
            if (serviceType.IsInterface)
            {
                processedInterfacesInPath.Add(serviceType);
                var implementations = typeCache.ServiceTypes
                    .Where(t => t.InterfaceType == serviceType)
                    .Select(t => t.ImplementationType)
                    .ToList();

                foreach (var impl in implementations)
                {
                    if (foundImplementationCircular) break;
                    TravoseDependencies(impl, true);
                }
                processedInterfacesInPath.Remove(serviceType);
            }
            else
            {
                TravoseDependencies(serviceType);
            }
            
            // Format the dependency chain for display
            string chain = capturedChain ?? "";  // Use the captured chain if available
            
            if (foundCircular && EnableLogging)
            {
                if (isImplementationDependency)
                {
                    Debug.LogError($"Implementation circular dependency detected: {chain}");
                }
                else
                {
                    Debug.LogWarning($"Interface Circular dependency detected: {chain}");
                }
            }
            
            return (foundCircular, chain, isImplementationDependency, hasAsyncDependency);
        }

        
        /// <summary>
        /// Gets the immediate dependencies of a type through static analysis.
        /// This includes constructor parameters, fields, and properties.
        /// For interfaces, it includes all implementing types.
        /// </summary>
        private static HashSet<Type> GetDirectDependencies(Type type, ServiceTypeCache typeCache)
        {
            var dependencies = new HashSet<Type>();

            // Check if this type is an interface
            if (type.IsInterface)
            {
                // Get and add all implementations of this interface
                var implementations = typeCache.ServiceTypes
                    .Where(t => t.InterfaceType == type)
                    .Select(t => t.ImplementationType);
                    
                foreach (var impl in implementations)
                {
                    dependencies.Add(impl);
                }
            }

            // Get type info to check for additional dependencies
            var typeInfo = typeCache.GetTypeInfo(type);
            if (typeInfo != null)
            {
                foreach (var additionalDep in typeInfo.AdditionalDependencyTypes)
                {
                    if (ShouldConsiderType(additionalDep))
                    {
                        dependencies.Add(additionalDep);
                    }
                }
            }

            // Check constructor dependencies
            var constructors = type.GetConstructors();
            foreach (var constructor in constructors)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (ShouldConsiderType(parameter.ParameterType))
                    {
                        dependencies.Add(parameter.ParameterType);
                    }
                }
            }

            // Check field dependencies
            var allFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                            BindingFlags.Static | BindingFlags.GetField | BindingFlags.FlattenHierarchy;
                            
            var fields = type.GetFields(allFlags);
            foreach (var field in fields)
            {
                if (ShouldConsiderType(field.FieldType))
                {
                    dependencies.Add(field.FieldType);
                }
            }

            // Check property dependencies
            var properties = type.GetProperties(allFlags);
            foreach (var property in properties)
            {
                if (ShouldConsiderType(property.PropertyType))
                {
                    dependencies.Add(property.PropertyType);
                }
            }

            return dependencies;
        }

        private static bool ShouldConsiderType(Type type)
        {
            if (type == null) return false;
            
            // Skip primitive and basic system types
            if (type.IsPrimitive) return false;
            if (type == typeof(string)) return false;
            if (type == typeof(decimal)) return false;
            if (type == typeof(DateTime)) return false;
            if (type == typeof(TimeSpan)) return false;
            if (type == typeof(CancellationToken)) return false;
            if (type == typeof(object)) return false;
            if (type == typeof(ValueType)) return false;
            if (type.IsEnum) return false;
            
            // Handle arrays of primitives or strings
            if (type.IsArray)
            {
                return ShouldConsiderType(type.GetElementType());
            }

            // Get the assembly name
            var assemblyName = type.Assembly.GetName().Name;

            // Skip system assemblies
            if (assemblyName.StartsWith("System.")) return false;
            if (assemblyName.StartsWith("Microsoft.")) return false;
            if (assemblyName == "mscorlib") return false;
            if (assemblyName == "netstandard") return false;

            // Skip Unity types
            if (assemblyName.StartsWith("UnityEngine.")) return false;
            if (assemblyName.StartsWith("UnityEditor.")) return false;
            if (assemblyName == "Unity") return false;

            // Skip types from Package Cache (except our own package)
            var assemblyLocation = type.Assembly.Location.Replace('\\', '/');
            if (assemblyLocation.Contains("/PackageCache/") && 
                !assemblyLocation.Contains("com.gaos.servicelocator")) return false;

            return true;
        }

        /// <summary>
        /// Gets the complete dependency tree for a type through static analysis.
        /// This recursively traverses all dependencies to build a complete tree.
        /// </summary>
        public static ServiceTypeCacheBuilder.DependencyNode GetDependencyTree(
            Type type, 
            ServiceTypeCache typeCache, 
            HashSet<Type> visitedTypes = null,
            bool includeInterfaceImplementations = true)
        {
            visitedTypes ??= new HashSet<Type>();

            // If we've seen this type before, return null to break recursion
            if (!visitedTypes.Add(type))
            {
                return null;
            }

            try
            {
                var node = new ServiceTypeCacheBuilder.DependencyNode
                {
                    Type = type,
                    IsInterface = type.IsInterface
                };

                // Get direct dependencies
                var directDeps = GetDirectDependencies(type, typeCache);
                foreach (var dep in directDeps)
                {
                    // Skip interface-implementation relationships if not included
                    if (!includeInterfaceImplementations && typeCache.ServiceTypes.Any(t => 
                        t.InterfaceType == type && t.ImplementationType == dep))
                    {
                        continue;
                    }

                    var childNode = GetDependencyTree(dep, typeCache, visitedTypes, includeInterfaceImplementations);
                    if (childNode != null)
                    {
                        childNode.IsImplementation = typeCache.ServiceTypes.Any(t => 
                            t.InterfaceType == type && t.ImplementationType == dep);
                        node.Dependencies.Add(childNode);
                    }
                }

                return node;
            }
            finally
            {
                // Always clean up visited set when leaving this type
                visitedTypes.Remove(type);
            }
        }


      
    }
} 