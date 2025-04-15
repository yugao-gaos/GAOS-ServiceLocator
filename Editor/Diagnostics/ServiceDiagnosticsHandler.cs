using UnityEngine;
using UnityEditor;
using System;
using GAOS.ServiceLocator.Diagnostics;

namespace GAOS.ServiceLocator.Editor
{
    [InitializeOnLoad]
    internal static class ServiceDiagnosticsHandler
    {
        static ServiceDiagnosticsHandler()
        {
            // Always unsubscribe first to prevent duplicate handlers
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
            Debug.Log(message);
        }

        private static void HandleServiceNotFound(Type type, string location, string details, string message)
        {
            if (details.Contains("Creating new GameObject"))
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        private static void HandleMultipleServicesFound(Type type, string location, string[] instances, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleValidationError(Type type, string context, string details, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleInitializationError(Type type, string context, Exception error, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleCircularDependency(Type type, string[] dependencyChain, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleRuntimeServiceAccessedInEditor(Type type, string details, string message)
        {
            Debug.LogWarning(message);
        }

        private static void HandleMultipleServiceAssetsFound(Type type, string[] paths, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleEditorOnlyServiceInBuild(Type type, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleServiceInstanceValidationError(Type type, string details, string message)
        {
            Debug.LogError(message);
        }

        private static void HandleAsyncDependencyViolation(Type type, string[] dependencyChain, string message)
        {
            Debug.LogError($"{message}\nDependency chain: {string.Join(" → ", dependencyChain)}");
        }
    }
} 