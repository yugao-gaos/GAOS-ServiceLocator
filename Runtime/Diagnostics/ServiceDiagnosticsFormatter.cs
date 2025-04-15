using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    internal static class ServiceDiagnosticsFormatter
    {
        public static string FormatServiceCreated(Type type, string location, string details)
        {
            return $"[ServiceLocator] Created new instance of {type.Name}\n" +
                   $"Location: {location}\n" +
                   $"Details: {details}\n" +
                   $"Asset Path: Runtime created";
        }

        public static string FormatServiceNotFound(Type type, string location, string details)
        {
            return $"[ServiceLocator] Service of type {type.Name} with name '{location}' is not registered\n" +
                   $"Details: {details}";
        }

        public static string FormatMultipleServicesFound(Type type, string location, string[] instances)
        {
            var instanceList = string.Join("\n  ", instances);
            return $"[ServiceLocator] Found multiple instances of {type.Name}\n" +
                   $"Location: {location}\n" +
                   $"Instances:\n  {instanceList}";
        }

        public static string FormatValidationError(Type type, string context, string message)
        {
            return $"[ServiceLocator] Validation error for {type.Name}\n" +
                   $"Context: {context}\n" +
                   $"Message: {message}";
        }

        public static string FormatInitializationError(Type type, string context, Exception error)
        {
            return $"[ServiceLocator] Initialization error for {type.Name}\n" +
                   $"Context: {context}\n" +
                   $"Error: {error}";
        }

        public static string FormatCircularDependency(Type type, string[] dependencyChain)
        {
            return $"[ServiceLocator] Circular dependency detected for {type.Name}\n" +
                   $"Dependency Chain: {string.Join(" -> ", dependencyChain)}";
        }

        public static string FormatRuntimeServiceAccessedInEditor(Type type, string details)
        {
            return $"[ServiceLocator] Runtime service {type.Name} accessed in editor mode: {details}";
        }

        public static string FormatMultipleServiceAssetsFound(Type type, string[] paths)
        {
            var pathList = string.Join("\n  ", paths);
            return $"[ServiceLocator] Multiple {type.Name} assets found in project\n" +
                   $"Paths:\n  {pathList}";
        }

        public static string FormatEditorOnlyServiceInBuild(Type type)
        {
            return $"[ServiceLocator] {type.Name} is marked as EditorOnly but is being used in a build";
        }

        public static string FormatServiceInstanceValidationError(Type type, string details)
        {
            return $"[ServiceLocator] Service instance validation error for {type.Name}: {details}";
        }
    }
} 