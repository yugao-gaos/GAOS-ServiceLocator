using UnityEngine;
using System;
using System.Linq;
using GAOS.ServiceLocator.Optional;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Stores pre-computed type information for services to avoid runtime reflection
    /// </summary>
    [Serializable]
    public class ServiceTypeInfo
    {
        [SerializeField] private string _interfaceTypeName;
        [SerializeField] private string _implementationTypeName;
        [SerializeField] private string _interfaceTypeAssemblyQualifiedName;
        [SerializeField] private string _implementationTypeAssemblyQualifiedName;
        [SerializeField] private ServiceType _serviceType;
        [SerializeField] private bool _hasDefaultConstructor;
        [SerializeField] private ServiceLifetime _defaultLifetime;
        [SerializeField] private ServiceContext _defaultContext;
        [SerializeField] private string _defaultName;
        [SerializeField] private ServiceAssemblyType _assemblyType;
        [SerializeField] private string[] _additionalDependencyTypeNames;
        [SerializeField] private string[] _additionalDependencyAssemblyQualifiedNames;
        [SerializeField] private bool _isAsyncService;
        [SerializeField] private bool _hasAsyncDependency;

        // Runtime cached types
        [NonSerialized]
        private Type _interfaceType;
        [NonSerialized]
        private Type _implementationType;
        [NonSerialized]
        private Type[] _additionalDependencyTypes;

        public string InterfaceTypeName { get => _interfaceTypeName; set => _interfaceTypeName = value; }
        public string ImplementationTypeName { get => _implementationTypeName; set => _implementationTypeName = value; }
        public string InterfaceTypeAssemblyQualifiedName { get => _interfaceTypeAssemblyQualifiedName; set => _interfaceTypeAssemblyQualifiedName = value; }
        public string ImplementationTypeAssemblyQualifiedName { get => _implementationTypeAssemblyQualifiedName; set => _implementationTypeAssemblyQualifiedName = value; }
        public ServiceType ServiceType { get => _serviceType; set => _serviceType = value; }
        public bool HasDefaultConstructor { get => _hasDefaultConstructor; set => _hasDefaultConstructor = value; }
        public ServiceLifetime DefaultLifetime { get => _defaultLifetime; set => _defaultLifetime = value; }
        public ServiceContext DefaultContext { get => _defaultContext; set => _defaultContext = value; }
        public string DefaultName { get => _defaultName; set => _defaultName = value; }
        public ServiceAssemblyType AssemblyType { get => _assemblyType; set => _assemblyType = value; }
        public bool IsAsyncService { get => _isAsyncService; set => _isAsyncService = value; }
        public bool HasAsyncDependency { get => _hasAsyncDependency; set => _hasAsyncDependency = value; }

        public Type InterfaceType => _interfaceType ??= Type.GetType(InterfaceTypeAssemblyQualifiedName);
        public Type ImplementationType => _implementationType ??= Type.GetType(ImplementationTypeAssemblyQualifiedName);
        public Type[] AdditionalDependencyTypes
        {
            get
            {
                if (_additionalDependencyTypes == null && _additionalDependencyAssemblyQualifiedNames != null)
                {
                    _additionalDependencyTypes = _additionalDependencyAssemblyQualifiedNames
                        .Select(name => Type.GetType(name))
                        .Where(t => t != null)
                        .ToArray();
                }
                return _additionalDependencyTypes ?? Array.Empty<Type>();
            }
        }

        private ServiceTypeInfo()
        {
            // For serialization
        }

        public ServiceTypeInfo(
            Type interfaceType,
            Type implementationType,
            ServiceType serviceType,
            ServiceLifetime defaultLifetime,
            ServiceContext defaultContext,
            string defaultName,
            bool hasDefaultConstructor,
            ServiceAssemblyType assemblyType,
            Type[] additionalDependencies = null)
        {
            _interfaceType = interfaceType;
            _implementationType = implementationType;
            _interfaceTypeName = interfaceType.Name;
            _implementationTypeName = implementationType.Name;
            _interfaceTypeAssemblyQualifiedName = interfaceType.AssemblyQualifiedName;
            _implementationTypeAssemblyQualifiedName = implementationType.AssemblyQualifiedName;
            _serviceType = serviceType;
            _defaultLifetime = defaultLifetime;
            _defaultContext = defaultContext;
            _defaultName = defaultName;
            _hasDefaultConstructor = hasDefaultConstructor;
            _assemblyType = assemblyType;
            _isAsyncService = typeof(IServiceInitializable).IsAssignableFrom(implementationType);

            if (additionalDependencies != null && additionalDependencies.Length > 0)
            {
                _additionalDependencyTypes = additionalDependencies;
                _additionalDependencyTypeNames = additionalDependencies.Select(t => t.Name).ToArray();
                _additionalDependencyAssemblyQualifiedNames = additionalDependencies.Select(t => t.AssemblyQualifiedName).ToArray();
            }
        }

        public static ServiceTypeInfo CreateInstance(
            Type interfaceType,
            Type implementationType,
            ServiceLifetime defaultLifetime,
            ServiceContext defaultContext,
            string defaultName,
            ServiceAssemblyType assemblyType = ServiceAssemblyType.Runtime,
            Type[] additionalDependencies = null
            )
        {
            var serviceType = DetermineServiceType(implementationType);
            var hasDefaultConstructor = HasDefaultConstructorMethod(implementationType);

            return new ServiceTypeInfo(
                interfaceType,
                implementationType,
                serviceType,
                defaultLifetime,
                defaultContext,
                defaultName,
                hasDefaultConstructor,
                assemblyType,
                additionalDependencies
            );
        }

        private static bool HasDefaultConstructorMethod(Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static ServiceType DetermineServiceType(Type type)
        {
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
                return ServiceType.MonoBehaviour;
            else if (typeof(ScriptableObject).IsAssignableFrom(type))
                return ServiceType.ScriptableObject;
            else
                return ServiceType.Regular;
        }
    }
} 