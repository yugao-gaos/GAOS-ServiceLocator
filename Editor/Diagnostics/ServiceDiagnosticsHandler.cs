using UnityEngine;
using System;
using GAOS.Logger;
using GAOS.ServiceLocator.Diagnostics;   

namespace GAOS.ServiceLocator.Editor.Diagnostics
{
    public static class ServiceDiagnosticsHandler
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void Initialize()
        {
            // First unsubscribe to avoid double subscription
            ServiceDiagnostics.OnServiceCreated -= HandleServiceCreated;
            ServiceDiagnostics.OnServiceNotFound -= HandleServiceNotFound;
            ServiceDiagnostics.OnMultipleServicesFound -= HandleMultipleServicesFound;
            ServiceDiagnostics.OnValidationError -= HandleValidationError;
            ServiceDiagnostics.OnInitializationError -= HandleInitializationError;
            ServiceDiagnostics.OnCircularDependencyDetected -= HandleCircularDependency;
            ServiceDiagnostics.OnRuntimeServiceAccessedInEditor -= HandleRuntimeServiceAccessedInEditor;
            ServiceDiagnostics.OnMultipleServiceAssetsFound -= HandleMultipleServiceAssetsFound;
            ServiceDiagnostics.OnEditorOnlyServiceInBuild -= HandleEditorOnlyServiceInBuild;
            ServiceDiagnostics.OnServiceInstanceValidationError -= HandleServiceInstanceValidationError;
            ServiceDiagnostics.OnAsyncDependencyViolation -= HandleAsyncDependencyViolation;

            // Then subscribe
            ServiceDiagnostics.OnServiceCreated += HandleServiceCreated;
            ServiceDiagnostics.OnServiceNotFound += HandleServiceNotFound;
            ServiceDiagnostics.OnMultipleServicesFound += HandleMultipleServicesFound;
            ServiceDiagnostics.OnValidationError += HandleValidationError;
            ServiceDiagnostics.OnInitializationError += HandleInitializationError;
            ServiceDiagnostics.OnCircularDependencyDetected += HandleCircularDependency;
            ServiceDiagnostics.OnRuntimeServiceAccessedInEditor += HandleRuntimeServiceAccessedInEditor;
            ServiceDiagnostics.OnMultipleServiceAssetsFound += HandleMultipleServiceAssetsFound;
            ServiceDiagnostics.OnEditorOnlyServiceInBuild += HandleEditorOnlyServiceInBuild;
            ServiceDiagnostics.OnServiceInstanceValidationError += HandleServiceInstanceValidationError;
            ServiceDiagnostics.OnAsyncDependencyViolation += HandleAsyncDependencyViolation;
        }

        private static void HandleServiceCreated(Type type, string location, string details, string message)
        {
            GLog.Info<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleServiceNotFound(Type type, string location, string details, string message)
        {
            if (details.Contains("Creating new GameObject"))
            {
                GLog.Info<ServiceLocatorEditorLogSystem>(message);
            }
            else
            {
                GLog.Warning<ServiceLocatorEditorLogSystem>(message);
            }
        }

        private static void HandleMultipleServicesFound(Type type, string location, string[] instances, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleValidationError(Type type, string context, string details, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleInitializationError(Type type, string context, Exception error, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleCircularDependency(Type type, string[] dependencyChain, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleRuntimeServiceAccessedInEditor(Type type, string location, string message)
        {
            GLog.Warning<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleMultipleServiceAssetsFound(Type type, string[] assetPaths, string message)
        {
            GLog.Warning<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleEditorOnlyServiceInBuild(Type type, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleServiceInstanceValidationError(Type type, string details, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }

        private static void HandleAsyncDependencyViolation(Type type, string[] dependencyChain, string message)
        {
            GLog.Error<ServiceLocatorEditorLogSystem>(message);
        }
    }
} 