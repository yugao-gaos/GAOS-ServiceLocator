using UnityEngine;
using UnityEditor;
using System.IO;

namespace GAOS.ServiceLocator.Editor
{
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

        [MenuItem("GAOS/Service Locator/Open Service Registry Viewer", false, 10)]
        public static void OpenServiceRegistryViewer()
        {
            ServiceRegistryViewer.ShowWindow();
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