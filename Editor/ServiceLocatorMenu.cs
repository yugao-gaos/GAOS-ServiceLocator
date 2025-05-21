using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using GAOS.ServiceLocator.Editor.Diagnostics;
using GAOS.Logger;

namespace GAOS.ServiceLocator.Editor
{
    /// <summary>
    /// Menu items for the Service Locator
    /// </summary>
    public static class ServiceLocatorMenu
    {
        private const string MENU_ROOT = "GAOS/Service Locator/";
        private const string DOCUMENTATION_MENU = MENU_ROOT + "Documentation";
        private const string PACKAGE_NAME = "com.gaos.servicelocator";

        [MenuItem(DOCUMENTATION_MENU, false, 20)]
        private static void OpenDocumentation()
        {
            string packagePath = Path.GetFullPath("Packages/" + PACKAGE_NAME);
            string docPath = Path.Combine(packagePath, "Documentation~", "index.html");

            if (File.Exists(docPath))
            {
                Application.OpenURL("file:///" + docPath.Replace("\\", "/"));
            }
            else
            {
                Debug.LogError($"Documentation not found at: {docPath}");
                
                // Fallback to package documentation URL from package.json
                string packageJsonPath = Path.Combine(packagePath, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    string jsonContent = File.ReadAllText(packageJsonPath);
                    var packageJson = JsonUtility.FromJson<PackageJson>(jsonContent);
                    if (!string.IsNullOrEmpty(packageJson.documentationUrl))
                    {
                        Application.OpenURL("file:///" + Path.GetFullPath(packageJson.documentationUrl).Replace("\\", "/"));
                    }
                }
            }
        }

        /// <summary>
        /// Menu item to open the service registry viewer
        /// </summary>
        [MenuItem("GAOS/Service Locator/Open Service Registry")]
        public static void OpenServiceRegistry()
        {
            ServiceRegistryViewer.ShowWindow();
        }

        /// <summary>
        /// Menu item to rebuild the service registry manually
        /// </summary>
        [MenuItem("GAOS/Service Locator/Rebuild Service Registry")]
        public static void RebuildServiceRegistry()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache();
        }

        /// <summary>
        /// Menu item to clear the service registry
        /// </summary>
        [MenuItem("GAOS/Service Locator/Clear Service Registry")]
        public static void ClearServiceRegistry()
        {
            var typeCache = Resources.Load<ServiceTypeCache>("ServiceTypeCache");
            if (typeCache != null)
            {
                typeCache.Clear();
                EditorUtility.SetDirty(typeCache);
                AssetDatabase.SaveAssets();
                GLog.Info<ServiceLocatorEditorLogSystem>("Service registry cleared");
            }
        }

        /// <summary>
        /// Menu item to toggle project-only circular dependency reporting
        /// </summary>
        [MenuItem("GAOS/Service Locator/Toggle Project-Only Circular Dependencies")]
        public static void ToggleProjectOnlyCircularDependencies()
        {
            var settings = LoadDependencyValidationSettings();
            if (settings != null)
            {
                settings.OnlyReportProjectCircularDependencies = !settings.OnlyReportProjectCircularDependencies;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                
                string state = settings.OnlyReportProjectCircularDependencies ? "enabled" : "disabled";
                GLog.Info<ServiceLocatorEditorLogSystem>($"Project-only circular dependency reporting {state}");
            }
        }
        
        /// <summary>
        /// Menu item validation for Toggle Project-Only Circular Dependencies
        /// </summary>
        [MenuItem("GAOS/Service Locator/Toggle Project-Only Circular Dependencies", true)]
        public static bool ValidateToggleProjectOnlyCircularDependencies()
        {
            var settings = LoadDependencyValidationSettings();
            if (settings != null)
            {
                Menu.SetChecked("GAOS/Service Locator/Toggle Project-Only Circular Dependencies", 
                    settings.OnlyReportProjectCircularDependencies);
            }
            return true;
        }
        
        /// <summary>
        /// Loads the dependency validation settings
        /// </summary>
        private static DependencyValidationSettings LoadDependencyValidationSettings()
        {
            // First try to load from Resources
            var settings = Resources.Load<DependencyValidationSettings>("DependencyValidationSettings");
            
            // If not found, try to find in the project
            if (settings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:DependencyValidationSettings");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    settings = AssetDatabase.LoadAssetAtPath<DependencyValidationSettings>(path);
                }
            }
            
            // If still not found, create a default one
            if (settings == null)
            {
                // Create Resources folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                
                settings = ScriptableObject.CreateInstance<DependencyValidationSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Resources/DependencyValidationSettings.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log("Created default DependencyValidationSettings.asset in Resources folder");
            }
            
            return settings;
        }

        [MenuItem("GAOS/Service Locator/Open Service Cache", false, 30)]
        public static void OpenServiceCache()
        {
            var guids = AssetDatabase.FindAssets("t:ServiceTypeCache");
            if (guids.Length == 0)
            {
                Debug.LogError("ServiceTypeCache asset not found in project");
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var cache = AssetDatabase.LoadAssetAtPath<ServiceTypeCache>(path);
            
            // Ping and select in project window
            EditorGUIUtility.PingObject(cache);
            Selection.activeObject = cache;

            // Open in inspector
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            Debug.Log($"Inspector type found: {inspectorType != null}");
            
            var currentInspector = EditorWindow.GetWindow(inspectorType) as EditorWindow;
            Debug.Log($"Current inspector found: {currentInspector != null}");
            
            // Check if current inspector is locked
            bool isLocked = false;
            if (currentInspector != null)
            {
                var isLockedProperty = currentInspector.GetType().GetProperty("isLocked");
                Debug.Log($"IsLocked property found: {isLockedProperty != null}");
                
                if (isLockedProperty != null)
                {
                    isLocked = (bool)isLockedProperty.GetValue(currentInspector);
                    Debug.Log($"Current inspector is locked: {isLocked}");
                }
            }

            // Always create a new inspector if locked or doesn't exist
            if (isLocked || currentInspector == null)
            {
                Debug.Log("Attempting to create new inspector window");
                
                // Get current inspector position
                Rect currentPos = currentInspector != null ? currentInspector.position : new Rect(0, 0, 400, 600);
                
                // Create new window slightly offset
                var newWindow = ScriptableObject.CreateInstance(inspectorType) as EditorWindow;
                newWindow.position = new Rect(currentPos.x + 50, currentPos.y + 50, currentPos.width, currentPos.height);
                newWindow.Show();
                newWindow.Focus();
                
                Debug.Log($"New inspector window created: {newWindow != null}");
            }
            else
            {
                Debug.Log("Focusing existing inspector");
                currentInspector.Focus();
            }
        }

        private class PackageJson
        {
            public string documentationUrl;
        }
    }
} 