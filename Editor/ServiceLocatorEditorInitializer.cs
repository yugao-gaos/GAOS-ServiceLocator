using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GAOS.ServiceLocator.Editor
{
    /// <summary>
    /// Initializes the ServiceLocator in edit mode to support editor-only services
    /// </summary>
    [InitializeOnLoad]
    public static class ServiceLocatorEditorInitializer
    {
        // Use reflection to access ServiceLocator's TypeCache
        private static readonly PropertyInfo _typeCacheProperty = typeof(ServiceLocator).GetProperty("TypeCache", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly object _typeCache = _typeCacheProperty?.GetValue(null);
        private static readonly PropertyInfo _serviceTypesProperty = _typeCache?.GetType().GetProperty("ServiceTypes");
        private static readonly IEnumerable<object> _serviceTypes = _serviceTypesProperty?.GetValue(_typeCache) as IEnumerable<object>;
        
        static ServiceLocatorEditorInitializer()
        {
            // Ensure we register editor services when Unity loads
            EditorApplication.delayCall += RegisterEditorOnlyServices;
            
            // Also register after domain reload
            AssemblyReloadEvents.afterAssemblyReload += () => {
                EditorApplication.delayCall += RegisterEditorOnlyServices;
            };
            
            Debug.Log("ServiceLocatorEditorInitializer initialized");
        }
        
        private static void RegisterEditorOnlyServices()
        {
            if (_serviceTypes == null)
            {
                Debug.LogError("Could not access ServiceTypes via reflection");
                return;
            }
            
            Debug.Log("Registering editor-only services");
            int count = 0;
            
            // Get all types with the ServiceAttribute and EditorOnly context using reflection
            foreach (var infoObj in _serviceTypes)
            {
                if (infoObj == null) continue;
                
                // Extract properties via reflection
                Type infoType = infoObj.GetType();
                PropertyInfo contextProp = infoType.GetProperty("Context");
                PropertyInfo implTypeProp = infoType.GetProperty("ImplementationType");
                PropertyInfo interfaceTypeProp = infoType.GetProperty("InterfaceType");
                PropertyInfo nameProp = infoType.GetProperty("DefaultName");
                PropertyInfo lifetimeProp = infoType.GetProperty("Lifetime");
                
                // Get values
                object contextObj = contextProp?.GetValue(infoObj);
                Type implType = implTypeProp?.GetValue(infoObj) as Type;
                Type interfaceType = interfaceTypeProp?.GetValue(infoObj) as Type;
                string name = nameProp?.GetValue(infoObj) as string;
                object lifetimeObj = lifetimeProp?.GetValue(infoObj);
                
                // Check if it's an editor-only service
                if (contextObj == null || (ServiceContext)contextObj != ServiceContext.EditorOnly)
                    continue;
                
                if (implType == null || interfaceType == null || string.IsNullOrEmpty(name))
                {
                    Debug.LogWarning("Incomplete type info found");
                    continue;
                }

                try
                {
                    // Use the public API to check if the service is already registered
                    if (ServiceLocator.GetServiceNames(interfaceType).Contains(name))
                    {
                        Debug.Log($"Editor service already registered: {implType.Name} with name {name}");
                        continue;
                    }

                    // Use reflection to call the internal Register method
                    MethodInfo registerMethod = typeof(ServiceLocator).GetMethod("Register", 
                        BindingFlags.NonPublic | BindingFlags.Static,
                        null,
                        new[] { typeof(Type), typeof(Type), typeof(string), typeof(ServiceLifetime), typeof(ServiceContext) },
                        null);
                    
                    if (registerMethod != null)
                    {
                        registerMethod.Invoke(null, new object[] { 
                            interfaceType, 
                            implType, 
                            name, 
                            lifetimeObj, 
                            ServiceContext.EditorOnly 
                        });
                        
                        Debug.Log($"Registered editor service: {implType.Name} with name {name}");
                        count++;
                    }
                    else
                    {
                        Debug.LogError("Could not find Register method via reflection");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error registering editor service: {ex.Message}");
                }
            }
            
            Debug.Log($"Registered {count} editor-only services");
        }
    }
} 