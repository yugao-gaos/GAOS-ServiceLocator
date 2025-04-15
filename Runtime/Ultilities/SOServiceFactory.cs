using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using GAOS.ServiceLocator.Diagnostics;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Factory for creating ScriptableObject service instances without reflection
    /// </summary>
    internal static class SOServiceFactory
    {

        /// <summary>
        /// Finds instance of the service in resources
        /// </summary>
        internal static ScriptableObject FindResourceInstance(Type ImplementationType, string name)
        {

            var instances = Resources.LoadAll(string.Empty, ImplementationType)
                .Cast<ScriptableObject>()
                .ToList();

            if (instances.Count > 0)
            {
                if (instances.Count > 1)
                {
                    ServiceDiagnostics.NotifyMultipleServicesFound(
                        ImplementationType,
                        "Resources",
                        instances.Select(i => i.name).ToArray()
                    );
                }

                return instances[0];
            }

            return null;
        }


        /// <summary>
        /// Gets or creates a instance of the service
        /// </summary>
        internal static ScriptableObject CreateInstance(Type ImplementationType, string name)
        {
            var instance = ScriptableObject.CreateInstance(ImplementationType);
            instance.name = name;
            return instance;
        }
    }
} 