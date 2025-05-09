using UnityEngine;
using System;
using System.Collections.Generic;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Configuration for dependency validation rules
    /// </summary>
    [CreateAssetMenu(fileName = "DependencyValidationSettings", menuName = "GAOS/Service Locator/Dependency Validation Settings")]
    public class DependencyValidationSettings : ScriptableObject
    {
        [SerializeField] private List<string> excludedAssemblies = new List<string>();
        [SerializeField] private List<string> excludedTypeFullNames = new List<string>();
        
        /// <summary>
        /// List of assemblies excluded from circular dependency validation
        /// </summary>
        public IReadOnlyList<string> ExcludedAssemblies => excludedAssemblies;
        
        /// <summary>
        /// List of specific types excluded from circular dependency validation (by full name)
        /// </summary>
        public IReadOnlyList<string> ExcludedTypeFullNames => excludedTypeFullNames;
        
        /// <summary>
        /// Determines if a type should be validated for circular dependencies
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type should be validated, false if it should be excluded</returns>
        public bool ShouldValidateDependency(Type type)
        {
            if (type == null) return false;
            
            // Skip validation for types in excluded assemblies
            if (excludedAssemblies.Contains(type.Assembly.GetName().Name))
                return false;
                
            // Skip validation for specific excluded types
            if (excludedTypeFullNames.Contains(type.FullName))
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Add an assembly to the exclusion list
        /// </summary>
        /// <param name="assemblyName">Name of the assembly to exclude</param>
        /// <returns>True if the assembly was added, false if it was already in the list</returns>
        public bool AddExcludedAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || excludedAssemblies.Contains(assemblyName))
                return false;
                
            excludedAssemblies.Add(assemblyName);
            return true;
        }
        
        /// <summary>
        /// Add a type to the exclusion list
        /// </summary>
        /// <param name="type">The type to exclude</param>
        /// <returns>True if the type was added, false if it was already in the list</returns>
        public bool AddExcludedType(Type type)
        {
            if (type == null) return false;
            
            string fullName = type.FullName;
            if (excludedTypeFullNames.Contains(fullName))
                return false;
                
            excludedTypeFullNames.Add(fullName);
            return true;
        }
        
        /// <summary>
        /// Remove an assembly from the exclusion list
        /// </summary>
        /// <param name="assemblyName">Name of the assembly to remove</param>
        /// <returns>True if the assembly was removed</returns>
        public bool RemoveExcludedAssembly(string assemblyName)
        {
            return excludedAssemblies.Remove(assemblyName);
        }
        
        /// <summary>
        /// Remove a type from the exclusion list
        /// </summary>
        /// <param name="type">The type to remove</param>
        /// <returns>True if the type was removed</returns>
        public bool RemoveExcludedType(Type type)
        {
            if (type == null) return false;
            return excludedTypeFullNames.Remove(type.FullName);
        }
        
        /// <summary>
        /// Remove a type from the exclusion list by its full name
        /// </summary>
        /// <param name="typeFullName">Full name of the type to remove</param>
        /// <returns>True if the type was removed</returns>
        public bool RemoveExcludedTypeByName(string typeFullName)
        {
            return excludedTypeFullNames.Remove(typeFullName);
        }
    }
} 