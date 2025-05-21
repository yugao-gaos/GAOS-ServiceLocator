using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

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
        [SerializeField] private bool onlyReportProjectCircularDependencies = true;
        [SerializeField] private List<string> projectAssemblyPrefixes = new List<string> { "Assembly-CSharp", "GAOS." };
        [SerializeField] private List<string> projectPackagePrefixes = new List<string> { "com.gaos." };
        
        /// <summary>
        /// List of assemblies excluded from circular dependency validation
        /// </summary>
        public IReadOnlyList<string> ExcludedAssemblies => excludedAssemblies;
        
        /// <summary>
        /// List of specific types excluded from circular dependency validation (by full name)
        /// </summary>
        public IReadOnlyList<string> ExcludedTypeFullNames => excludedTypeFullNames;
        
        /// <summary>
        /// When enabled, circular dependencies are only reported if at least one type 
        /// belongs to a project assembly (not a third-party library)
        /// </summary>
        public bool OnlyReportProjectCircularDependencies 
        {
            get => onlyReportProjectCircularDependencies;
            set => onlyReportProjectCircularDependencies = value;
        }
        
        /// <summary>
        /// List of assembly name prefixes that identify project-owned assemblies
        /// </summary>
        public IReadOnlyList<string> ProjectAssemblyPrefixes => projectAssemblyPrefixes;
        
        /// <summary>
        /// List of package name prefixes that identify project-owned packages
        /// </summary>
        public IReadOnlyList<string> ProjectPackagePrefixes => projectPackagePrefixes;
        
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
        /// Determines whether a circular dependency between two types should be reported
        /// based on current settings
        /// </summary>
        /// <param name="typeA">First type in the dependency</param>
        /// <param name="typeB">Second type in the dependency</param>
        /// <returns>True if this circular dependency should be reported</returns>
        public bool ShouldReportCircularDependency(Type typeA, Type typeB)
        {
            // If we're not filtering by project assemblies, always report
            if (!onlyReportProjectCircularDependencies)
                return true;
                
            // Only report if at least one type is from a project assembly
            return IsProjectAssembly(typeA.Assembly) || IsProjectAssembly(typeB.Assembly);
        }
        
        /// <summary>
        /// Determines whether a circular dependency path should be reported
        /// based on current settings
        /// </summary>
        /// <param name="dependencyPath">List of types in the dependency path</param>
        /// <returns>True if this circular dependency should be reported</returns>
        public bool ShouldReportCircularDependency(IList<Type> dependencyPath)
        {
            // If we're not filtering by project assemblies, always report
            if (!onlyReportProjectCircularDependencies)
                return true;
                
            // Only report if at least one type in the path is from a project assembly
            return dependencyPath.Any(type => IsProjectAssembly(type.Assembly));
        }
        
        /// <summary>
        /// Determines if an assembly is part of the project (not a third-party library)
        /// </summary>
        /// <param name="assembly">The assembly to check</param>
        /// <returns>True if this is a project-owned assembly</returns>
        public bool IsProjectAssembly(Assembly assembly)
        {
            if (assembly == null) return false;
            
            string name = assembly.GetName().Name;
            
            // Check if the assembly name matches any of our project assembly prefixes
            foreach (var prefix in projectAssemblyPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            // Check if the assembly location contains any of our project package prefixes
            string location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                location = location.Replace('\\', '/');
                
                // Check if it's in a Packages/ folder
                int packagesIndex = location.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase);
                if (packagesIndex >= 0)
                {
                    // Get the package name
                    string packagePath = location.Substring(packagesIndex + 10); // +10 to skip "/Packages/"
                    foreach (var prefix in projectPackagePrefixes)
                    {
                        if (packagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            
            return false;
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
        /// Add a project assembly prefix to the list
        /// </summary>
        /// <param name="prefix">Assembly name prefix to add</param>
        /// <returns>True if the prefix was added</returns>
        public bool AddProjectAssemblyPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || projectAssemblyPrefixes.Contains(prefix))
                return false;
                
            projectAssemblyPrefixes.Add(prefix);
            return true;
        }
        
        /// <summary>
        /// Add a project package prefix to the list
        /// </summary>
        /// <param name="prefix">Package name prefix to add</param>
        /// <returns>True if the prefix was added</returns>
        public bool AddProjectPackagePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || projectPackagePrefixes.Contains(prefix))
                return false;
                
            projectPackagePrefixes.Add(prefix);
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