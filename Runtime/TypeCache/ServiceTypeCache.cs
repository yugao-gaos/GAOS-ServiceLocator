using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Registry containing all pre-computed service type information
    /// </summary>
    [CreateAssetMenu(fileName = "ServiceTypeCache", menuName = "GAOS/Service Locator/Type Cache")]
    public class ServiceTypeCache : ScriptableObject
    {
        [SerializeField] private List<ServiceTypeInfo> _serviceTypes = new();
        private Dictionary<Type, ServiceTypeInfo> _typeInfoCache;

        /// <summary>
        /// Gets all registered service type information
        /// </summary>
        public IReadOnlyList<ServiceTypeInfo> ServiceTypes => _serviceTypes;

        public ServiceTypeInfo GetTypeInfo(Type type)
        {
            InitializeCache();
            return _typeInfoCache.TryGetValue(type, out var info) ? info : null;
        }

        private void InitializeCache()
        {
            if (_typeInfoCache != null) return;

            _typeInfoCache = new Dictionary<Type, ServiceTypeInfo>();
            foreach (var typeInfo in _serviceTypes)
            {
                try
                {
                    // Cache by both interface and implementation type
                    if (typeInfo.InterfaceType != null)
                    {
                        _typeInfoCache[typeInfo.InterfaceType] = typeInfo;
                    }
                    if (typeInfo.ImplementationType != null)
                    {
                        _typeInfoCache[typeInfo.ImplementationType] = typeInfo;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error caching type info for {typeInfo.InterfaceTypeName}: {ex}");
                }
            }
        }

#if UNITY_EDITOR
        public void AddTypeInfo(ServiceTypeInfo typeInfo)
        {
            if (typeInfo == null)
                throw new ArgumentNullException(nameof(typeInfo));

            // Check for duplicates
            var existingType = _serviceTypes.FirstOrDefault(t => t != null &&
                t.InterfaceTypeAssemblyQualifiedName == typeInfo.InterfaceTypeAssemblyQualifiedName &&
                t.ImplementationTypeAssemblyQualifiedName == typeInfo.ImplementationTypeAssemblyQualifiedName);

            if (existingType != null)
            {
                Debug.LogWarning($"Type info for {typeInfo.InterfaceTypeName} already exists in registry");
                return;
            }

            _serviceTypes.Add(typeInfo);
            _typeInfoCache = null; // Force cache rebuild
        }

        public void Clear()
        {
            _serviceTypes.Clear();
            _typeInfoCache = null;
        }

        public void AddServiceType(ServiceTypeInfo typeInfo)
        {
            if (typeInfo == null)
                throw new ArgumentNullException(nameof(typeInfo));

            // Check for duplicates
            var existingType = _serviceTypes.FirstOrDefault(t => t != null &&
                t.InterfaceTypeAssemblyQualifiedName == typeInfo.InterfaceTypeAssemblyQualifiedName &&
                t.ImplementationTypeAssemblyQualifiedName == typeInfo.ImplementationTypeAssemblyQualifiedName);

            if (existingType != null)
            {
                throw new ArgumentException($"Type info for {typeInfo.InterfaceTypeName} already exists in cache");
            }

            _serviceTypes.Add(typeInfo);
            _typeInfoCache = null; // Force cache rebuild
        }
#endif
    }
} 