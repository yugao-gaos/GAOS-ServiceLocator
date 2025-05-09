using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using GAOS.Logger;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Provides diagnostic events, logging, and error tracking for service lifecycle monitoring.
    /// Runtime cases are logged directly, editor cases are fired through events.
    /// </summary>
    public static class ServiceDiagnostics
    {
        #region Configuration
        private static bool _isDebugLoggingEnabled = false;
        public static void EnableDebugLogging(bool enable) => _isDebugLoggingEnabled = enable;
        #endregion

        #region General Logging
        internal static void LogInfo(string message, bool requireDebugMode = true)
        {
            if (requireDebugMode && !_isDebugLoggingEnabled) return;
            GLog.Info<ServiceLocatorLogSystem>(message);
        }

        internal static void LogWarning(string message) => 
            GLog.Warning<ServiceLocatorLogSystem>(message);

        internal static void LogError(string message) => 
            GLog.Error<ServiceLocatorLogSystem>(message);
        #endregion

        #region Service Lifecycle Logging
        internal static void LogServiceRegistration(Type serviceType, string name, ServiceLifetime lifetime)
        {
            if (!_isDebugLoggingEnabled) return;
            LogInfo($"Registering service: {serviceType.Name}, Name: {name}, Lifetime: {lifetime}");
        }

        internal static void LogServiceResolution(Type serviceType, string name, bool found)
        {
            if (!_isDebugLoggingEnabled) return;
            if (found)
                LogInfo($"Resolved service: {serviceType.Name}, Name: {name}");
            else
                LogInfo($"Failed to resolve service: {serviceType.Name}, Name: {name}");
        }

        internal static void LogServiceInstanceCreation(Type ImplementationType, string key, string location)
        {
            if (!_isDebugLoggingEnabled) return;
            LogInfo($"Created new instance of {ImplementationType.Name}\n" +
                   $"Key: {key}\n" +
                   $"Location: {location}");
        }

     

        
        #endregion

        #region Events (Editor Only)
        /// <summary>
        /// Fired when a service instance is created
        /// </summary>
        public static event Action<Type, string, string, string> OnServiceCreated;

        /// <summary>
        /// Fired when a service instance is not found
        /// </summary>
        public static event Action<Type, string, string, string> OnServiceNotFound;

        /// <summary>
        /// Fired when multiple service instances are found
        /// </summary>
        public static event Action<Type, string, string[], string> OnMultipleServicesFound;

        /// <summary>
        /// Fired when a service validation error occurs
        /// </summary>
        public static event Action<Type, string, string, string> OnValidationError;

        /// <summary>
        /// Fired when a service initialization error occurs
        /// </summary>
        public static event Action<Type, string, Exception, string> OnInitializationError;

        /// <summary>
        /// Fired when a circular dependency is detected
        /// </summary>
        public static event Action<Type, string[], string> OnCircularDependencyDetected;

        /// <summary>
        /// Fired when a runtime service is accessed in editor mode
        /// </summary>
        public static event Action<Type, string, string> OnRuntimeServiceAccessedInEditor;

        /// <summary>
        /// Fired when multiple service assets are found in the project
        /// </summary>
        public static event Action<Type, string[], string> OnMultipleServiceAssetsFound;

        /// <summary>
        /// Fired when an editor-only service is detected in build
        /// </summary>
        public static event Action<Type, string> OnEditorOnlyServiceInBuild;

        /// <summary>
        /// Fired when there's a service instance validation error
        /// </summary>
        public static event Action<Type, string, string> OnServiceInstanceValidationError;

        /// <summary>
        /// Fired when a service with async dependencies is accessed synchronously
        /// </summary>
        public static event Action<Type, string[], string> OnAsyncDependencyViolation;

        /// <summary>
        /// Fired when an async operation logs a message
        /// </summary>
        public static event Action<Type, string, string, string> OnAsyncOperationLog;

        /// <summary>
        /// Fired when an async operation encounters an error
        /// </summary>
        public static event Action<Type, string, Exception, string> OnAsyncOperationError;
        #endregion

        #region Runtime Notifications
        internal static void NotifyServiceCreated(Type type, string location, string details)
        {
            var message = ServiceDiagnosticsFormatter.FormatServiceCreated(type, location, details);
#if UNITY_EDITOR
            if (OnServiceCreated != null)
            {
                OnServiceCreated(type, location, details, message);
                return;
            }
#endif
            GLog.Info<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyServiceNotFound(Type type, string location, string details)
        {
            var message = details.Contains("Creating new GameObject") 
                ? ServiceDiagnosticsFormatter.FormatServiceCreated(type, location, details)
                : ServiceDiagnosticsFormatter.FormatServiceNotFound(type, location, details);
#if UNITY_EDITOR
            if (OnServiceNotFound != null)
            {
                OnServiceNotFound(type, location, details, message);
                return;
            }
#endif
            if (details.Contains("Creating new GameObject"))
            {
                GLog.Info<ServiceLocatorLogSystem>(message);
            }
            else
            {
                GLog.Error<ServiceLocatorLogSystem>(message);
            }
        }

        internal static void NotifyMultipleServicesFound(Type type, string location, string[] instances)
        {
            var message = ServiceDiagnosticsFormatter.FormatMultipleServicesFound(type, location, instances);
#if UNITY_EDITOR
            if (OnMultipleServicesFound != null)
            {
                OnMultipleServicesFound(type, location, instances, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyValidationError(Type type, string context, string message)
        {
            var formattedMessage = ServiceDiagnosticsFormatter.FormatValidationError(type, context, message);
#if UNITY_EDITOR
            if (OnValidationError != null)
            {
                OnValidationError(type, context, message, formattedMessage);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(formattedMessage);
        }

        internal static void NotifyInitializationError(Type type, string context, Exception error)
        {
            var message = ServiceDiagnosticsFormatter.FormatInitializationError(type, context, error);
#if UNITY_EDITOR
            if (OnInitializationError != null)
            {
                OnInitializationError(type, context, error, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyCircularDependency(Type type, string[] dependencyChain)
        {
            var message = ServiceDiagnosticsFormatter.FormatCircularDependency(type, dependencyChain);
#if UNITY_EDITOR
            if (OnCircularDependencyDetected != null)
            {
                OnCircularDependencyDetected(type, dependencyChain, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyRuntimeServiceAccessedInEditor(Type type, string location)
        {
            var message = $"[ServiceLocator] Runtime service {type.Name} accessed in editor mode at {location}";
#if UNITY_EDITOR
            if (OnRuntimeServiceAccessedInEditor != null)
            {
                OnRuntimeServiceAccessedInEditor(type, location, message);
                return;
            }
#endif
            GLog.Warning<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyMultipleServiceAssetsFound(Type type, string[] assetPaths)
        {
            var message = $"[ServiceLocator] Found multiple service assets for {type.Name}:\n{string.Join("\n", assetPaths)}";
#if UNITY_EDITOR
            if (OnMultipleServiceAssetsFound != null)
            {
                OnMultipleServiceAssetsFound(type, assetPaths, message);
                return;
            }
#endif
            GLog.Warning<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyEditorOnlyServiceInBuild(Type type)
        {
            var message = ServiceDiagnosticsFormatter.FormatEditorOnlyServiceInBuild(type);
#if UNITY_EDITOR
            if (OnEditorOnlyServiceInBuild != null)
            {
                OnEditorOnlyServiceInBuild(type, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyServiceInstanceValidationError(Type type, string details)
        {
            var message = ServiceDiagnosticsFormatter.FormatServiceInstanceValidationError(type, details);
#if UNITY_EDITOR
            if (OnServiceInstanceValidationError != null)
            {
                OnServiceInstanceValidationError(type, details, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyAsyncDependencyViolation(Type type, string[] dependencyChain)
        {
            var message = $"[ServiceLocator] Service {type.Name} has async dependencies in its chain. Use GetAsync() and consider making all types in the dependency chain IAsyncInitializable to properly handle async initialization.";
#if UNITY_EDITOR
            if (OnAsyncDependencyViolation != null)
            {
                OnAsyncDependencyViolation(type, dependencyChain, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }

        internal static void NotifyAsyncOperationLog(Type type, string context, string message)
        {
            var formattedMessage = $"[ServiceLocator] Async Operation ({type.Name}): {message}";
#if UNITY_EDITOR
            if (OnAsyncOperationLog != null)
            {
                OnAsyncOperationLog(type, context, message, formattedMessage);
                return;
            }
#endif
            GLog.Info<ServiceLocatorLogSystem>(formattedMessage);
        }

        internal static void NotifyAsyncOperationError(Type type, string context, Exception error)
        {
            var message = $"[ServiceLocator] Async Operation Error ({type.Name}): {error.Message}";
#if UNITY_EDITOR
            if (OnAsyncOperationError != null)
            {
                OnAsyncOperationError(type, context, error, message);
                return;
            }
#endif
            GLog.Error<ServiceLocatorLogSystem>(message);
        }
        #endregion

        #region Exception Creation
        internal static ServiceNotFoundException CreateServiceNotFoundException(Type type, string name, string details)
        {
            var message = $"Failed to find service {type.Name} with name '{name}'. {details}";
            NotifyServiceNotFound(type, name, details);
            return new ServiceNotFoundException(message);
        }

        internal static ServiceInitializationException CreateInitializationException(Type type, string context, string details, Exception innerException = null)
        {
            var message = $"Failed to initialize service {type.Name}. {details}";
            NotifyInitializationError(type, context, innerException ?? new Exception(details));
            return new ServiceInitializationException(message, innerException);
        }

        internal static CircularDependencyException CreateCircularDependencyException(Type type, string[] dependencyChain)
        {
            var message = $"Circular dependency detected while initializing {type.Name}. " +
                         $"Dependency chain: {string.Join(" -> ", dependencyChain)}";
            NotifyCircularDependency(type, dependencyChain);
            return new CircularDependencyException(message);
        }

        internal static InvalidOperationException CreateInvalidOperationException(Type type, string context, string details)
        {
            var message = $"Invalid operation for service {type.Name}. {details}";
            NotifyValidationError(type, context, details);
            return new InvalidOperationException(message);
        }

        internal static InvalidOperationException CreateAsyncAccessViolationException(Type type, string details)
        {
            var message = $"Invalid synchronous access to async service {type.Name}. {details}";
            NotifyAsyncOperationError(type, "Sync Access Violation", new InvalidOperationException(message));
            return new InvalidOperationException($"Invalid synchronous access to async service {type.Name}. Service has async dependencies in its chain. Use GetAsync() and consider making all types in the dependency chain IServiceInitializable to properly handle async initialization.");
        }
        #endregion
    }
} 