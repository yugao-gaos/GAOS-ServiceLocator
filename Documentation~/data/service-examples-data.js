const SERVICE_EXAMPLES = {
    "examples": [
        {
            "name": "Game Settings Manager",
            "description": "Manages and persists player preferences, graphics settings, and control configurations",
            "category": "Configuration",
            "complexity": "Medium",
            "has_state": true,
            "unity_integration": "config",
            "context": "runtime_and_editor",
            "lifetime": "singleton",
            "tags": ["settings", "configuration", "persistence"],
            "code": {
                "interface": "public interface IGameSettings\n{\n    float MasterVolume { get; set; }\n    bool IsFullscreen { get; set; }\n    void SaveSettings();\n    void LoadSettings();\n}",
                "implementation": "[Service(typeof(IGameSettings), \"GameSettings\", ServiceLifetime.Singleton, ServiceContext.RuntimeAndEditor)]\npublic class GameSettings : SOServiceBase, IGameSettings\n{\n    [SerializeField] private float _masterVolume = 1.0f;\n    [SerializeField] private bool _isFullscreen = true;\n\n    public float MasterVolume\n    {\n        get => _masterVolume;\n        set\n        {\n            _masterVolume = value;\n            AudioListener.volume = value;\n        }\n    }\n\n    public bool IsFullscreen\n    {\n        get => _isFullscreen;\n        set\n        {\n            _isFullscreen = value;\n            Screen.fullScreen = value;\n        }\n    }\n\n    public void SaveSettings()\n    {\n        PlayerPrefs.SetFloat(\"MasterVolume\", _masterVolume);\n        PlayerPrefs.SetInt(\"Fullscreen\", _isFullscreen ? 1 : 0);\n        PlayerPrefs.Save();\n    }\n\n    public void LoadSettings()\n    {\n        MasterVolume = PlayerPrefs.GetFloat(\"MasterVolume\", 1.0f);\n        IsFullscreen = PlayerPrefs.GetInt(\"Fullscreen\", 1) == 1;\n    }\n\n    protected override void Initialize()\n    {\n        LoadSettings();\n    }\n}"
            }
        },
        {
            "name": "Player Progress Tracker",
            "description": "Tracks and saves player achievements, inventory, and game progression",
            "category": "Game Progress",
            "complexity": "High",
            "has_state": true,
            "unity_integration": "none",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["save", "progression", "achievements"],
            "code": {
                "interface": "public interface IProgressTracker\n{\n    // Progress tracking\n    float GetProgress(string key);\n    void SetProgress(string key, float value);\n    bool IsCompleted(string key);\n\n    // Achievement system\n    void UnlockAchievement(string achievementId);\n    bool IsAchievementUnlocked(string achievementId);\n    IReadOnlyList<string> GetUnlockedAchievements();\n\n    // Inventory management\n    void AddItem(string itemId, int amount = 1);\n    void RemoveItem(string itemId, int amount = 1);\n    int GetItemCount(string itemId);\n    IReadOnlyDictionary<string, int> GetInventory();\n\n    // Save/Load operations\n    void SaveProgress();\n    void LoadProgress();\n    void ResetProgress();\n}",
                "implementation": "[Service(typeof(IProgressTracker), \"Progress\", ServiceLifetime.Singleton, ServiceContext.Runtime)]\npublic class ProgressTracker : IProgressTracker\n{\n    private readonly Dictionary<string, float> _progressData = new();\n    private readonly HashSet<string> _achievements = new();\n    private readonly Dictionary<string, int> _inventory = new();\n\n    public float GetProgress(string key)\n    {\n        return _progressData.TryGetValue(key, out float value) ? value : 0f;\n    }\n\n    public void SetProgress(string key, float value)\n    {\n        _progressData[key] = Mathf.Clamp01(value);\n        CheckProgressAchievements(key, value);\n    }\n\n    public bool IsCompleted(string key)\n    {\n        return GetProgress(key) >= 1f;\n    }\n\n    public void UnlockAchievement(string achievementId)\n    {\n        if (_achievements.Add(achievementId))\n        {\n            // Notify achievement system\n            ServiceLocator.Get<IAnalyticsService>().TrackEvent(\"Achievement_Unlocked\", new Dictionary<string, object>\n            {\n                { \"achievement_id\", achievementId }\n            });\n        }\n    }\n\n    public bool IsAchievementUnlocked(string achievementId)\n    {\n        return _achievements.Contains(achievementId);\n    }\n\n    public IReadOnlyList<string> GetUnlockedAchievements()\n    {\n        return _achievements.ToList().AsReadOnly();\n    }\n\n    public void AddItem(string itemId, int amount = 1)\n    {\n        if (!_inventory.ContainsKey(itemId))\n            _inventory[itemId] = 0;\n        _inventory[itemId] += amount;\n    }\n\n    public void RemoveItem(string itemId, int amount = 1)\n    {\n        if (_inventory.ContainsKey(itemId))\n        {\n            _inventory[itemId] = Mathf.Max(0, _inventory[itemId] - amount);\n            if (_inventory[itemId] == 0)\n                _inventory.Remove(itemId);\n        }\n    }\n\n    public int GetItemCount(string itemId)\n    {\n        return _inventory.TryGetValue(itemId, out int count) ? count : 0;\n    }\n\n    public IReadOnlyDictionary<string, int> GetInventory()\n    {\n        return _inventory;\n    }\n\n    public void SaveProgress()\n    {\n        var saveData = new SaveData\n        {\n            Progress = _progressData,\n            Achievements = _achievements.ToList(),\n            Inventory = _inventory\n        };\n\n        string json = JsonUtility.ToJson(saveData);\n        PlayerPrefs.SetString(\"SaveData\", json);\n        PlayerPrefs.Save();\n    }\n\n    public void LoadProgress()\n    {\n        if (PlayerPrefs.HasKey(\"SaveData\"))\n        {\n            string json = PlayerPrefs.GetString(\"SaveData\");\n            var saveData = JsonUtility.FromJson<SaveData>(json);\n\n            _progressData.Clear();\n            foreach (var kvp in saveData.Progress)\n                _progressData[kvp.Key] = kvp.Value;\n\n            _achievements.Clear();\n            _achievements.UnionWith(saveData.Achievements);\n\n            _inventory.Clear();\n            foreach (var kvp in saveData.Inventory)\n                _inventory[kvp.Key] = kvp.Value;\n        }\n    }\n\n    public void ResetProgress()\n    {\n        _progressData.Clear();\n        _achievements.Clear();\n        _inventory.Clear();\n        PlayerPrefs.DeleteKey(\"SaveData\");\n        PlayerPrefs.Save();\n    }\n\n    private void CheckProgressAchievements(string key, float value)\n    {\n        // Example: Unlock achievements based on progress\n        if (value >= 1f)\n        {\n            UnlockAchievement($\"{key}_completed\");\n        }\n        else if (value >= 0.5f)\n        {\n            UnlockAchievement($\"{key}_halfway\");\n        }\n    }\n\n    [Serializable]\n    private class SaveData\n    {\n        public Dictionary<string, float> Progress;\n        public List<string> Achievements;\n        public Dictionary<string, int> Inventory;\n    }\n}"
            }
        },
        {
            "name": "Input Manager",
            "description": "Processes Unity input events and manages input state",
            "category": "Input",
            "complexity": "Medium",
            "has_state": true,
            "unity_integration": "scene",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["input", "controls", "gameplay"],
            "code": {
                "interface": "public interface IInputManager\n{\n    Vector2 MovementInput { get; }\n    bool IsJumpPressed { get; }\n    event System.Action<Vector2> OnMovementChanged;\n    event System.Action OnJumpPressed;\n}",
                "implementation": "[Service(typeof(IInputManager), \"Input\", ServiceLifetime.Singleton, ServiceContext.Runtime)]\npublic class InputManager : MonoBehaviour, IInputManager\n{\n    private Vector2 _movementInput;\n    public Vector2 MovementInput => _movementInput;\n    public bool IsJumpPressed { get; private set; }\n\n    public event System.Action<Vector2> OnMovementChanged;\n    public event System.Action OnJumpPressed;\n\n    private void Update()\n    {\n        Vector2 newMovement = new Vector2(Input.GetAxisRaw(\"Horizontal\"), Input.GetAxisRaw(\"Vertical\"));\n        if (newMovement != _movementInput)\n        {\n            _movementInput = newMovement;\n            OnMovementChanged?.Invoke(_movementInput);\n        }\n\n        if (Input.GetButtonDown(\"Jump\"))\n        {\n            IsJumpPressed = true;\n            OnJumpPressed?.Invoke();\n        }\n        else if (Input.GetButtonUp(\"Jump\"))\n        {\n            IsJumpPressed = false;\n        }\n    }\n}"
            }
        },
        {
            "name": "Localization Translator",
            "description": "Translates text based on current culture and context, with no shared state between translations",
            "category": "Localization",
            "complexity": "Medium",
            "has_state": false,
            "unity_integration": "none",
            "context": "runtime_and_editor",
            "lifetime": "transient",
            "tags": ["localization", "i18n", "text"],
            "code": {
                "interface": "public interface ITranslator\n{\n    void SetCulture(CultureInfo culture);\n    void AddContextOverride(string key, string value);\n    string Translate(string key);\n    string TranslateWithParams(string key, params object[] args);\n}",
                "implementation": "[Service(typeof(ITranslator), \"Translator\", ServiceLifetime.Transient, ServiceContext.RuntimeAndEditor)]\npublic class LocalizationTranslator : ITranslator\n{\n    private CultureInfo _culture;\n    private readonly Dictionary<string, string> _contextOverrides = new();\n\n    public void SetCulture(CultureInfo culture)\n    {\n        _culture = culture;\n    }\n\n    public void AddContextOverride(string key, string value)\n    {\n        _contextOverrides[key] = value;\n    }\n\n    public string Translate(string key)\n    {\n        if (_contextOverrides.TryGetValue(key, out string override))\n            return override;\n\n        return LocalizationManager.GetTranslation(key, _culture);\n    }\n\n    public string TranslateWithParams(string key, params object[] args)\n    {\n        string template = Translate(key);\n        return string.Format(_culture, template, args);\n    }\n}"
            }
        },
        {
            "name": "Level Editor Tools",
            "description": "Provides Unity Editor tools for level design and scene manipulation",
            "category": "Editor Tools",
            "complexity": "High",
            "has_state": true,
            "unity_integration": "scene",
            "context": "editor_only",
            "lifetime": "singleton",
            "tags": ["editor", "level design", "tools"],
            "code": {
                "interface": "public interface ILevelEditorTools\n{\n    void SaveLevel(string levelName);\n    void LoadLevel(string levelName);\n    void PlaceObject(GameObject prefab, Vector3 position);\n    void RemoveObject(GameObject obj);\n    IEnumerable<GameObject> GetSelectedObjects();\n}",
                "implementation": "[Service(typeof(ILevelEditorTools), \"LevelEditor\", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]\npublic class LevelEditorTools : MonoBehaviour, ILevelEditorTools\n{\n    private List<GameObject> _selectedObjects = new();\n\n    public void SaveLevel(string levelName)\n    {\n        var levelData = new LevelData();\n        foreach (var obj in FindObjectsOfType<GameObject>())\n        {\n            if (obj.transform.parent == null)\n                levelData.AddObject(obj);\n        }\n        AssetDatabase.CreateAsset(levelData, $\"Assets/Levels/{levelName}.asset\");\n        AssetDatabase.SaveAssets();\n    }\n\n    public void LoadLevel(string levelName)\n    {\n        var levelData = AssetDatabase.LoadAssetAtPath<LevelData>($\"Assets/Levels/{levelName}.asset\");\n        if (levelData != null)\n        {\n            ClearCurrentLevel();\n            levelData.InstantiateObjects();\n        }\n    }\n\n    public void PlaceObject(GameObject prefab, Vector3 position)\n    {\n        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;\n        instance.transform.position = position;\n        Undo.RegisterCreatedObjectUndo(instance, \"Place Object\");\n    }\n\n    public void RemoveObject(GameObject obj)\n    {\n        Undo.DestroyObjectImmediate(obj);\n    }\n\n    public IEnumerable<GameObject> GetSelectedObjects()\n    {\n        return _selectedObjects;\n    }\n\n    private void ClearCurrentLevel()\n    {\n        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();\n        foreach (var obj in rootObjects)\n        {\n            DestroyImmediate(obj);\n        }\n    }\n}"
            }
        },
        {
            "name": "Asset Manager",
            "description": "Handles asset loading, caching, and resource management",
            "category": "Asset Management",
            "complexity": "High",
            "has_state": true,
            "unity_integration": "none",
            "context": "runtime_and_editor",
            "lifetime": "singleton",
            "tags": ["assets", "resources", "caching"],
            "code": {
                "interface": "public interface IAssetManager\n{\n    T LoadAsset<T>(string path) where T : UnityEngine.Object;\n    void PreloadAssets(string[] paths);\n    void UnloadUnusedAssets();\n    bool IsAssetLoaded(string path);\n}",
                "implementation": "[Service(typeof(IAssetManager), \"AssetManager\", ServiceLifetime.Singleton, ServiceContext.RuntimeAndEditor)]\npublic class AssetManager : IAssetManager\n{\n    private readonly Dictionary<string, WeakReference> _cache = new();\n\n    public T LoadAsset<T>(string path) where T : UnityEngine.Object\n    {\n        if (_cache.TryGetValue(path, out var weakRef) && weakRef.IsAlive)\n        {\n            return weakRef.Target as T;\n        }\n\n        var asset = Resources.Load<T>(path);\n        if (asset != null)\n        {\n            _cache[path] = new WeakReference(asset);\n        }\n        return asset;\n    }\n\n    public void PreloadAssets(string[] paths)\n    {\n        foreach (var path in paths)\n        {\n            if (!_cache.ContainsKey(path))\n            {\n                var asset = Resources.Load(path);\n                if (asset != null)\n                {\n                    _cache[path] = new WeakReference(asset);\n                }\n            }\n        }\n    }\n\n    public void UnloadUnusedAssets()\n    {\n        var unusedPaths = _cache.Where(kvp => !kvp.Value.IsAlive)\n                               .Select(kvp => kvp.Key)\n                               .ToList();\n\n        foreach (var path in unusedPaths)\n        {\n            _cache.Remove(path);\n        }\n\n        Resources.UnloadUnusedAssets();\n    }\n\n    public bool IsAssetLoaded(string path)\n    {\n        return _cache.TryGetValue(path, out var weakRef) && weakRef.IsAlive;\n    }\n}"
            }
        },
        {
            "name": "Data Validator",
            "description": "Validates input data against rules without maintaining internal state",
            "category": "Utility",
            "complexity": "Low",
            "has_state": false,
            "unity_integration": "none",
            "context": "runtime",
            "lifetime": "transient",
            "tags": ["validation", "utility", "data"],
            "code": {
                "interface": "public interface IDataValidator\n{\n    ValidationResult Validate<T>(T data, ValidationRules rules);\n    bool IsValid<T>(T data, ValidationRules rules);\n    IEnumerable<string> GetValidationErrors<T>(T data, ValidationRules rules);\n}",
                "implementation": "[Service(typeof(IDataValidator), \"Validator\", ServiceLifetime.Transient, ServiceContext.Runtime)]\npublic class DataValidator : IDataValidator\n{\n    public ValidationResult Validate<T>(T data, ValidationRules rules)\n    {\n        var errors = new List<string>();\n        var properties = typeof(T).GetProperties();\n\n        foreach (var rule in rules.GetRules())\n        {\n            if (!rule.Validate(data))\n            {\n                errors.Add(rule.ErrorMessage);\n            }\n        }\n\n        return new ValidationResult(errors.Count == 0, errors);\n    }\n\n    public bool IsValid<T>(T data, ValidationRules rules)\n    {\n        return Validate(data, rules).IsValid;\n    }\n\n    public IEnumerable<string> GetValidationErrors<T>(T data, ValidationRules rules)\n    {\n        return Validate(data, rules).Errors;\n    }\n}"
            }
        },
        {
            "name": "UI Theme Manager",
            "description": "Manages UI themes and styles with Inspector-configured values",
            "category": "UI",
            "complexity": "Medium",
            "has_state": true,
            "unity_integration": "config",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["ui", "themes", "configuration"],
            "code": {
                "interface": "public interface IUIThemeManager\n{\n    // Theme management\n    void SetTheme(string themeName);\n    string GetCurrentTheme();\n    IReadOnlyList<string> GetAvailableThemes();\n\n    // Style getters\n    Color GetColor(string colorName);\n    TMP_FontAsset GetFont(string fontName);\n    Sprite GetIcon(string iconName);\n    UIStyleData GetStyle(string styleName);\n\n    // Events\n    event System.Action<string> OnThemeChanged;\n}",
                "implementation": "[Service(typeof(IUIThemeManager), \"UITheme\", ServiceLifetime.Singleton, ServiceContext.Runtime)]\npublic class UIThemeManager : SOServiceBase, IUIThemeManager\n{\n    [System.Serializable]\n    public class ThemeData\n    {\n        public string themeName;\n        [Header(\"Colors\")]\n        public Color primaryColor = Color.blue;\n        public Color secondaryColor = Color.cyan;\n        public Color backgroundColor = Color.white;\n        public Color textColor = Color.black;\n\n        [Header(\"Fonts\")]\n        public TMP_FontAsset headerFont;\n        public TMP_FontAsset bodyFont;\n\n        [Header(\"Icons\")]\n        public Sprite[] icons;\n        public string[] iconNames;\n\n        [Header(\"Styles\")]\n        public UIStyleData[] styles;\n        public string[] styleNames;\n    }\n\n    [SerializeField] private ThemeData[] _themes;\n    [SerializeField] private string _defaultTheme;\n\n    private ThemeData _currentTheme;\n    private readonly Dictionary<string, Sprite> _iconCache = new();\n    private readonly Dictionary<string, UIStyleData> _styleCache = new();\n\n    public event System.Action<string> OnThemeChanged;\n\n    protected override void Initialize()\n    {\n        SetTheme(_defaultTheme);\n    }\n\n    public void SetTheme(string themeName)\n    {\n        var theme = _themes.FirstOrDefault(t => t.themeName == themeName);\n        if (theme != null)\n        {\n            _currentTheme = theme;\n            UpdateCaches();\n            OnThemeChanged?.Invoke(themeName);\n        }\n    }\n\n    public string GetCurrentTheme()\n    {\n        return _currentTheme?.themeName;\n    }\n\n    public IReadOnlyList<string> GetAvailableThemes()\n    {\n        return _themes.Select(t => t.themeName).ToList();\n    }\n\n    public Color GetColor(string colorName)\n    {\n        if (_currentTheme == null) return Color.white;\n\n        return colorName switch\n        {\n            \"primary\" => _currentTheme.primaryColor,\n            \"secondary\" => _currentTheme.secondaryColor,\n            \"background\" => _currentTheme.backgroundColor,\n            \"text\" => _currentTheme.textColor,\n            _ => Color.white\n        };\n    }\n\n    public TMP_FontAsset GetFont(string fontName)\n    {\n        if (_currentTheme == null) return null;\n\n        return fontName switch\n        {\n            \"header\" => _currentTheme.headerFont,\n            \"body\" => _currentTheme.bodyFont,\n            _ => _currentTheme.bodyFont\n        };\n    }\n\n    public Sprite GetIcon(string iconName)\n    {\n        return _iconCache.TryGetValue(iconName, out var icon) ? icon : null;\n    }\n\n    public UIStyleData GetStyle(string styleName)\n    {\n        return _styleCache.TryGetValue(styleName, out var style) ? style : null;\n    }\n\n    private void UpdateCaches()\n    {\n        _iconCache.Clear();\n        _styleCache.Clear();\n\n        if (_currentTheme == null) return;\n\n        for (int i = 0; i < _currentTheme.iconNames.Length; i++)\n        {\n            if (i < _currentTheme.icons.Length)\n            {\n                _iconCache[_currentTheme.iconNames[i]] = _currentTheme.icons[i];\n            }\n        }\n\n        for (int i = 0; i < _currentTheme.styleNames.Length; i++)\n        {\n            if (i < _currentTheme.styles.Length)\n            {\n                _styleCache[_currentTheme.styleNames[i]] = _currentTheme.styles[i];\n            }\n        }\n    }\n}"
            }
        },
        {
            "name": "Scene Transition Manager",
            "description": "Handles scene loading, transitions, and state preservation",
            "category": "Core Systems",
            "complexity": "High",
            "has_state": true,
            "unity_integration": "scene",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["scenes", "loading", "transitions"],
            "code": {
                "interface": "public interface ISceneTransitionManager\n{\n    // Scene loading\n    Task LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single);\n    Task LoadSceneWithTransitionAsync(string sceneName, ITransitionEffect transition);\n    void UnloadScene(string sceneName);\n\n    // State management\n    void PreserveObject(GameObject obj);\n    void SetLoadingScreen(GameObject loadingScreenPrefab);\n\n    // Events\n    event System.Action<string> OnSceneLoadStarted;\n    event System.Action<string> OnSceneLoadCompleted;\n    float LoadProgress { get; }\n}",
                "implementation": "[Service(typeof(ISceneTransitionManager), \"SceneTransition\", ServiceLifetime.Singleton, ServiceContext.Runtime)]\npublic class SceneTransitionManager : MonoBehaviour, ISceneTransitionManager\n{\n    private GameObject _loadingScreen;\n    private readonly HashSet<GameObject> _preservedObjects = new();\n    private AsyncOperation _currentOperation;\n    private ITransitionEffect _currentTransition;\n\n    public event System.Action<string> OnSceneLoadStarted;\n    public event System.Action<string> OnSceneLoadCompleted;\n    public float LoadProgress => _currentOperation?.progress ?? 1f;\n\n    private void Awake()\n    {\n        DontDestroyOnLoad(gameObject);\n    }\n\n    public async Task LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)\n    {\n        OnSceneLoadStarted?.Invoke(sceneName);\n\n        if (_loadingScreen != null)\n        {\n            _loadingScreen.SetActive(true);\n        }\n\n        PreserveCurrentObjects();\n\n        _currentOperation = SceneManager.LoadSceneAsync(sceneName, mode);\n        _currentOperation.allowSceneActivation = false;\n\n        while (_currentOperation.progress < 0.9f)\n        {\n            await Task.Yield();\n        }\n\n        _currentOperation.allowSceneActivation = true;\n        await Task.Yield();\n\n        if (_loadingScreen != null)\n        {\n            _loadingScreen.SetActive(false);\n        }\n\n        OnSceneLoadCompleted?.Invoke(sceneName);\n    }\n\n    public async Task LoadSceneWithTransitionAsync(string sceneName, ITransitionEffect transition)\n    {\n        _currentTransition = transition;\n        await _currentTransition.StartTransition();\n\n        await LoadSceneAsync(sceneName);\n\n        await _currentTransition.EndTransition();\n        _currentTransition = null;\n    }\n\n    public void UnloadScene(string sceneName)\n    {\n        SceneManager.UnloadSceneAsync(sceneName);\n    }\n\n    public void PreserveObject(GameObject obj)\n    {\n        if (!_preservedObjects.Contains(obj))\n        {\n            DontDestroyOnLoad(obj);\n            _preservedObjects.Add(obj);\n        }\n    }\n\n    public void SetLoadingScreen(GameObject loadingScreenPrefab)\n    {\n        if (_loadingScreen != null)\n        {\n            Destroy(_loadingScreen);\n        }\n\n        _loadingScreen = Instantiate(loadingScreenPrefab);\n        _loadingScreen.SetActive(false);\n        DontDestroyOnLoad(_loadingScreen);\n    }\n\n    private void PreserveCurrentObjects()\n    {\n        foreach (var obj in _preservedObjects)\n        {\n            if (obj != null)\n            {\n                DontDestroyOnLoad(obj);\n            }\n        }\n    }\n}"
            }
        },
        {
            "name": "Command Processor",
            "description": "Processes game commands and actions independently for each request",
            "category": "Core Systems",
            "complexity": "Medium",
            "has_state": false,
            "unity_integration": "none",
            "context": "runtime",
            "lifetime": "transient",
            "tags": ["commands", "actions", "processing"],
            "code": {
                "interface": "public interface ICommandProcessor\n{\n    CommandResult Process(IGameCommand command);\n    bool CanProcess(IGameCommand command);\n    void RegisterHandler<T>(ICommandHandler<T> handler) where T : IGameCommand;\n}",
                "implementation": "[Service(typeof(ICommandProcessor), \"CommandProcessor\", ServiceLifetime.Transient, ServiceContext.Runtime)]\npublic class CommandProcessor : ICommandProcessor\n{\n    private readonly Dictionary<Type, object> _handlers = new();\n\n    public CommandResult Process(IGameCommand command)\n    {\n        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());\n        if (_handlers.TryGetValue(command.GetType(), out var handler))\n        {\n            try\n            {\n                var method = handlerType.GetMethod(\"Handle\");\n                return (CommandResult)method.Invoke(handler, new[] { command });\n            }\n            catch (Exception ex)\n            {\n                return CommandResult.Error($\"Command processing failed: {ex.Message}\");\n            }\n        }\n        return CommandResult.Error(\"No handler registered for command\");\n    }\n\n    public bool CanProcess(IGameCommand command)\n    {\n        return _handlers.ContainsKey(command.GetType());\n    }\n\n    public void RegisterHandler<T>(ICommandHandler<T> handler) where T : IGameCommand\n    {\n        _handlers[typeof(T)] = handler;\n    }\n}"
            }
        },
        {
            "name": "Analytics Reporter",
            "description": "Collects and processes game analytics data",
            "category": "Analytics",
            "complexity": "Medium",
            "has_state": true,
            "unity_integration": "none",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["analytics", "metrics", "reporting"],
            "code": {
                "interface": "public interface IAnalyticsReporter\n{\n    // Event tracking\n    void TrackEvent(string eventName, IDictionary<string, object> parameters = null);\n    void TrackScreenView(string screenName);\n    void TrackProgress(string progressionId, string status, int score = 0);\n\n    // User properties\n    void SetUserProperty(string property, string value);\n    void SetUserId(string userId);\n\n    // Session management\n    void StartSession();\n    void EndSession();\n\n    // Data management\n    void EnableTracking(bool enabled);\n    void ClearUserData();\n    Task<bool> SendQueuedEvents();\n}",
                "implementation": "[Service(typeof(IAnalyticsReporter), \"Analytics\", ServiceLifetime.Singleton, ServiceContext.Runtime)]\npublic class AnalyticsReporter : IAnalyticsReporter\n{\n    private readonly Queue<AnalyticsEvent> _eventQueue = new();\n    private readonly Dictionary<string, string> _userProperties = new();\n    private string _userId;\n    private bool _isTrackingEnabled = true;\n    private bool _isSessionActive;\n\n    private const int MAX_QUEUE_SIZE = 1000;\n    private const int BATCH_SIZE = 100;\n\n    public void TrackEvent(string eventName, IDictionary<string, object> parameters = null)\n    {\n        if (!_isTrackingEnabled || !_isSessionActive) return;\n\n        var analyticsEvent = new AnalyticsEvent\n        {\n            Type = \"event\",\n            Name = eventName,\n            Timestamp = DateTime.UtcNow,\n            Parameters = parameters ?? new Dictionary<string, object>(),\n            UserId = _userId,\n            UserProperties = new Dictionary<string, string>(_userProperties)\n        };\n\n        QueueEvent(analyticsEvent);\n    }\n\n    public void TrackScreenView(string screenName)\n    {\n        TrackEvent(\"screen_view\", new Dictionary<string, object>\n        {\n            { \"screen_name\", screenName }\n        });\n    }\n\n    public void TrackProgress(string progressionId, string status, int score = 0)\n    {\n        TrackEvent(\"progression\", new Dictionary<string, object>\n        {\n            { \"progression_id\", progressionId },\n            { \"status\", status },\n            { \"score\", score }\n        });\n    }\n\n    public void SetUserProperty(string property, string value)\n    {\n        _userProperties[property] = value;\n    }\n\n    public void SetUserId(string userId)\n    {\n        _userId = userId;\n    }\n\n    public void StartSession()\n    {\n        _isSessionActive = true;\n        TrackEvent(\"session_start\");\n    }\n\n    public void EndSession()\n    {\n        if (_isSessionActive)\n        {\n            TrackEvent(\"session_end\");\n            _isSessionActive = false;\n        }\n    }\n\n    public void EnableTracking(bool enabled)\n    {\n        _isTrackingEnabled = enabled;\n    }\n\n    public void ClearUserData()\n    {\n        _userProperties.Clear();\n        _userId = null;\n        _eventQueue.Clear();\n    }\n\n    public async Task<bool> SendQueuedEvents()\n    {\n        if (_eventQueue.Count == 0) return true;\n\n        try\n        {\n            var events = new List<AnalyticsEvent>();\n            while (_eventQueue.Count > 0 && events.Count < BATCH_SIZE)\n            {\n                events.Add(_eventQueue.Dequeue());\n            }\n\n            // Simulate sending to analytics service\n            await Task.Delay(100); // Replace with actual API call\n\n            return true;\n        }\n        catch (Exception ex)\n        {\n            Debug.LogError($\"Failed to send analytics events: {ex.Message}\");\n            return false;\n        }\n    }\n\n    private void QueueEvent(AnalyticsEvent analyticsEvent)\n    {\n        _eventQueue.Enqueue(analyticsEvent);\n\n        if (_eventQueue.Count >= MAX_QUEUE_SIZE)\n        {\n            _ = SendQueuedEvents();\n        }\n    }\n\n    private class AnalyticsEvent\n    {\n        public string Type { get; set; }\n        public string Name { get; set; }\n        public DateTime Timestamp { get; set; }\n        public IDictionary<string, object> Parameters { get; set; }\n        public string UserId { get; set; }\n        public IDictionary<string, string> UserProperties { get; set; }\n    }\n}"
            }
        },
        {
            "name": "Object Pool Manager",
            "description": "Handles object pooling and instance management in the scene",
            "category": "Performance",
            "complexity": "Medium",
            "has_state": true,
            "unity_integration": "scene",
            "context": "runtime",
            "lifetime": "singleton",
            "tags": ["pooling", "optimization", "instances"],
            "code": {
                "interface": "public interface IObjectPoolManager\n{\n    void CreatePool(string poolId, GameObject prefab, int initialSize);\n    GameObject GetObject(string poolId);\n    void ReturnObject(GameObject obj);\n    void DestroyPool(string poolId);\n}",
                "implementation": "[Service(typeof(IObjectPoolManager), \"ObjectPool\", ServiceLifetime.Singleton)]\npublic class ObjectPoolManager : MonoBehaviour, IObjectPoolManager\n{\n    private Dictionary<string, Queue<GameObject>> _pools = new();\n    private Dictionary<string, GameObject> _prefabs = new();\n    \n    public void CreatePool(string poolId, GameObject prefab, int initialSize)\n    {\n        if (!_pools.ContainsKey(poolId))\n        {\n            _pools[poolId] = new Queue<GameObject>();\n            _prefabs[poolId] = prefab;\n            \n            for (int i = 0; i < initialSize; i++)\n            {\n                var obj = CreateNewObject(poolId);\n                obj.SetActive(false);\n                _pools[poolId].Enqueue(obj);\n            }\n        }\n    }\n\n    public GameObject GetObject(string poolId)\n    {\n        if (_pools.TryGetValue(poolId, out var pool))\n        {\n            GameObject obj = pool.Count > 0 ? pool.Dequeue() : CreateNewObject(poolId);\n            obj.SetActive(true);\n            return obj;\n        }\n        return null;\n    }\n\n    public void ReturnObject(GameObject obj)\n    {\n        var poolId = obj.name.Split('_')[0];\n        if (_pools.ContainsKey(poolId))\n        {\n            obj.SetActive(false);\n            _pools[poolId].Enqueue(obj);\n        }\n    }\n\n    public void DestroyPool(string poolId)\n    {\n        if (_pools.ContainsKey(poolId))\n        {\n            foreach (var obj in _pools[poolId])\n            {\n                Destroy(obj);\n            }\n            _pools.Remove(poolId);\n            _prefabs.Remove(poolId);\n        }\n    }\n\n    private GameObject CreateNewObject(string poolId)\n    {\n        var obj = Instantiate(_prefabs[poolId]);\n        obj.name = $\"{poolId}_pooled\";\n        return obj;\n    }\n}"
            }
        }
    ]
};