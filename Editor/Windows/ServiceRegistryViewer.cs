using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace GAOS.ServiceLocator.Editor
{
    public class ServiceRegistryViewer : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _searchQuery = "";
        private bool _showRuntimeServices = true;
        private bool _showEditorServices = true;
        private bool _showPlayModeServices = true;
        private GUIStyle _headerStyle;
        private GUIStyle _serviceStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _groupStyle;
        private bool _stylesInitialized;

        // Removed menu item to hide from Window menu
        // [MenuItem("Window/GAOS/Service Registry Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<ServiceRegistryViewer>();
            window.titleContent = new GUIContent("Service Registry");
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Service Registry");
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            _headerStyle = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 5, 5),
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };

            _labelStyle = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 2, 2),
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };

            _serviceStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(12, 12, 0, 8)
            };

            _errorStyle = new GUIStyle(_serviceStyle)
            {
                normal = { textColor = new Color(1f, 0.3f, 0.3f) }
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                margin = new RectOffset(4, 0, 0, 0)
            };

            _groupStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(16, 16, 0, 0)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space();
            DrawHeader();
            EditorGUILayout.Space();

            DrawSearchAndFilter();
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawServiceList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawFooter();
        }

        private void DrawHeader()
        {
            var services = ServiceLocator.GetRegisteredServices().ToList();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Service Registry", _headerStyle);
            EditorGUILayout.LabelField($"Total Services: {services.Count}", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchAndFilter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12); // Left margin to match outer boxes
                
                // Search field without label
                _searchQuery = GUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);

                GUILayout.Space(8); // Space between search and filters

                // Context filters
                _showRuntimeServices = GUILayout.Toggle(_showRuntimeServices, "Runtime", EditorStyles.toolbarButton);
                _showEditorServices = GUILayout.Toggle(_showEditorServices, "Editor", EditorStyles.toolbarButton);
                _showPlayModeServices = GUILayout.Toggle(_showPlayModeServices, "Play Mode", EditorStyles.toolbarButton);

                GUILayout.Space(12); // Right margin to match outer boxes
            }
        }

        private void DrawServiceList()
        {
            var services = ServiceLocator.GetRegisteredServices()
                .Where(s => PassesFilters(s))
                .OrderBy(s => s.Context)
                .ThenBy(s => s.InterfaceType.Name)
                .ThenBy(s => s.Name);

            ServiceContext currentContext = ServiceContext.Runtime;
            bool headerDrawn = false;

            foreach (var service in services)
            {
                if (service.Context != currentContext)
                {
                    currentContext = service.Context;
                    headerDrawn = false;
                }

                if (!headerDrawn)
                {
                    DrawContextHeader(currentContext);
                    headerDrawn = true;
                }

                DrawServiceEntry(service);
            }
        }

        private bool PassesFilters(ServiceLocator.ServiceRegistrationInfo service)
        {
            // Context filters
            if (!_showRuntimeServices && service.Context == ServiceContext.Runtime) return false;
            if (!_showEditorServices && service.Context == ServiceContext.EditorOnly) return false;
            if (!_showPlayModeServices && service.Context == ServiceContext.RuntimeAndEditor) return false;

            // Search filter
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                var searchLower = _searchQuery.ToLower();
                return service.Name.ToLower().Contains(searchLower) ||
                       service.InterfaceType.Name.ToLower().Contains(searchLower) ||
                       service.ImplementationType.Name.ToLower().Contains(searchLower);
            }

            return true;
        }

        private void DrawContextHeader(ServiceContext context)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(context.ToString(), _headerStyle);
        }

        private void DrawServiceEntry(ServiceLocator.ServiceRegistrationInfo service)
        {
            EditorGUILayout.BeginVertical(service.HasError ? _errorStyle : _serviceStyle);

            // Service header with proper indentation
            EditorGUILayout.BeginHorizontal();
            
            // Service type badge
            var typeColor = GetServiceTypeColor(service.ServiceType);
            var oldColor = GUI.color;
            GUI.color = typeColor;
            EditorGUILayout.LabelField($"[{service.ServiceType}]", EditorStyles.miniLabel, GUILayout.Width(100));
            GUI.color = oldColor;

            // Service name with click-to-copy
            if (GUILayout.Button(service.Name, EditorStyles.label, GUILayout.Width(200)))
            {
                GUIUtility.systemCopyBuffer = service.Name;
                Debug.Log($"Copied service name to clipboard: {service.Name}");
            }

            // Interface and Implementation on same line
            EditorGUILayout.LabelField($"{service.InterfaceType.Name} → {service.ImplementationType.Name}", GUILayout.ExpandWidth(true));

            // Instance counts for transient services
            if (service.Lifetime == ServiceLifetime.Transient)
            {
                var countStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };
                EditorGUILayout.LabelField($"[{service.ActiveInstanceCount}/{service.TotalInstancesCreated}]", countStyle, GUILayout.Width(50));
            }

            // Status and configuration
            string status = service.IsInitialized ? "✓" : "○";
            if (service.HasError) status = "⚠";
            var statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = service.HasError ? Color.red : (service.IsInitialized ? Color.green : Color.gray) }
            };
            EditorGUILayout.LabelField(status, statusStyle, GUILayout.Width(20));

            // Lifetime and Context as icons/short text
            string lifetimeText = service.Lifetime == ServiceLifetime.Singleton ? "S" : "T";
            var tooltip = GetLifetimeTooltip(service.Lifetime);
            EditorGUILayout.LabelField(new GUIContent(lifetimeText, tooltip), GUILayout.Width(20));

            string contextText = service.Context switch
            {
                ServiceContext.Runtime => "R",
                ServiceContext.EditorOnly => "E",
                ServiceContext.RuntimeAndEditor => "R+E",
                _ => "?"
            };
            tooltip = GetContextTooltip(service.Context);
            EditorGUILayout.LabelField(new GUIContent(contextText, tooltip), GUILayout.Width(30));
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private Color GetServiceTypeColor(ServiceType type)
        {
            return type switch
            {
                ServiceType.MonoBehaviour => new Color(0.4f, 0.8f, 0.4f),
                ServiceType.ScriptableObject => new Color(0.6f, 0.6f, 1f),
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
        }

        private string GetLifetimeTooltip(ServiceLifetime lifetime)
        {
            return lifetime switch
            {
                ServiceLifetime.Singleton => "A single instance is created and reused for all requests",
                ServiceLifetime.Transient => "A new instance is created for each request",
                _ => lifetime.ToString()
            };
        }

        private string GetContextTooltip(ServiceContext context)
        {
            return context switch
            {
                ServiceContext.Runtime => "Service is only available during play mode",
                ServiceContext.EditorOnly => "Service is only available in the editor",
                ServiceContext.RuntimeAndEditor => "Service is available in both play mode and editor",
                _ => context.ToString()
            };
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Refresh"))
            {
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
} 