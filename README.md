# GAOS Service Locator

A thread-safe service locator system for Unity that provides seamless management of both regular C# services, MonoBehaviour services, and ScriptableObject services.

## Features

- Thread-safe service registration and retrieval
- Support for both regular C# classes and MonoBehaviours
- Support for ScriptableObject-based services with automatic initialization
- Automatic scene-aware MonoBehaviour service discovery
- Support for multiple implementations of the same interface
- Singleton and Transient lifetime management
- DontDestroyOnLoad object support
- Automatic cache validation during scene changes

## Installation

1. Open the Unity Package Manager
2. Click the '+' button and select "Add package from git URL"
3. Enter: `com.gaos.servicelocator`

## Quick Start

### Regular C# Service

```csharp
// Define your service interface
public interface IDataService
{
    void SaveData(string key, string value);
    string LoadData(string key);
}

// Implement your service
public class JsonDataService : IDataService
{
    public void SaveData(string key, string value)
    {
        // Implementation
    }

    public string LoadData(string key)
    {
        // Implementation
        return "";
    }
}

// Register the service
ServiceLocator.Register<IDataService, JsonDataService>("JsonData");

// Use the service
var dataService = ServiceLocator.Get<IDataService>("JsonData");
dataService.SaveData("key", "value");
```

### MonoBehaviour Service

```csharp
// Define your service interface
public interface IGameManager
{
    void StartGame();
    void PauseGame();
}

// Implement your MonoBehaviour service
public class GameManager : MonoBehaviour, IGameManager
{
    private void Awake()
    {
        // Optional: Make the service persist across scenes
        DontDestroyOnLoad(gameObject);
    }

    public void StartGame()
    {
        // Implementation
    }

    public void PauseGame()
    {
        // Implementation
    }
}

// Register the MonoBehaviour service type
ServiceLocator.RegisterMonoBehaviourService<IGameManager, GameManager>("MainGame");

// Use the service anywhere
if (ServiceLocator.TryGet<IGameManager>(out var gameManager, "MainGame"))
{
    gameManager.StartGame();
}
```

### ScriptableObject Service

```csharp
// Define your service interface
public interface IGameSettings
{
    float MasterVolume { get; set; }
    bool IsMusicEnabled { get; set; }
}

// Implement your ScriptableObject service
[CreateAssetMenu(fileName = "GameSettings", menuName = "Services/Game Settings")]
[Service(typeof(IGameSettings), "GameSettings", ServiceLifetime.Singleton, ServiceContext.Runtime)]
public class GameSettingsService : ScriptableObject, IGameSettings
{
    [SerializeField] private float _masterVolume = 1f;
    [SerializeField] private bool _isMusicEnabled = true;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = value;
            SaveSettings();
        }
    }

    public bool IsMusicEnabled
    {
        get => _isMusicEnabled;
        set
        {
            _isMusicEnabled = value;
            SaveSettings();
        }
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void SaveSettings()
    {
        // Implementation
    }

    private void LoadSettings()
    {
        // Implementation
    }
}

// The service will be auto-registered on startup
// Or you can manually register it:
ServiceLocator.RegisterScriptableObjectService<IGameSettings, GameSettingsService>("GameSettings");

// Use the service
var settings = ServiceLocator.Get<IGameSettings>("GameSettings");
settings.MasterVolume = 0.5f;
```