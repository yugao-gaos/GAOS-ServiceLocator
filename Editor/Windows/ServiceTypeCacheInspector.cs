using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.IO;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Optional;

namespace GAOS.ServiceLocator.Editor
{
    [CustomEditor(typeof(ServiceTypeCache))]
    public class ServiceTypeCacheInspector : UnityEditor.Editor
    {
        private bool[] _serviceTypeFoldouts;
        private Vector2 _scrollPosition;
        private string _searchQuery = "";
        private bool _isLoading;
        private bool _cacheInitialized;
        private double _lastDomainReloadTime;
        private const double DOMAIN_RELOAD_GRACE_PERIOD = 1.0; // 1 second grace period

        // Window size tracking
        private float _lastWindowHeight;

        // Pagination
        private int _currentPage = 0;
        private int _totalPages = 0;
        private List<ServiceTypeInfo> _filteredServices = new List<ServiceTypeInfo>();
        private int _itemsPerPage = 10; // Default value, will be recalculated

        // Cache for script paths and type locations
        private static Dictionary<Type, string> _typeToScriptPathCache = new();
        private static Dictionary<Type, int> _typeToLineNumberCache = new();
        private static HashSet<string> _allScriptPaths;
        private static bool _pathCacheInitialized;
        private static string _lastCachedAssetPath;
        private static System.DateTime _lastAssetModificationTime;

        // Add enum for filter options
        private enum ServiceTypeFilter
        {
            All = -1,
            Regular = ServiceType.Regular,
            MonoBehaviour = ServiceType.MonoBehaviour,
            ScriptableObject = ServiceType.ScriptableObject
        }

        private enum ServiceContextFilter
        {
            All = -1,
            Runtime = ServiceContext.Runtime,
            EditorOnly = ServiceContext.EditorOnly,
            RuntimeAndEditor = ServiceContext.RuntimeAndEditor
        }

        private ServiceTypeFilter _typeFilter = ServiceTypeFilter.All;
        private ServiceContextFilter _contextFilter = ServiceContextFilter.All;

        private void OnEnable()
        {
            _isLoading = true;
            _cacheInitialized = false;
            _lastDomainReloadTime = EditorApplication.timeSinceStartup;
            


            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= HandleAfterAssemblyReload;
            ServiceTypeCacheBuilder.OnTypeRegistryRebuilt -= HandleTypeRegistryRebuilt;

            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
            ServiceTypeCacheBuilder.OnTypeRegistryRebuilt += HandleTypeRegistryRebuilt;
            
            InitializeServiceTypeFoldouts();
            CheckAndRefreshScriptCache();
            _currentPage = 0; // Reset to first page when window is enabled
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= HandleAfterAssemblyReload;
            ServiceTypeCacheBuilder.OnTypeRegistryRebuilt -= HandleTypeRegistryRebuilt;
        }

        private void HandleBeforeAssemblyReload()
        {
            _isLoading = true;
            _cacheInitialized = false;
        }

        private void HandleAfterAssemblyReload()
        {
            _lastDomainReloadTime = EditorApplication.timeSinceStartup;
            _isLoading = true;
        }

        private void HandleTypeRegistryRebuilt()
        {
            _isLoading = false;
            InitializeServiceTypeFoldouts();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            // Check if we're still in the grace period after domain reload
            if (_isLoading && EditorApplication.timeSinceStartup - _lastDomainReloadTime > DOMAIN_RELOAD_GRACE_PERIOD)
            {
                InitializeServiceTypeFoldouts();
                _isLoading = false;
                Repaint();
            }
        }

        private void InitializeServiceTypeFoldouts()
        {
            try 
            {
                var registry = target as ServiceTypeCache;
                if (registry != null && registry.ServiceTypes != null)
                {
                    _serviceTypeFoldouts = new bool[registry.ServiceTypes.Count];
                    _cacheInitialized = true;
                    _currentPage = 0; // Reset to first page when initializing

                    // If validation cache is not available, wait for it
                    if (!ServiceTypeCacheBuilder.IsValidationCacheAvailable)
                    {
                        _isLoading = true;
                        return;
                    }

                    // Log validation messages
                    foreach (var serviceInfo in registry.ServiceTypes)
                    {
                        if (serviceInfo?.ImplementationType == null) continue;

                        var validationData = ServiceTypeCacheBuilder.GetValidationData(serviceInfo.ImplementationType);
                        if (validationData == null) continue;

                        // Log messages if any
                        foreach (var (message, type) in validationData.ValidationMessages)
                        {
                            switch (type)
                            {
                                case MessageType.Error:
                                    Debug.LogError($"[ServiceLocator] {message}");
                                    break;
                                case MessageType.Warning:
                                    Debug.LogWarning($"[ServiceLocator] {message}");
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServiceLocator] Error initializing service type foldouts: {ex}");
            }
        }

        private void RecalculateItemsPerPage()
        {
            // Get the actual window height where the inspector is docked
            float availableHeight = Mathf.Max(Screen.height - 150, 400); // Minimum height of 400 pixels
            if (availableHeight <= 0) return;

            // Calculate space needed for UI elements
            float headerHeight = EditorGUIUtility.singleLineHeight * 2; // Header + spacing
            float searchHeight = EditorGUIUtility.singleLineHeight * 2; // Search bar + spacing
            float paginationHeight = _totalPages > 1 ? EditorGUIUtility.singleLineHeight * 2 : 0; // Pagination controls if needed
            float footerHeight = EditorGUIUtility.singleLineHeight * 2; // Footer buttons
            float padding = 40f; // Additional padding

            float totalUIHeight = headerHeight + searchHeight + paginationHeight + footerHeight + padding;
            float remainingHeight = Mathf.Max(availableHeight - totalUIHeight, 100f); // Ensure minimum space

            // Base height for a collapsed service item (including margins)
            float itemHeight = EditorGUIUtility.singleLineHeight + 16f; // Line height + margins

            // Calculate how many items can fit, with a minimum of 5 and maximum of 20
            int newItemsPerPage = Mathf.Clamp(Mathf.FloorToInt(remainingHeight / itemHeight), 5, 20);
            
            // Only update if the number of items per page has changed
            if (newItemsPerPage != _itemsPerPage)
            {
                _itemsPerPage = newItemsPerPage;
                
                // Recalculate pagination
                if (_filteredServices != null)
                {
                    _totalPages = Mathf.CeilToInt((float)_filteredServices.Count / _itemsPerPage);
                    _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, _totalPages - 1));
                }
                
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            // Only recalculate pagination when window resize is complete
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.Used && _lastWindowHeight != Screen.height)
            {
                _lastWindowHeight = Screen.height;
                RecalculateItemsPerPage();
            }
            
            if (_isLoading)
            {
                EditorGUILayout.HelpBox("Loading service cache...", MessageType.Info);
                return;
            }

            if (!_cacheInitialized)
            {
                EditorGUILayout.HelpBox("Service cache not initialized. Try rebuilding the cache.", MessageType.Warning);
                if (GUILayout.Button("Rebuild Cache"))
                {
                    ServiceTypeCacheBuilder.RebuildTypeCache();
                }
                return;
            }

            var registry = target as ServiceTypeCache;
            if (registry == null || registry.ServiceTypes == null)
            {
                EditorGUILayout.HelpBox("Invalid service cache state.", MessageType.Error);
                return;
            }

            try
            {
                CheckAndRefreshScriptCache();
                
                // Filter services based on search and filters only (no sorting)
                _filteredServices = registry.ServiceTypes
                    .Where(s => PassesFilters(s))
                    .ToList();

                // Calculate pagination
                RecalculateItemsPerPage();
                
                DrawHeader(_filteredServices.Count);
                EditorGUILayout.Space();

                DrawSearchAndFilter();
                EditorGUILayout.Space();

                // Draw top pagination controls if we have multiple pages
                DrawPaginationControls(true);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                
                // Get current page of services
                var pageServices = _filteredServices
                    .Skip(_currentPage * _itemsPerPage)
                    .Take(_itemsPerPage);

                foreach (var service in pageServices)
                {
                    DrawServiceTypeInfo(service, _filteredServices.IndexOf(service));
                }

                EditorGUILayout.EndScrollView();

                // Draw bottom pagination controls if we have multiple pages
                DrawPaginationControls(false);

                EditorGUILayout.Space();
                DrawFooter();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error drawing ServiceTypeCacheInspector: {ex}");
                EditorGUILayout.HelpBox("An error occurred while drawing the inspector. Check console for details.", MessageType.Error);
            }
        }

        private void DrawHeader(int serviceCount)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Service Type Cache", EditorStyles.boldLabel);
            
            string pageInfo = _totalPages > 0 ? $" (Page {_currentPage + 1}/{_totalPages})" : "";
            EditorGUILayout.LabelField($"Total Services: {serviceCount}{pageInfo}", EditorStyles.miniLabel, GUILayout.Width(150));
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPaginationControls(bool isTop)
        {
            if (_totalPages <= 1) return;

            EditorGUILayout.BeginHorizontal();
            
            if (isTop)
            {
                GUILayout.FlexibleSpace();
            }
            else
            {
                EditorGUILayout.Space();
            }

            // Previous button
            GUI.enabled = _currentPage > 0;
            if (GUILayout.Button("◄", GUILayout.Width(25)))
            {
                _currentPage--;
                _scrollPosition = Vector2.zero;
            }
            GUI.enabled = true;

            // Calculate visible page range
            const int maxVisiblePages = 7;
            int startPage = Mathf.Max(0, _currentPage - maxVisiblePages / 2);
            int endPage = Mathf.Min(_totalPages - 1, startPage + maxVisiblePages - 1);
            
            // Adjust start if we're near the end
            if (endPage - startPage < maxVisiblePages - 1)
            {
                startPage = Mathf.Max(0, endPage - maxVisiblePages + 1);
            }

            // First page if not in range
            if (startPage > 0)
            {
                if (GUILayout.Button("1", EditorStyles.miniButton, GUILayout.Width(25)))
                {
                    _currentPage = 0;
                    _scrollPosition = Vector2.zero;
                }
                if (startPage > 1)
                {
                    GUILayout.Label("...", EditorStyles.miniLabel, GUILayout.Width(15));
                }
            }

            // Visible page numbers
            for (int i = startPage; i <= endPage; i++)
            {
                var style = i == _currentPage ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                GUI.backgroundColor = i == _currentPage ? new Color(0.7f, 0.85f, 1f) : Color.white;
                
                if (GUILayout.Button((i + 1).ToString(), style, GUILayout.Width(25)))
                {
                    if (i != _currentPage)
                    {
                        _currentPage = i;
                        _scrollPosition = Vector2.zero;
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            // Last page if not in range
            if (endPage < _totalPages - 1)
            {
                if (endPage < _totalPages - 2)
                {
                    GUILayout.Label("...", EditorStyles.miniLabel, GUILayout.Width(15));
                }
                if (GUILayout.Button(_totalPages.ToString(), EditorStyles.miniButton, GUILayout.Width(25)))
                {
                    _currentPage = _totalPages - 1;
                    _scrollPosition = Vector2.zero;
                }
            }

            // Next button
            GUI.enabled = _currentPage < _totalPages - 1;
            if (GUILayout.Button("►", GUILayout.Width(25)))
            {
                _currentPage++;
                _scrollPosition = Vector2.zero;
            }
            GUI.enabled = true;

            if (!isTop)
            {
                GUILayout.FlexibleSpace();
            }
            
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

                // Type filter dropdown
                EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
                _typeFilter = (ServiceTypeFilter)EditorGUILayout.EnumPopup(_typeFilter, GUILayout.Width(120));

                GUILayout.Space(8); // Space between filters

                // Context filter dropdown
                EditorGUILayout.LabelField("Context:", GUILayout.Width(55));
                _contextFilter = (ServiceContextFilter)EditorGUILayout.EnumPopup(_contextFilter, GUILayout.Width(120));

                GUILayout.Space(12); // Right margin to match outer boxes
            }
        }

        private void DrawServiceList(IReadOnlyList<ServiceTypeInfo> serviceTypes)
        {
            for (int i = 0; i < serviceTypes.Count; i++)
            {
                var serviceInfo = serviceTypes[i];

                // Apply filters
                if (!string.IsNullOrEmpty(_searchQuery))
                {
                    var searchLower = _searchQuery.ToLower();
                    if (!serviceInfo.InterfaceTypeName.ToLower().Contains(searchLower) &&
                        !serviceInfo.ImplementationTypeName.ToLower().Contains(searchLower))
                        continue;
                }

                if (_typeFilter != ServiceTypeFilter.All && (ServiceType)_typeFilter != serviceInfo.ServiceType)
                    continue;

                DrawServiceTypeInfo(serviceInfo, i);
            }
        }

        private void DrawServiceTypeInfo(ServiceTypeInfo info, int index)
        {
            try
            {
                var outerStyle = new GUIStyle(EditorStyles.helpBox);
                outerStyle.margin = new RectOffset(12, 12, 0, 8);

                EditorGUILayout.BeginVertical(outerStyle);

                // Service header with proper indentation
                EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
                GUILayout.Space(8);
                
                // Service type badge first
                var typeColor = GetServiceTypeColor(info.ServiceType);
                var oldColor = GUI.color;
                GUI.color = typeColor;
                EditorGUILayout.LabelField($"[{info.ServiceType}]", EditorStyles.miniLabel, GUILayout.Width(100));
                GUI.color = oldColor;

                GUILayout.Space(4);
                
                // Then the title with fixed width to prevent overlap
                string title = info.ImplementationTypeName;
                if (info.IsAsyncService)
                {
                    title += " [Async]";
                }
                _serviceTypeFoldouts[index] = EditorGUILayout.Foldout(_serviceTypeFoldouts[index], title, true);

                // Get validation data once for all sections
                var validationData = ServiceTypeCacheBuilder.GetValidationData(info.ImplementationType);

                // Show validation icons in header for any type of validation message
                if (validationData?.ValidationMessages != null && validationData.ValidationMessages.Any())
                {
                    GUILayout.Space(-20); // Move icons closer to the title
                    foreach (var (message, type) in validationData.ValidationMessages)
                    {
                        // Only show icons for warnings and errors
                        if (type != MessageType.Info)
                        {
                            var icon = type == MessageType.Error ? 
                                EditorGUIUtility.FindTexture("console.erroricon") :
                                EditorGUIUtility.FindTexture("console.warnicon");
                            GUILayout.Label(new GUIContent(icon, message), GUILayout.Width(20));
                        }
                    }
                }
                
                EditorGUILayout.EndHorizontal();

                if (_serviceTypeFoldouts[index])
                {
                    EditorGUI.indentLevel++;
                    GUILayout.Space(8);

                    // Create consistent layout
                    float labelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 140;

                    // Group style with symmetric padding
                    var groupStyle = new GUIStyle(EditorStyles.helpBox);
                    groupStyle.padding = new RectOffset(12, 12, 8, 8);
                    groupStyle.margin = new RectOffset(16, 16, 0, 0);

                    // Section title style
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel);
                    titleStyle.margin = new RectOffset(4, 0, 0, 0);

                    // Group 1: Basic Information
                    EditorGUILayout.BeginVertical(groupStyle);
                    EditorGUILayout.LabelField("Basic Information", titleStyle);
                    GUILayout.Space(4);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        // Service Name first
                        if (!string.IsNullOrEmpty(info.DefaultName))
                        {
                            DrawDetailRow("Service Name", info.DefaultName);
                        }

                        // Interface
                        DrawDetailRow("Interface", info.InterfaceTypeName, () => OpenCodeEditorAtType(info.InterfaceType));

                        // Implementation
                        DrawDetailRow("Implementation", info.ImplementationTypeName, () => OpenCodeEditorAtType(info.ImplementationType));

                        // Add description from XML docs if available
                        string description = GetTypeDescription(info.ImplementationType);
                        if (!string.IsNullOrEmpty(description))
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                            EditorGUILayout.HelpBox(description, MessageType.None);
                        }
                    }

                    EditorGUILayout.EndVertical();
                    
                    GUILayout.Space(8);

                    // Group 2: Service Configuration
                    EditorGUILayout.BeginVertical(groupStyle);
                    EditorGUILayout.LabelField("Service Configuration", titleStyle);
                    GUILayout.Space(4);
                    
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawDetailRow("Service Type", info.ServiceType.ToString());
                        DrawDetailRow("Lifetime", info.DefaultLifetime.ToString(), tooltip: GetLifetimeTooltip(info.DefaultLifetime));
                        DrawDetailRow("Context", info.DefaultContext.ToString(), tooltip: GetContextTooltip(info.DefaultContext));

                        // Show implemented interfaces
                        var implementedInterfaces = info.ImplementationType.GetInterfaces();
                        var specialInterfaces = implementedInterfaces.Where(i => 
                            i == typeof(IServiceInitializable) || 
                            i == typeof(IAsyncDisposable) ||
                            i.Name.StartsWith("I") && (i.Name.Contains("Service") || i.Name.Contains("Provider")));

                        if (specialInterfaces.Any())
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Implemented Interfaces", EditorStyles.boldLabel);
                            foreach (var iface in specialInterfaces)
                            {
                                EditorGUILayout.LabelField("• " + iface.Name, EditorStyles.miniLabel);
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();

                    GUILayout.Space(8);

                    // Group 3: Dependencies & Initialization
                    DrawDependenciesSection(info, groupStyle, titleStyle, validationData?.DependencyTree);

                    GUILayout.Space(8);

                    // Group 4: Location Information
                    EditorGUILayout.BeginVertical(groupStyle);
                    EditorGUILayout.LabelField("Location Information", titleStyle);
                    GUILayout.Space(4);
                    
                    using (new EditorGUILayout.VerticalScope())
                    {
                        // Show assembly first
                        DrawDetailRow("Assembly", info.ImplementationType.Assembly.GetName().Name);

                        // Show assembly type
                        DrawDetailRow("Assembly Type", info.AssemblyType.ToString(), 
                            tooltip: GetAssemblyTypeTooltip(info.AssemblyType));

                        // Show file location with consistent alignment
                        string scriptPath = GetScriptPath(info.ImplementationType);
                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            DrawFileLocation(scriptPath, info.ImplementationType);
                        }
                    }
                    EditorGUILayout.EndVertical();

                    GUILayout.Space(12);

                    // Restore original label width
                    EditorGUIUtility.labelWidth = labelWidth;
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CRASH] Error in DrawServiceTypeInfo: {ex.Message}\nStack trace: {ex.StackTrace}");
                try
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                catch (Exception cleanup)
                {
                    Debug.LogError($"[CRASH] Additional error during cleanup: {cleanup.Message}");
                }
            }
        }

        private void DrawDependenciesSection(ServiceTypeInfo info, GUIStyle groupStyle, GUIStyle titleStyle, ServiceTypeCacheBuilder.DependencyNode dependencyTree)
        {
            if (dependencyTree == null) return;

            EditorGUILayout.BeginVertical(groupStyle);
            EditorGUILayout.LabelField("Dependencies & Initialization", titleStyle);
            EditorGUILayout.Space();

            EditorGUI.indentLevel++; // Add indentation for dependency tree

            // Draw dependency tree first
            var originalIndent = EditorGUI.indentLevel;
            DrawDependencyNode(dependencyTree, 0);
            EditorGUI.indentLevel = originalIndent; // Restore original indentation

            EditorGUI.indentLevel--; // Reset indentation

            // Show validation messages related to dependencies at the bottom
            var validationData = ServiceTypeCacheBuilder.GetValidationData(info.ImplementationType);
            if (validationData?.ValidationMessages != null)
            {
                EditorGUILayout.Space();
                foreach (var (message, type) in validationData.ValidationMessages)
                {
                    // Only show messages about async dependencies or circular dependencies
                    if (message.Contains("async dependencies") || message.Contains("circular dependency"))
                    {
                        EditorGUILayout.HelpBox(message, type);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDependencyNode(ServiceTypeCacheBuilder.DependencyNode node, int depth)
        {
            if (node == null) return;

            var originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = originalIndent + depth;
            
            EditorGUILayout.BeginHorizontal();
            
            // Draw the type name with proper formatting
            var label = node.IsInterface ? $"<i>{node.Type.Name}</i>" : node.Type.Name;
            if (node.IsImplementation)
            {
                label = $"[Impl] {label}";
            }
            // Add [Async] tag if type implements IServiceInitializable
            if (typeof(IServiceInitializable).IsAssignableFrom(node.Type))
            {
                label = $"{label} [Async]";
            }
            EditorGUILayout.LabelField(label, new GUIStyle(EditorStyles.label) { richText = true });
            
            EditorGUILayout.EndHorizontal();

            // Draw child dependencies
            foreach (var child in node.Dependencies)
            {
                DrawDependencyNode(child, depth + 1);
            }

            EditorGUI.indentLevel = originalIndent; // Restore original indentation
        }

        private void DrawDetailRow(string label, string value, System.Action onClick = null, string tooltip = null)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Get the current line rect
            Rect lineRect = EditorGUILayout.GetControlRect(true, 18f);
            
            // Label section (0-140 pixels)
            Rect labelRect = new Rect(lineRect.x, lineRect.y, 140f, lineRect.height);
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
            
            // Value section - different positions for different types of fields
            float xOffset;
            if (label == "Service Name")
                xOffset = 215f;
            else if (label == "Interface" || label == "Implementation")
                xOffset = 215f;  // Extra offset for interface and implementation
            else
                xOffset = 200f;

            Rect valueRect = new Rect(lineRect.x + xOffset, lineRect.y, lineRect.width - xOffset, lineRect.height);
            var content = tooltip != null ? new GUIContent(value, tooltip) : new GUIContent(value);

            if (onClick != null)
            {
                if (GUI.Button(valueRect, content, EditorStyles.linkLabel))
                {
                    onClick();
                }
            }
            else if (label == "Service Name")
            {
                if (GUI.Button(valueRect, content, EditorStyles.label))
                {
                    GUIUtility.systemCopyBuffer = value;
                    Debug.Log($"Copied service name to clipboard: {value}");
                }
            }
            else
            {
                EditorGUI.LabelField(valueRect, content);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFileLocation(string scriptPath, Type implementationType)
        {
            if (string.IsNullOrEmpty(scriptPath)) return;

            EditorGUILayout.BeginHorizontal();
            
            // Get the current line rect
            Rect lineRect = EditorGUILayout.GetControlRect(true, 18f);
            
            // Label section
            Rect labelRect = new Rect(lineRect.x, lineRect.y, 140f, lineRect.height);
            EditorGUI.LabelField(labelRect, "File Location", EditorStyles.boldLabel);
            
            // Value section (extra offset for file location)
            Rect valueRect = new Rect(lineRect.x + 215f, lineRect.y, lineRect.width - 220f, lineRect.height);
            if (GUI.Button(valueRect, scriptPath, EditorStyles.linkLabel))
            {
                PingScript(implementationType);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private Color GetServiceTypeColor(ServiceType type)
        {
            return type switch
            {
                ServiceType.MonoBehaviour => new Color(0.4f, 0.8f, 0.4f),
                ServiceType.ScriptableObject => new Color(0.6f, 0.6f, 1f), // Lighter blue color
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Expand All"))
            {
                for (int i = 0; i < _serviceTypeFoldouts.Length; i++)
                    _serviceTypeFoldouts[i] = true;
            }
            
            if (GUILayout.Button("Collapse All"))
            {
                for (int i = 0; i < _serviceTypeFoldouts.Length; i++)
                    _serviceTypeFoldouts[i] = false;
            }

            GUILayout.FlexibleSpace(); // Add space between button groups

            // Add rebuild button with a different style to make it stand out
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.85f, 1f); // Light blue tint
            if (GUILayout.Button(new GUIContent("Rebuild Type Cache", "Rebuild the service type cache to reflect latest code changes")))
            {
                if (EditorUtility.DisplayDialog("Rebuild Type Cache", 
                    "Are you sure you want to rebuild the service type cache?\nThis will rescan all assemblies for service types.", 
                    "Rebuild", "Cancel"))
                {
                    ServiceTypeCacheBuilder.RebuildTypeCache();
                    // Force inspector refresh
                    Repaint();
                    // Reset our state
                    InitializeServiceTypeFoldouts();
                    _isLoading = true;
                }
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();
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

        private string GetTypeDescription(Type type)
        {
            if (type == null) return null;
            
            // Try to get XML documentation
            var assembly = type.Assembly;
            var memberInfo = type;
            var docProvider = typeof(UnityEditor.Editor).Assembly
                .GetType("UnityEditor.ScriptAttributeUtility")?
                .GetProperty("documentationProvider", BindingFlags.NonPublic | BindingFlags.Static)?
                .GetValue(null);

            if (docProvider != null)
            {
                var getDocMethod = docProvider.GetType().GetMethod("GetDocumentation");
                if (getDocMethod != null)
                {
                    var doc = getDocMethod.Invoke(docProvider, new object[] { memberInfo }) as string;
                    if (!string.IsNullOrEmpty(doc))
                    {
                        return doc;
                    }
                }
            }

            // Fallback to checking for Description attribute
            var descAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return descAttr?.Description;
        }

    

        private bool IsService(Type type)
        {
            if (type == null) return false;
            var registry = target as ServiceTypeCache;
            return registry != null && registry.GetTypeInfo(type) != null;
        }

        private string GetScriptPath(Type type)
        {
            if (type == null) return null;

            // Use cached path if available
            if (_typeToScriptPathCache.TryGetValue(type, out var path))
            {
                return path;
            }

            // If not in cache, try to find and cache it
            CacheTypeLocation(type);
            return _typeToScriptPathCache.GetValueOrDefault(type);
        }

        private void OpenCodeEditorAtType(Type type)
        {
            if (type == null) return;

            var path = GetScriptPath(type);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // Convert to absolute path
                string fullPath = Path.GetFullPath(path);
                
                // Try MonoScript first
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    AssetDatabase.OpenAsset(script);
                }
                else
                {
                    // For external files, use the system's default editor
                    System.Diagnostics.Process.Start(fullPath);
                }

                // Try to navigate to the correct line
                if (_typeToLineNumberCache.TryGetValue(type, out int line))
                {
                    // Wait a bit for the editor to open
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            var codeEditor = typeof(EditorWindow).Assembly.GetType("UnityEditor.CodeEditor.CodeEditor");
                            var currentEditor = codeEditor?.GetProperty("CurrentEditor", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                            
                            if (currentEditor != null)
                            {
                                var gotoLineMethod = currentEditor.GetType().GetMethod("SyncToLine");
                                if (gotoLineMethod != null)
                                {
                                    gotoLineMethod.Invoke(currentEditor, new object[] { line });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Failed to navigate to line {line}: {ex.Message}");
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to open file {path}: {ex.Message}");
            }
        }

        private void PingScript(Type type)
        {
            var path = GetScriptPath(type);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // Try as MonoScript first
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    EditorGUIUtility.PingObject(script);
                }
                else
                {
                    // For regular .cs files
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                    else
                    {
                        // If we can't ping it, at least select it in the project window
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to ping script {path}: {ex.Message}");
            }
        }

        private void CheckAndRefreshScriptCache()
        {
            var registry = target as ServiceTypeCache;
            if (registry == null) return;

            string assetPath = AssetDatabase.GetAssetPath(registry);
            System.DateTime currentModTime = System.IO.File.GetLastWriteTime(assetPath);

            bool needsRefresh = false;

            // Check if this is a different asset than last time
            if (_lastCachedAssetPath != assetPath)
            {
                needsRefresh = true;
                _lastCachedAssetPath = assetPath;
            }
            // Check if the asset has been modified
            else if (currentModTime != _lastAssetModificationTime)
            {
                needsRefresh = true;
            }
            // Check if cache was invalidated by domain reload
            else if (!_pathCacheInitialized)
            {
                needsRefresh = true;
            }

            if (needsRefresh)
            {
                RefreshScriptCache();
                _lastAssetModificationTime = currentModTime;
                _pathCacheInitialized = true;
            }
        }

        private void RefreshScriptCache()
        {
            _typeToScriptPathCache.Clear();
            _typeToLineNumberCache.Clear();

            // Find all .cs files in the project
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            var csFileGuids = AssetDatabase.FindAssets("t:TextAsset")
                .Where(guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(".cs"));
            
            _allScriptPaths = new HashSet<string>();
            
            // Add MonoScript paths
            foreach (var guid in scriptGuids)
            {
                _allScriptPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            
            // Add regular .cs file paths
            foreach (var guid in csFileGuids)
            {
                _allScriptPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            // Cache type locations for all service types
            var registry = target as ServiceTypeCache;
            if (registry != null)
            {
                foreach (var serviceInfo in registry.ServiceTypes)
                {
                    CacheTypeLocation(serviceInfo.InterfaceType);
                    CacheTypeLocation(serviceInfo.ImplementationType);
                }
            }
        }

        private void CacheTypeLocation(Type type)
        {
            if (type == null || _typeToScriptPathCache.ContainsKey(type)) return;

            // Get the full type name including namespace
            string fullTypeName = type.FullName;
            if (string.IsNullOrEmpty(fullTypeName)) return;

            // Handle nested types
            if (type.IsNested)
            {
                fullTypeName = type.DeclaringType.FullName + "+" + type.Name;
            }

            // First try to find by assembly name to narrow down search
            string assemblyName = type.Assembly.GetName().Name;
            bool foundInAssembly = false;

            // First try to find in the expected assembly paths
            var assemblyPaths = _allScriptPaths.Where(p => p.Contains(assemblyName));
            foreach (var path in assemblyPaths)
            {
                if (TryFindTypeInFile(type, path))
                {
                    foundInAssembly = true;
                    break;
                }
            }

            // If not found in assembly-matched files, try all files without logging
            if (!foundInAssembly)
            {
                foreach (var path in _allScriptPaths)
                {
                    if (TryFindTypeInFile(type, path))
                    {
                        return;
                    }
                }
                
                // Only log warning if we completely failed to find the type
                Debug.LogWarning($"Could not find source file for type {type.FullName}");
            }
        }

        private bool TryFindTypeInFile(Type type, string path)
        {
            try
            {
                // Try MonoScript first
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    var scriptClass = script.GetClass();
                    if (scriptClass != null)
                    {
                        // First check if this is the exact script we're looking for
                        if (scriptClass == type)
                        {
                            CacheTypeInFile(type, path, script.text);
                            return true;
                        }

                        // If not exact match, check if type is defined in this script's assembly
                        if (scriptClass.Assembly == type.Assembly)
                        {
                            var content = script.text;
                            if (IsTypeDefinedInContent(type, content))
                            {
                                CacheTypeInFile(type, path, content);
                                return true;
                            }
                        }
                    }
                }
                else if (path.EndsWith(".cs"))
                {
                    var content = File.ReadAllText(path);
                    if (IsTypeDefinedInContent(type, content))
                    {
                        CacheTypeInFile(type, path, content);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Only log if it's not a file access issue
                if (!(ex is IOException || ex is UnauthorizedAccessException))
                {
                    Debug.LogWarning($"Error processing file {path} for type {type.FullName}: {ex.Message}");
                }
            }
            return false;
        }

        private bool IsTypeDefinedInContent(Type type, string content)
        {
            try
            {
                // Get precise type declaration pattern
                string typePattern = type.IsInterface ? "interface" : "class";
                string typeDecl = $"{typePattern} {type.Name}";
                
                // Get namespace declaration if any
                string namespaceDecl = type.Namespace != null ? $"namespace {type.Namespace}" : null;

                var lines = content.Split('\n');
                bool inCorrectNamespace = namespaceDecl == null;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    
                    // Track namespace context
                    if (namespaceDecl != null && line.StartsWith("namespace"))
                    {
                        inCorrectNamespace = line.Contains(type.Namespace);
                        continue;
                    }

                    // Only look for type declaration in correct namespace
                    if (inCorrectNamespace)
                    {
                        // Check for exact type declaration match
                        if (line.Contains(typeDecl))
                        {
                            // Verify it's an actual type declaration
                            if (line.StartsWith("public") || line.StartsWith("internal") || 
                                line.StartsWith("private") || line.StartsWith(typePattern))
                            {
                                _typeToLineNumberCache[type] = i + 1;
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error checking type {type.FullName} in content: {ex.Message}");
            }
            return false;
        }

        private void CacheTypeInFile(Type type, string path, string content)
        {
            _typeToScriptPathCache[type] = path;
            
            try
            {
                // Get more precise type declaration pattern
                string typePattern = type.IsInterface ? "interface" : "class";
                string typeDecl = $"{typePattern} {type.Name}";
                
                var lines = content.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    
                    // Check for exact type declaration match
                    if (line.Contains(typeDecl) &&
                        (line.StartsWith("public") || line.StartsWith("internal") || line.StartsWith("private") ||
                         line.StartsWith(typePattern)))
                    {
                        _typeToLineNumberCache[type] = i + 1;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error finding line number for type {type.Name}: {ex.Message}");
            }
        }

        private bool PassesFilters(ServiceTypeInfo serviceInfo)
        {
            // Type filter
            if (_typeFilter != ServiceTypeFilter.All && (int)serviceInfo.ServiceType != (int)_typeFilter)
                return false;

            // Context filter
            if (_contextFilter != ServiceContextFilter.All && serviceInfo.DefaultContext != (ServiceContext)_contextFilter)
                return false;

            // Search filter
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                var searchLower = _searchQuery.ToLower();
                var validationData = ServiceTypeCacheBuilder.GetValidationData(serviceInfo.ImplementationType);
                bool hasCircular = validationData?.HasCircularDependency ?? false;

                return serviceInfo.InterfaceTypeName.ToLower().Contains(searchLower) ||
                       serviceInfo.ImplementationTypeName.ToLower().Contains(searchLower) ||
                       serviceInfo.DefaultName.ToLower().Contains(searchLower) ||
                       (hasCircular && "circular".Contains(searchLower));
            }

            return true;
        }

        private string GetAssemblyTypeTooltip(ServiceAssemblyType assemblyType)
        {
            switch (assemblyType)
            {
                case ServiceAssemblyType.Runtime:
                    return "Service is in a runtime assembly.\n" +
                           "Can be used in builds and editor.\n" +
                           "Cannot be marked as EditorOnly.";

                case ServiceAssemblyType.Editor:
                    return "Service is in an editor assembly.\n" +
                           "Must be marked as EditorOnly.\n" +
                           "Cannot be used in builds.";

                case ServiceAssemblyType.Test:
                    return "Service is in a test assembly.\n" +
                           "Can use any context.\n" +
                           "Not included in builds.";

                default:
                    return assemblyType.ToString();
            }
        }
    }
} 