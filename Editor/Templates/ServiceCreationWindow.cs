using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using GAOS.ServiceLocator;

namespace GAOS.ServiceLocator.Editor
{
    [System.Serializable]
    public class ServiceCreationData
    {
        public string ServiceName;
        public string Namespace;
        public string Description;
        public ServiceType ServiceType;
        public bool UseExistingInterface;
        public string SelectedInterface;
        public bool InterfaceOnly;
        public bool AutoRegister;
        public ServiceContext RegistrationContext;
        public ServiceLifetime ServiceLifetime;

        public ServiceCreationData()
        {
            // Set defaults
            ServiceType = ServiceType.Regular;
            AutoRegister = true;
            RegistrationContext = ServiceContext.Runtime;
            ServiceLifetime = ServiceLifetime.Singleton;
        }

        public ServiceCreationData Clone()
        {
            return new ServiceCreationData
            {
                ServiceName = this.ServiceName,
                Namespace = this.Namespace,
                Description = this.Description,
                ServiceType = this.ServiceType,
                UseExistingInterface = this.UseExistingInterface,
                SelectedInterface = this.SelectedInterface,
                InterfaceOnly = this.InterfaceOnly,
                AutoRegister = this.AutoRegister,
                RegistrationContext = this.RegistrationContext,
                ServiceLifetime = this.ServiceLifetime
            };
        }
    }

    public class ServiceCreationWindow : EditorWindow
    {
        private enum EditorState
        {
            Creation,
            Review
        }

        [MenuItem("GAOS/Service Locator/Create New Service", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<ServiceCreationWindow>("Create Service");
            window.minSize = new Vector2(450, 500);

            // If window already exists and has pending services, ask if user wants to restore
            if (window._pendingServices != null && window._pendingServices.Count > 0)
            {
                bool shouldRestore = EditorUtility.DisplayDialog(
                    "Restore Services",
                    $"Found {window._pendingServices.Count} service(s) from previous session. Do you want to restore them?",
                    "Restore",
                    "Start Fresh"
                );

                if (!shouldRestore)
                {
                    window._pendingServices.Clear();
                    window._currentService = new ServiceCreationData();
                    window._selectedServiceIndex = -1;
                    window._currentState = EditorState.Creation;
                }
                else
                {
                    window._currentState = EditorState.Review;
                }
            }

            window.Show();
        }

        // Add state management
        private EditorState _currentState = EditorState.Creation;
        private List<ServiceCreationData> _pendingServices = new List<ServiceCreationData>();
        private ServiceCreationData _currentService = new ServiceCreationData();
        private int _selectedServiceIndex = -1;
        private string _serviceName = "";
        private string _namespace = "";
        private string _description = "";
        private ServiceType _serviceType = ServiceType.Regular;
        private string _targetDirectory = "Assets/Scripts/Services";
        private Vector2 _scrollPosition;
        
        // Interface settings
        private bool _useExistingInterface;
        private string _selectedInterface;
        private List<string> _availableInterfaces = new List<string>();
        private string _interfaceSearchQuery = "";
        private Vector2 _interfaceListScrollPosition;
        private bool _showInterfaceSelector;
        private List<string> _filteredInterfaces = new List<string>();
        private const int MAX_VISIBLE_INTERFACES = 10;

        // New fields
        private bool _autoRegister = true;
        private ServiceContext _registrationContext = ServiceContext.Runtime;
        private ServiceLifetime _serviceLifetime = ServiceLifetime.Singleton;

        // Add new fields for namespace selection
        private bool _useExistingNamespace;
        private string _selectedNamespace;
        private List<string> _availableNamespaces = new List<string>();
        private string _namespaceSearchQuery = "";
        private Vector2 _namespaceListScrollPosition;
        private bool _showNamespaceSelector;
        private List<string> _filteredNamespaces = new List<string>();

        // Tooltips
        private static readonly GUIContent NamespaceContent = new GUIContent("Namespace (Optional)", 
            "The namespace for the service (e.g. MyCompany.MyGame.Services). If left empty, the service will be created in the global namespace.");
        
        private static readonly GUIContent ServiceTypeContent = new GUIContent("Service Type",
            "Regular: Standard C# class\nMonoBehaviour: Unity component that can be attached to GameObjects\nScriptableObject: Asset-based service that can be configured in the Unity Inspector");
        
        private static readonly GUIContent UseExistingInterfaceContent = new GUIContent("Use Existing Interface",
            "Select an existing interface instead of creating a new one");
        
        private static readonly GUIContent AutoRegisterContent = new GUIContent("Auto Register Service",
            "Automatically register the service with ServiceLocator on initialization");
        
        private static readonly GUIContent RegistrationContextContent = new GUIContent("Registration Context",
            "Runtime: Service is available only in play mode\nRuntimeAndEditor: Service is available in both editor and play mode\nEditorOnly: Service is available only in editor");
        
        private static readonly GUIContent ServiceLifetimeContent = new GUIContent("Service Lifetime",
            "Singleton: One instance shared across all requests\nTransient: New instance created for each request");

        // Add new field at the top with other fields
        private bool _interfaceOnly;

        // Add new tooltip with other tooltips
        private static readonly GUIContent InterfaceOnlyContent = new GUIContent("Interface Only",
            "Create only the interface file without implementation");

        // Add new state management
        private Vector2 _reviewScrollPosition;

        private string _lastValidatedServiceName;
        private string _lastValidatedNamespace;
        private bool _isValidationScheduled;
        private ValidationResult _currentValidation = new ValidationResult();
        private double _lastValidationRequestTime;
        private const double VALIDATION_DELAY = 0.5; // Delay in seconds

        private class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public string Message { get; set; } = "✓ Valid service name";
            public bool ShowMessage { get; set; } = false;
        }

        // Add new cache fields
        private class TypeCache
        {
            public Dictionary<string, HashSet<string>> NamespaceToTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> AllTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        private TypeCache _typeCache;

        private void BuildTypeCache()
        {
            _typeCache = new TypeCache();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Namespace != null)
                        {
                            if (!_typeCache.NamespaceToTypes.TryGetValue(type.Namespace, out var typeSet))
                            {
                                typeSet = new HashSet<string>();
                                _typeCache.NamespaceToTypes[type.Namespace] = typeSet;
                            }
                            typeSet.Add(type.Name);
                        }
                        _typeCache.AllTypeNames.Add(type.Name);
                    }
                }
                catch (Exception) { /* Skip problematic assemblies */ }
            }
        }

        private void OnEnable()
        {
            BuildTypeCache();
            RefreshAvailableInterfaces();
            RefreshAvailableNamespaces();
        }

        private void RefreshAvailableInterfaces()
        {
            _availableInterfaces.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var interfaces = assembly.GetTypes()
                        .Where(t => t.IsInterface && !t.IsGenericType)
                        .Select(t => t.FullName)
                        .ToList();
                    
                    _availableInterfaces.AddRange(interfaces);
                }
                catch (Exception) { /* Skip problematic assemblies */ }
            }
            
            _availableInterfaces.Sort();
            FilterInterfaces();
        }

        private void FilterInterfaces()
        {
            if (string.IsNullOrWhiteSpace(_interfaceSearchQuery))
            {
                _filteredInterfaces = _availableInterfaces.ToList();
                return;
            }

            _filteredInterfaces = _availableInterfaces
                .Where(i => i.IndexOf(_interfaceSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void RefreshAvailableNamespaces()
        {
            _availableNamespaces.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    var namespaces = types
                        .Select(t => t.Namespace)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();
                    
                    _availableNamespaces.AddRange(namespaces);
                }
                catch (Exception) { /* Skip problematic assemblies */ }
            }
            
            _availableNamespaces = _availableNamespaces.Distinct().OrderBy(n => n).ToList();
            FilterNamespaces();
        }

        private void FilterNamespaces()
        {
            if (string.IsNullOrWhiteSpace(_namespaceSearchQuery))
            {
                _filteredNamespaces = _availableNamespaces.ToList();
                return;
            }

            _filteredNamespaces = _availableNamespaces
                .Where(n => n.IndexOf(_namespaceSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_currentState)
            {
                case EditorState.Creation:
                    DrawCreationState();
                    break;
                case EditorState.Review:
                    DrawReviewState();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCreationState()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawMainForm();
            EditorGUILayout.Space(10);
            DrawInterfaceSettings();
            EditorGUILayout.Space(10);
            if (!_interfaceOnly)
            {
                DrawImplementationSettings();
                EditorGUILayout.Space(10);
            }
            DrawDirectorySettings();
            EditorGUILayout.Space(10);
            DrawCreationStateButtons();
        }

        private void DrawCreationStateButtons()
        {
            EditorGUILayout.BeginVertical();
            
            EditorGUI.BeginDisabledGroup(!IsValidInput());
            
            if (GUILayout.Button(_selectedServiceIndex >= 0 ? "Update Service" : "Add Service", GUILayout.Height(30)))
            {
                AddOrUpdateService();
                _currentState = EditorState.Review;
            }

            EditorGUI.EndDisabledGroup();

            if (!IsValidInput())
            {
                EditorGUILayout.HelpBox("Please fill in all required fields and ensure the service name is valid.", MessageType.Warning);
            }

            // Add a separator if we have pending services
            if (_pendingServices.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.Space(5);

                // Button to review pending services
                if (GUILayout.Button($"Review Pending Services ({_pendingServices.Count})", GUILayout.Height(25)))
                {
                    _currentState = EditorState.Review;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawReviewState()
        {
            EditorGUILayout.Space(10);
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("Review Services", headerStyle);
            EditorGUILayout.Space(10);

            _reviewScrollPosition = EditorGUILayout.BeginScrollView(_reviewScrollPosition);
            
            for (int i = 0; i < _pendingServices.Count; i++)
            {
                DrawServiceReviewItem(i);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Another Service", GUILayout.Height(30)))
            {
                _selectedServiceIndex = -1;
                _currentService = new ServiceCreationData();
                _currentState = EditorState.Creation;
            }
            
            if (_pendingServices.Count > 0)
            {
                if (GUILayout.Button("Confirm Writing", GUILayout.Height(30)))
                {
                    WriteAllServices();
                }

                if (GUILayout.Button("Clear All", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Clear All Services",
                        "Are you sure you want to clear all pending services? This cannot be undone.",
                        "Clear",
                        "Cancel"))
                    {
                        _pendingServices.Clear();
                        _currentService = new ServiceCreationData();
                        _selectedServiceIndex = -1;
                        _currentState = EditorState.Creation;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawServiceReviewItem(int index)
        {
            var service = _pendingServices[index];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Service: {service.ServiceName}", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Edit", GUILayout.Width(60)))
            {
                _selectedServiceIndex = index;
                _currentService = service.Clone();
                _currentState = EditorState.Creation;
            }
            
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Remove Service", 
                    $"Are you sure you want to remove {service.ServiceName}?", "Yes", "No"))
                {
                    _pendingServices.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Type: {(service.InterfaceOnly ? "Interface Only" : $"{service.ServiceType} Service")}");
            if (!string.IsNullOrEmpty(service.Namespace))
                EditorGUILayout.LabelField($"Namespace: {service.Namespace}");
            
            if (!service.InterfaceOnly)
            {
                if (service.UseExistingInterface)
                    EditorGUILayout.LabelField($"Using Interface: {service.SelectedInterface}");
                EditorGUILayout.LabelField($"Auto Register: {(service.AutoRegister ? "Yes" : "No")}");
                if (service.AutoRegister)
                {
                    EditorGUILayout.LabelField($"Context: {service.RegistrationContext}, Lifetime: {service.ServiceLifetime}");
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField("Service Creation", headerStyle);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Create a new service with interface and implementation.", MessageType.Info);
        }

        private GUIStyle GetValidationStyle(bool isValid)
        {
            return new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = isValid ? Color.green : Color.red }
            };
        }

        private void DrawMainForm()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Service Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Service Name field and validation
            EditorGUI.BeginChangeCheck();
            _serviceName = EditorGUILayout.TextField("Service Name", _serviceName);
            if (EditorGUI.EndChangeCheck())
            {
                ScheduleValidation();
            }
            
            if (_currentValidation.ShowMessage)
            {
                EditorGUILayout.LabelField(_currentValidation.Message, GetValidationStyle(_currentValidation.IsValid));
            }
            
            EditorGUILayout.Space(5);
            
            // Namespace section
            EditorGUILayout.BeginHorizontal();
            _useExistingNamespace = EditorGUILayout.Toggle("Use Existing Namespace", _useExistingNamespace);
            EditorGUILayout.EndHorizontal();

            if (_useExistingNamespace)
            {
                DrawNamespaceSelector();
            }
            else
            {
                // Manual namespace input and validation
                EditorGUI.BeginChangeCheck();
                var newNamespace = EditorGUILayout.TextField(NamespaceContent, _namespace);
                if (EditorGUI.EndChangeCheck())
                {
                    // Only allow valid C# namespace characters
                    _namespace = string.Join(".", newNamespace.Split('.')
                        .Select(part => new string(part.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray())));
                    ScheduleValidation();
                }

                if (!string.IsNullOrWhiteSpace(_namespace))
                {
                    bool isValidNamespace = _namespace.Split('.').All(part => 
                        !string.IsNullOrEmpty(part) && 
                        char.IsLetter(part[0]) && 
                        part.All(c => char.IsLetterOrDigit(c) || c == '_'));

                    EditorGUILayout.LabelField(
                        isValidNamespace ? 
                            "✓ Valid namespace" : 
                            "✗ Invalid namespace. Must be a valid C# namespace (e.g. MyCompany.MyGame.Services)",
                        GetValidationStyle(isValidNamespace)
                    );
                }
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.Height(60));

            EditorGUILayout.EndVertical();
        }

        private void DrawInterfaceSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _interfaceSearchQuery = EditorGUILayout.TextField("Search Interfaces", _interfaceSearchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                FilterInterfaces();
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshAvailableInterfaces();
                FilterInterfaces();
            }
            EditorGUILayout.EndHorizontal();

            // Selected interface display with button to open popup
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected Interface:", GUILayout.Width(120));
            EditorGUILayout.LabelField(_selectedInterface ?? "None", EditorStyles.boldLabel);
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                _showInterfaceSelector = !_showInterfaceSelector;
                if (_showInterfaceSelector)
                {
                    FilterInterfaces();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Popup list of filtered interfaces
            if (_showInterfaceSelector)
            {
                EditorGUILayout.Space(5);
                _interfaceListScrollPosition = EditorGUILayout.BeginScrollView(
                    _interfaceListScrollPosition, 
                    GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight * MAX_VISIBLE_INTERFACES)
                );

                foreach (var interfaceName in _filteredInterfaces)
                {
                    if (GUILayout.Button(interfaceName, EditorStyles.label))
                    {
                        _selectedInterface = interfaceName;
                        _showInterfaceSelector = false;
                    }
                }

                EditorGUILayout.EndScrollView();

                // Show count of filtered results
                EditorGUILayout.LabelField(
                    $"Showing {_filteredInterfaces.Count} of {_availableInterfaces.Count} interfaces",
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNamespaceSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _namespaceSearchQuery = EditorGUILayout.TextField("Search Namespaces", _namespaceSearchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                FilterNamespaces();
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshAvailableNamespaces();
                FilterNamespaces();
            }
            EditorGUILayout.EndHorizontal();

            // Selected namespace display with button to open popup
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected Namespace:", GUILayout.Width(120));
            EditorGUILayout.LabelField(_selectedNamespace ?? "None", EditorStyles.boldLabel);
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                _showNamespaceSelector = !_showNamespaceSelector;
                if (_showNamespaceSelector)
                {
                    FilterNamespaces();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Popup list of filtered namespaces
            if (_showNamespaceSelector)
            {
                EditorGUILayout.Space(5);
                _namespaceListScrollPosition = EditorGUILayout.BeginScrollView(
                    _namespaceListScrollPosition, 
                    GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight * MAX_VISIBLE_INTERFACES)
                );

                foreach (var namespaceName in _filteredNamespaces)
                {
                    if (GUILayout.Button(namespaceName, EditorStyles.label))
                    {
                        _selectedNamespace = namespaceName;
                        _namespace = namespaceName; // Update the actual namespace field
                        _showNamespaceSelector = false;
                    }
                }

                EditorGUILayout.EndScrollView();

                // Show count of filtered results
                EditorGUILayout.LabelField(
                    $"Showing {_filteredNamespaces.Count} of {_availableNamespaces.Count} namespaces",
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawInterfaceSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Interface Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            _interfaceOnly = EditorGUILayout.Toggle(InterfaceOnlyContent, _interfaceOnly);
            if (EditorGUI.EndChangeCheck() && _interfaceOnly)
            {
                // Reset interface selection when switching to interface only mode
                _useExistingInterface = false;
                _selectedInterface = null;
            }

            if (!_interfaceOnly)
            {
                _useExistingInterface = EditorGUILayout.Toggle(UseExistingInterfaceContent, _useExistingInterface);

                if (_useExistingInterface)
                {
                    DrawInterfaceSelector();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawImplementationSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Implementation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _serviceType = (ServiceType)EditorGUILayout.EnumPopup(ServiceTypeContent, _serviceType);
            
            EditorGUILayout.Space(5);
            _autoRegister = EditorGUILayout.Toggle(AutoRegisterContent, _autoRegister);

            if (_autoRegister)
            {
                EditorGUI.indentLevel++;
                _registrationContext = (ServiceContext)EditorGUILayout.EnumPopup(
                    RegistrationContextContent, _registrationContext);
                _serviceLifetime = (ServiceLifetime)EditorGUILayout.EnumPopup(
                    ServiceLifetimeContent, _serviceLifetime);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectorySettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Directory Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            _targetDirectory = EditorGUILayout.TextField("Scripts Directory", _targetDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string dir = EditorUtility.OpenFolderPanel("Select Scripts Directory", "Assets", "");
                if (!string.IsNullOrEmpty(dir))
                {
                    _targetDirectory = GetRelativePath(dir);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private bool IsValidInput()
        {
            if (string.IsNullOrWhiteSpace(_serviceName)) return false;
            if (_useExistingInterface && string.IsNullOrWhiteSpace(_selectedInterface)) return false;
            if (string.IsNullOrWhiteSpace(_targetDirectory)) return false;

            // Check if service name is a valid C# identifier
            if (!System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(_serviceName))
                return false;

            // Check for duplicate service names
            if (HasDuplicateServiceName(_serviceName))
                return false;

            // Check for namespace conflicts
            if (HasNamespaceConflict(_serviceName, _namespace))
                return false;

            // Check if service name starts with 'I'
            if (_serviceName.StartsWith("I") && _serviceName.Length > 1 && char.IsUpper(_serviceName[1]))
                return false;

            // Check for confusing name patterns
            if (HasConfusingNamePattern(_serviceName, out _))
                return false;

            // Validate namespace format if provided
            if (!string.IsNullOrWhiteSpace(_namespace))
            {
                if (!_namespace.Split('.').All(part => 
                    !string.IsNullOrEmpty(part) && 
                    char.IsLetter(part[0]) && 
                    part.All(c => char.IsLetterOrDigit(c) || c == '_')))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasDuplicateServiceName(string serviceName)
        {
            if (_selectedServiceIndex >= 0)
            {
                // If we're editing an existing service, exclude it from the check
                return _pendingServices
                    .Where((_, index) => index != _selectedServiceIndex)
                    .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            }
            
            // Check for duplicates in pending services
            return _pendingServices.Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasNamespaceConflict(string serviceName, string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
                return false;

            // Use cached types instead of scanning assemblies
            if (_typeCache.NamespaceToTypes.TryGetValue(namespaceName, out var typesInNamespace))
            {
                // Check for interface name conflict (case-insensitive)
                var interfaceName = $"I{serviceName}";
                if (typesInNamespace.Any(t => t.Equals(interfaceName, StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Check for service name conflict (case-insensitive)
                var serviceSuffixName = $"{serviceName}Service";
                if (typesInNamespace.Any(t => t.Equals(serviceSuffixName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Check pending services in the same namespace
            var pendingServicesInNamespace = _pendingServices
                .Where(s => string.Equals(s.Namespace, namespaceName, StringComparison.OrdinalIgnoreCase) && 
                           (_selectedServiceIndex < 0 || _pendingServices.IndexOf(s) != _selectedServiceIndex));

            return pendingServicesInNamespace.Any(s => 
                s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasConfusingNamePattern(string serviceName, out string reason)
        {
            reason = null;

            if (serviceName.StartsWith("i", StringComparison.OrdinalIgnoreCase) && 
                serviceName.Length > 1 && char.IsUpper(serviceName[1]))
            {
                reason = $"This name will result in interface 'I{serviceName}' (I{serviceName[0]}{serviceName.Substring(1)}) " +
                        $"and implementation '{serviceName}Service'";
                return true;
            }

            if (serviceName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"This name will result in implementation '{serviceName}Service' (double 'Service' suffix)";
                return true;
            }

            if (serviceName.EndsWith("Interface", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"This name will result in interface 'I{serviceName}' ('Interface' in interface name)";
                return true;
            }

            return false;
        }

        private void ResetForm()
        {
            _currentService = new ServiceCreationData();
            _serviceName = "";
            _description = "";
            _useExistingInterface = false;
            _interfaceOnly = false;
            _selectedInterface = null;
            _interfaceSearchQuery = "";
            _showInterfaceSelector = false;
            _autoRegister = true;
            _registrationContext = ServiceContext.Runtime;
            _serviceLifetime = ServiceLifetime.Singleton;
            _serviceType = ServiceType.Regular;
            
            _useExistingNamespace = false;
            _namespaceSearchQuery = "";
            _showNamespaceSelector = false;
        }

        private void AddOrUpdateService()
        {
            var serviceData = new ServiceCreationData
            {
                ServiceName = _serviceName,
                Namespace = _namespace,
                Description = _description,
                ServiceType = _serviceType,
                UseExistingInterface = _useExistingInterface,
                SelectedInterface = _selectedInterface,
                InterfaceOnly = _interfaceOnly,
                AutoRegister = _autoRegister,
                RegistrationContext = _registrationContext,
                ServiceLifetime = _serviceLifetime
            };

            if (_selectedServiceIndex >= 0)
            {
                _pendingServices[_selectedServiceIndex] = serviceData;
                _selectedServiceIndex = -1;
            }
            else
            {
                _pendingServices.Add(serviceData);
            }
        }

        private void WriteAllServices()
        {
            // Collect all files that would be created/overwritten
            var filesToCheck = new List<string>();
            foreach (var service in _pendingServices)
            {
                if (!service.UseExistingInterface)
                {
                    filesToCheck.Add(Path.Combine(_targetDirectory, $"I{service.ServiceName}.cs"));
                }
                if (!service.InterfaceOnly)
                {
                    filesToCheck.Add(Path.Combine(_targetDirectory, $"{service.ServiceName}Service.cs"));
                }
            }

            // Check for overwrites
            if (!ServiceTemplateManager.CheckAndConfirmFileOverwrite(filesToCheck.ToArray()))
            {
                return;
            }

            // Create all services
            foreach (var service in _pendingServices)
            {
                if (!service.UseExistingInterface)
                {
                    ServiceTemplateManager.CreateServiceInterface(
                        _targetDirectory,
                        service.Namespace,
                        service.ServiceName,
                        service.Description);
                }

                if (!service.InterfaceOnly)
                {
                    string interfaceName = service.UseExistingInterface ? 
                        service.SelectedInterface.Split('.').Last() : 
                        $"I{service.ServiceName}";

                    ServiceTemplateManager.CreateServiceImplementation(
                        _targetDirectory,
                        service.Namespace,
                        $"{service.ServiceName}Service",
                        service.UseExistingInterface ? service.SelectedInterface : interfaceName,
                        service.Description,
                        service.ServiceType,
                        service.AutoRegister,
                        service.RegistrationContext,
                        service.ServiceLifetime,
                        !service.UseExistingInterface
                    );
                }
            }

            EditorUtility.DisplayDialog(
                "Services Created",
                $"Successfully created {_pendingServices.Count} service(s).",
                "OK");

            // Clear everything
            _pendingServices.Clear();
            _currentService = new ServiceCreationData();
            _selectedServiceIndex = -1;
            ResetForm();
        }

        private string GetRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }

        private void OnDestroy()
        {
            // Cancel any pending validation
            if (_isValidationScheduled)
            {
                EditorApplication.delayCall -= ValidateWithDelay;
            }

            if (_pendingServices != null && _pendingServices.Count > 0)
            {
                bool shouldSave = EditorUtility.DisplayDialog(
                    "Unsaved Services",
                    $"You have {_pendingServices.Count} unsaved service(s) in the buffer. Do you want to save them before closing?",
                    "Save",
                    "Discard"
                );

                if (shouldSave)
                {
                    WriteAllServices();
                }
            }
        }

        private void ScheduleValidation()
        {
            // Cancel any pending validation
            if (_isValidationScheduled)
            {
                EditorApplication.delayCall -= ValidateWithDelay;
            }

            _lastValidationRequestTime = EditorApplication.timeSinceStartup;
            _isValidationScheduled = true;
            EditorApplication.delayCall += ValidateWithDelay;
        }

        private ValidationResult QuickValidate(string serviceName)
        {
            var result = new ValidationResult { ShowMessage = true };

            // Quick checks first (no heavy operations)
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                result.IsValid = false;
                result.Message = "✗ Service name cannot be empty";
                return result;
            }

            // Basic character validation
            if (serviceName.Length > 0)
            {
                if (!char.IsLetter(serviceName[0]) && serviceName[0] != '_')
                {
                    result.IsValid = false;
                    result.Message = "✗ Service name must start with a letter or underscore";
                    return result;
                }

                if (serviceName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                {
                    result.IsValid = false;
                    result.Message = "✗ Service name can only contain letters, numbers, and underscores";
                    return result;
                }
            }

            // Quick pattern checks
            if (serviceName.StartsWith("I") && serviceName.Length > 1 && char.IsUpper(serviceName[1]))
            {
                result.IsValid = false;
                result.Message = "✗ Service name should not start with 'I' (interface naming pattern)";
                return result;
            }

            return result;
        }

        private void ValidateWithDelay()
        {
            // If not enough time has passed since the last request, reschedule
            double timeSinceLastRequest = EditorApplication.timeSinceStartup - _lastValidationRequestTime;
            if (timeSinceLastRequest < VALIDATION_DELAY)
            {
                EditorApplication.delayCall -= ValidateWithDelay;
                EditorApplication.delayCall += ValidateWithDelay;
                return;
            }

            _isValidationScheduled = false;

            // Only validate if the values have changed since last validation
            if (_serviceName == _lastValidatedServiceName && _namespace == _lastValidatedNamespace)
                return;

            _lastValidatedServiceName = _serviceName;
            _lastValidatedNamespace = _namespace;

            // Do quick validation first
            var quickResult = QuickValidate(_serviceName);
            if (!quickResult.IsValid)
            {
                _currentValidation = quickResult;
                return;
            }

            // Only proceed with full validation if quick validation passes
            _currentValidation = ValidateServiceName(_serviceName, _namespace);
        }

        private ValidationResult ValidateServiceName(string serviceName, string namespaceName)
        {
            var result = new ValidationResult { ShowMessage = true };

            // Skip basic checks as they're done in QuickValidate
            bool hasDuplicate = HasDuplicateServiceName(serviceName);
            bool hasNamespaceConflict = HasNamespaceConflict(serviceName, namespaceName);
            bool hasConfusingPattern = HasConfusingNamePattern(serviceName, out string confusingReason);

            if (hasDuplicate)
            {
                result.IsValid = false;
                result.Message = "✗ A service with this name already exists in the buffer";
            }
            else if (hasNamespaceConflict)
            {
                result.IsValid = false;
                result.Message = "✗ A service or interface with this name already exists in the specified namespace";
            }
            else if (hasConfusingPattern)
            {
                result.IsValid = false;
                result.Message = $"✗ Potentially confusing name: {confusingReason}";
            }

            return result;
        }
    }
} 