using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using GAOS.ServiceLocator;

namespace GAOS.ServiceLocator.Editor
{
    public static class ServiceTemplateManager
    {
        private const string INTERFACE_TEMPLATE = @"namespace {0}
{{
    /// <summary>
    /// Interface for {1}
    /// {2}
    /// </summary>
    public interface {3}
    {{
        // TODO: Add service interface members
    }}
}}";

        private const string CLASS_TEMPLATE = @"using GAOS.ServiceLocator;
{0}

namespace {1}
{{
    /// <summary>
    /// Implementation of {2}
    /// {7}
    /// </summary>
    [Service(typeof({2}), ""{3}"", ServiceLifetime.{5}, ServiceContext.{4})]
    public class {6} : {2}
    {{
        /* Manual registration example:
         * To register:
         *     ServiceLocator.Register<{2}, {6}>(""{3}"", ServiceLifetime.{5}, ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({2}), ""{3}"");
         */

        // TODO: Implement service interface members
    }}
}}";

        private const string MONO_TEMPLATE = @"using UnityEngine;
    using GAOS.ServiceLocator;
{0}

namespace {1}
{{
    /// <summary>
    /// MonoBehaviour implementation of {2}
    /// {7}
    /// </summary>
    [Service(typeof({2}), ""{3}"", ServiceLifetime.{5}, ServiceContext.{4})]
    public class {6} : MonoBehaviour, {2}
    {{
        /* Manual registration example:
         * To register:
         *     ServiceLocator.RegisterMonoBehaviourService<{2}, {6}>(""{3}"", ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({2}), ""{3}"");
         */

        // TODO: Implement service interface members
    }}
}}";

        private const string SO_TEMPLATE = @"using UnityEngine;
using GAOS.ServiceLocator;
{0}

namespace {1}
{{
    /// <summary>
    /// ScriptableObject implementation of {2}
    /// {7}
    /// </summary>
    [CreateAssetMenu(fileName = ""{6}"", menuName = ""Services/{6}"")]
    [Service(typeof({2}), ""{3}"", ServiceLifetime.{5}, ServiceContext.{4})]
    public class {6} : ScriptableObject, {2}
    {{
        /* Manual registration example:
         * To register:
         *     ServiceLocator.RegisterScriptableObjectService<{2}, {6}>(""{3}"", ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({2}), ""{3}"");
         */

        private void OnEnable()
        {{
            // TODO: Add initialization logic
        }}

        // TODO: Implement service interface members
    }}
}}";

        private static string GetNamespaceDeclaration(string namespaceName)
        {
            return string.IsNullOrWhiteSpace(namespaceName) ? 
                string.Empty : $"namespace {namespaceName}\n{{";
        }

        private static string GetNamespaceClosing(string namespaceName)
        {
            return string.IsNullOrWhiteSpace(namespaceName) ? 
                string.Empty : "\n}";
        }

        private static string GetRegistrationCode(bool autoRegister, ServiceContext context, ServiceLifetime lifetime, string interfaceName)
        {
            if (!autoRegister) return string.Empty;

            var sb = new StringBuilder("\n        ");
            
            // Add registration options
            var options = new List<string>();
            if (context != ServiceContext.Runtime)
                options.Add($"ServiceContext.{context}");
            if (lifetime == ServiceLifetime.Transient)
                options.Add("ServiceLifetime.Transient");

            string registrationOptions = options.Count > 0 ? 
                ", " + string.Join(", ", options) : 
                string.Empty;

            sb.Append($"ServiceLocator.Register<{interfaceName}>(this{registrationOptions});");
            return sb.ToString();
        }

        private static string GetUnregistrationCode(bool autoRegister, string interfaceName)
        {
            return autoRegister ? 
                $"\n        ServiceLocator.Unregister<{interfaceName}>(this);" : 
                string.Empty;
        }

        public static void CreateServiceInterface(string targetDirectory, string namespaceName, string serviceName, string description)
        {
            string filePath = Path.Combine(targetDirectory, $"I{serviceName}.cs");
            string content = string.Format(INTERFACE_TEMPLATE, 
                string.IsNullOrWhiteSpace(namespaceName) ? "global" : namespaceName,
                serviceName,
                description,
                $"I{serviceName}");

            WriteAndRefresh(filePath, content);
        }

        public static bool CheckAndConfirmFileOverwrite(params string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0) return true;

            // Filter out null paths and check which files exist
            var existingFiles = filePaths
                .Where(path => path != null && File.Exists(path))
                .Select(Path.GetFileName)
                .ToList();

            if (existingFiles.Count == 0) return true;

            string message = $"The following files already exist:\n{string.Join("\n", existingFiles)}\n\nDo you want to overwrite them?";
            return EditorUtility.DisplayDialog("Files Already Exist", message, "Yes", "Cancel");
        }

        public static void CreateServiceImplementation(
            string targetDirectory,
            string namespaceName,
            string className,
            string interfaceName,
            string description,
            ServiceType serviceType,
            bool autoRegister = false,
            ServiceContext context = ServiceContext.Runtime,
            ServiceLifetime lifetime = ServiceLifetime.Singleton,
            bool createInterface = true)
        {
            string servicePath = Path.Combine(targetDirectory, $"{className}.cs");
            string additionalUsings = string.Empty;

            string content;
            switch (serviceType)
            {
                case ServiceType.MonoBehaviour:
                    content = string.Format(MONO_TEMPLATE,
                        additionalUsings,
                        string.IsNullOrWhiteSpace(namespaceName) ? "global" : namespaceName,
                        interfaceName,
                        className,
                        context,
                        lifetime,
                        className,
                        description);
                    break;

                case ServiceType.ScriptableObject:
                    content = string.Format(SO_TEMPLATE,
                        additionalUsings,
                        string.IsNullOrWhiteSpace(namespaceName) ? "global" : namespaceName,
                        interfaceName,
                        className,
                        context,
                        lifetime,
                        className,
                        description);
                    break;

                case ServiceType.Regular:
                default:
                    content = string.Format(CLASS_TEMPLATE,
                        additionalUsings,
                        string.IsNullOrWhiteSpace(namespaceName) ? "global" : namespaceName,
                        interfaceName,
                        className,
                        context,
                        lifetime,
                        className,
                        description);
                    break;
            }

            if (autoRegister)
            {
                // Keep the [Service] attribute and remove the manual registration comment block
                string commentBlock;
                switch (serviceType)
                {
                    case ServiceType.MonoBehaviour:
                        commentBlock = string.Format(@"        /* Manual registration example:
         * To register:
         *     ServiceLocator.RegisterMonoBehaviourService<{0}, {1}>(""{2}"", ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({0}), ""{2}"");
         */

", interfaceName, className, className, lifetime, context);
                        break;

                    case ServiceType.ScriptableObject:
                        commentBlock = string.Format(@"        /* Manual registration example:
         * To register:
         *     ServiceLocator.RegisterScriptableObjectService<{0}, {1}>(""{2}"", ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({0}), ""{2}"");
         */

", interfaceName, className, className, lifetime, context);
                        break;

                    default:
                        commentBlock = string.Format(@"        /* Manual registration example:
         * To register:
         *     ServiceLocator.Register<{0}, {1}>(""{2}"", ServiceLifetime.{3}, ServiceContext.{4});
         * 
         * To unregister:
         *     ServiceLocator.UnregisterService(typeof({0}), ""{2}"");
         */

", interfaceName, className, className, lifetime, context);
                        break;
                }
                content = content.Replace(commentBlock, string.Empty);
            }
            else
            {
                // Remove the [Service] attribute when not using auto-registration
                var attributeLine = string.Format(@"    [Service(typeof({0}), ""{1}"", ServiceLifetime.{2}, ServiceContext.{3})]
", interfaceName, className, lifetime, context);
                if (string.IsNullOrEmpty(attributeLine))
                {
                    Debug.LogWarning($"Service attribute line not found in template for {className}");
                    return;
                }

                content = content.Replace(attributeLine, string.Empty);
            }

            WriteAndRefresh(servicePath, content);
        }

        private static void WriteAndRefresh(string filePath, string content)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
            AssetDatabase.Refresh();
        }
    }
} 