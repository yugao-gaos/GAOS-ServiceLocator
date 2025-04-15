using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GAOS.ServiceLocator.Optional;

namespace GAOS.ServiceLocator.Diagnostics
{
    internal static class ServiceValidator
    {
        // MonoBehaviour Validation
        public static bool ValidateMonoBehaviourInstance(MonoBehaviour instance)
        {
            if (instance == null) return false;
            
            // Check if the GameObject is active and in a valid scene
            if (!instance.gameObject.activeInHierarchy) return false;
            
            // If the instance is in DontDestroyOnLoad scene, it's valid
            if (instance.gameObject.scene.name == "DontDestroyOnLoad") return true;
            
            // For regular scenes, check if the scene is still loaded
            return instance.gameObject.scene.isLoaded;
        }

        // Registration Validation
        internal static void ValidateServiceRegistration(ServiceTypeInfo typeInfo, ServiceContext context)
        {
            if (ServiceLocator.SkipEditorContextValidation)
                return;

            // Test assemblies can use any context
            if (typeInfo.AssemblyType == ServiceAssemblyType.Test)
                return;

            // Editor assemblies must use EditorOnly context
            if (typeInfo.AssemblyType == ServiceAssemblyType.Editor && context != ServiceContext.EditorOnly)
            {
                ServiceDiagnostics.NotifyValidationError(
                    typeInfo.ImplementationType,
                    "Editor Context",
                    "Service is in an editor assembly but is not marked as EditorOnly"
                );
                throw new ServiceValidationException("Service is in an editor assembly but is not marked as EditorOnly");
            }
            // Runtime assemblies cannot use EditorOnly context
            else if (typeInfo.AssemblyType == ServiceAssemblyType.Runtime && context == ServiceContext.EditorOnly)
            {
                ServiceDiagnostics.NotifyValidationError(
                    typeInfo.ImplementationType,
                    "Editor Context",
                    "Service is marked as EditorOnly but is not in an editor assembly"
                );
                throw new ServiceValidationException("Service is marked as EditorOnly but is not in an editor assembly");
            }
        }

        // Usage Validation
        internal static void ValidateServiceUsage(ServiceRegistration registration, string serviceName)
        {
            // Skip all context validation in tests
            if (ServiceLocator.SkipEditorContextValidation)
                return;

            switch (registration.Context)
            {
                case ServiceContext.Runtime:
                    if (!Application.isPlaying)
                    {
                        ServiceDiagnostics.NotifyValidationError(
                            registration.ImplementationType,
                            "Runtime Context",
                            $"Service '{serviceName}' is Runtime-only and can only be accessed during play mode"
                        );
                        throw new ServiceContextException($"Service '{serviceName}' is Runtime-only and can only be accessed during play mode");
                    }
                    break;

                case ServiceContext.EditorOnly:
                    #if UNITY_EDITOR
                    // Allow access in editor
                    #else
                    ServiceDiagnostics.NotifyValidationError(
                        registration.ImplementationType,
                        "Editor Context",
                        $"Service '{serviceName}' is Editor-only and cannot be accessed in builds"
                    );
                    throw new ServiceContextException($"Service '{serviceName}' is Editor-only and cannot be accessed in builds");
                    #endif
                    break;

                case ServiceContext.RuntimeAndEditor:
                    // No restrictions
                    break;
            }
        }

        // Cache Validation
        internal static void ValidateServiceCache(ServiceRegistration registration)
        {
            if (registration.Lifetime == ServiceLifetime.Transient) return;
            if (registration.ServiceType == ServiceType.Regular) return;

            switch (registration.ServiceType)
            {
                case ServiceType.MonoBehaviour:
                    var monoBehaviour = registration.Instance as MonoBehaviour;
                    if (!ServiceValidator.ValidateMonoBehaviourInstance(monoBehaviour))
                    {
                        ServiceDiagnostics.NotifyValidationError(
                            registration.ImplementationType,
                            "Cache Validation",
                            $"MonoBehaviour service instance '{registration.Name}' is no longer valid"
                        );
                        throw new ServiceValidationException($"MonoBehaviour service instance '{registration.Name}' is no longer valid");
                    }
                    break;

                case ServiceType.ScriptableObject:
                    var scriptableObject = registration.Instance as ScriptableObject;
                    if (scriptableObject == null)
                    {
                        ServiceDiagnostics.NotifyValidationError(
                            registration.ImplementationType,
                            "Cache Validation",
                            $"ScriptableObject service instance '{registration.Name}' is no longer valid"
                        );
                        throw new ServiceValidationException($"ScriptableObject service instance '{registration.Name}' is no longer valid");
                    }
                    break;
            }
        }

        // Dependency Validation
        internal static void ValidateCircularDependency(Type serviceType, HashSet<Type> initializingChain)
        {
            if (initializingChain.Contains(serviceType))
            {
                var chain = string.Join(" → ", 
                    initializingChain.Select(t => t.Name)
                    .Concat(new[] { serviceType.Name }));
                
                var message = $"Circular dependency detected in service chain: {chain}\n\n" +
                            "To debug this issue:\n" +
                            "1. Check the Type Cache Inspector (Window > GAOS > Service Type Inspector)\n" +
                            "2. If no circular dependencies are shown in the inspector, the cycle is likely\n" +
                            "   created by ServiceLocator.Get calls in service methods.\n" +
                            "3. Review the constructors, fields, properties, and ServiceLocator.Get calls\n" +
                            "   in the services listed above.";

                ServiceDiagnostics.NotifyValidationError(
                    serviceType,
                    "Circular Dependency",
                    message
                );
                throw new CircularDependencyException(message);
            }
        }

        internal static void ValidateDisposableService(object instance)
        {
            // No validation needed anymore since IServiceDisposable implementations 
            // are responsible for managing their own state
            // This method is kept for backward compatibility
        }

        // Basic Validation
        public static void ValidateServiceName(string name, string paramName = "name")
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Service name cannot be null or empty", paramName);
            }
        }

        internal static void ValidateServiceInstance(object instance, Type implementationType, string name)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!implementationType.IsInstanceOfType(instance))
            {
                throw new InvalidOperationException(
                    $"Instance type {instance.GetType().Name} does not match the registered type {implementationType.Name}"
                );
            }
        }

        internal static void ValidateServiceTypeInfo(ServiceTypeInfo typeInfo, ServiceType expectedType, string typeName)
        {
            if (typeInfo == null)
                throw new ArgumentNullException(nameof(typeInfo));

            if (typeInfo.ServiceType != expectedType)
            {
                throw new ArgumentException(
                    $"Type {typeName} must be a {expectedType} service. Current type: {typeInfo.ServiceType}"
                );
            }
        }

        internal static void ValidateInterfaceImplementation(Type serviceType, Type implementationType)
        {
            if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException(
                    $"Type {implementationType.Name} must implement interface {serviceType.Name}"
                );
            }
        }

    
    }
} 