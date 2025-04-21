# Changelog

All notable changes to the Service Locator package will be documented in this file.

## [1.11.2] - 2024-06-10

### Fixed
- Fixed SceneSingleton MonoBehaviour service behavior
  - Fixed an issue where SceneSingleton services would create a new instance in DontDestroyOnLoad instead of finding and moving existing scene instances
  - Modified MonoServiceFactory.FindSceneInstance to prioritize finding existing instances by type rather than exact GameObject name
  - Improved instance reuse for MonoBehaviour services with SceneSingleton lifetime
  - Enhanced logging when finding existing service instances in scenes

### Added
- Extended ServiceLocatorEditorInitializer to support RuntimeAndEditor services
  - Updated editor initialization to register both EditorOnly and RuntimeAndEditor services during domain reload
  - Improved service initialization in the editor
  - Added better logging with separate counters for EditorOnly and RuntimeAndEditor services
  - Enhanced service discovery reliability across domain reloads

## [1.11.1] - 2024-05-15

### Fixed
- Critical deadlock issues in Unity operations
  - Resolved potential deadlocks when releasing service instances on the main thread
  - Fixed thread safety issues in MonoBehaviour service creation and destruction
  - Improved ServiceCoroutineRunner to safely schedule main thread operations
  - Enhanced GameObject operation handling to prevent blocking the main thread
  - Implemented safe thread coordination between async operations and Unity main thread

### Changed
- Enhanced thread safety throughout codebase
  - Replaced SynchronizationContext with ServiceCoroutineRunner for all Unity operations
  - Added main thread validation for GameObject operations
  - Improved async/await pattern usage to avoid deadlocks
  - Added comprehensive thread-safety documentation in Best Practices
  - Updated service instance cleanup to avoid blocking the main thread

## [1.11.0] - 2024-05-01

### Added
- Service instance pooling system
  - Added `IServicePoolable` interface for creating poolable services
  - Implemented service pool management with automatic instance recycling
  - Added instance pooling for MonoBehaviour services with GameObject deactivation
  - Added pool expansion capability when all instances are in use
  - Added lifecycle hooks for pool initialization and instance reuse
  - Added comprehensive documentation for implementing poolable services

### Changed
- Enhanced service instance management
  - Improved service registration to support pooled services
  - Added automatic pool initialization during service registration
  - Optimized instance tracking for pooled and non-pooled services
  - Updated disposal system to return poolable instances to the pool
  - Added ServicePool component for managing pooled service GameObjects

## [1.10.0] - 2024-04-20

### Added
- Added public service disposal API
  - Added `ServiceLocator.ReleaseServiceInstance(object service)` method for synchronous service disposal
  - Added `ServiceLocator.ReleaseServiceInstanceAsync(object service)` method for asynchronous service disposal
  - Implemented proper instance tracking and lookup for manual disposal
  - Added comprehensive documentation for manual service disposal

### Changed
- Improved encapsulation of internal APIs
  - Made `DisposalHandle` class internal to prevent direct access
  - Made `IDisposableService.DisposalHandle` property internal
  - Updated documentation to reflect the new internal access modifiers
  - Improved service disposal guidance in API documentation
  - Unified public API for service disposal through ServiceLocator methods

## [1.9.0] - 2024-03-07

### Added
- Added SceneTransient lifetime option
  - Provides new instances per request within a scene
  - Automatically disposes all instances when scene unloads
  - Combines benefits of Transient (unique instances) with scene lifecycle management
  - Implemented full thread-safety for scene-transient services
  - Added comprehensive test coverage for SceneTransient services

### Changed
- Updated documentation to include SceneTransient lifetime option
  - Added SceneTransient to API documentation
  - Updated service wizard to support SceneTransient option
  - Added code examples for SceneTransient services
  - Enhanced explanation of service lifetime options
  - Updated quick-start guide with new lifetime option

## [1.8.0] - 2024-03-05

### Added
- Enhanced service disposal system
  - Added `IDisposableService` interface for proper cleanup
  - Implemented `DisposalHandle` for tracking disposal state
  - Added support for async disposal operations
  - Added disposal state tracking and validation
  - Enhanced cleanup of Unity objects during disposal

### Changed
- Improved thread safety and synchronization
  - Replaced MonoBehaviour-based disposal with `SynchronizationContext`
  - Enhanced locking strategy for shared state operations
  - Improved async operation handling in service lifecycle
  - Better handling of Unity object destruction on main thread
  - Optimized instance tracking and cleanup

### Fixed
- Fixed instance naming consistency
  - Corrected instance counter increment timing
  - Ensured instance names match registration keys
  - Fixed instance tracking during disposal
  - Improved cleanup of disposed instances
  - Enhanced validation of disposal state

### Added
- Enhanced test coverage
  - Added comprehensive disposal tests
  - Added tests for async service lifecycle
  - Added tests for multiple instance handling
  - Added tests for scene-specific service behavior
  - Enhanced validation of service state during tests

## [1.7.0] - 2024-02-29

### Added
- Scene-aware service management
  - Added scene-specific service resolution with three methods:
    - `GetService<T>(string name)` - Uses active scene
    - `GetService<T>(string name, Component)` - Uses component's scene
    - `GetService<T>(string name, Scene)` - Uses specific scene
  - Implemented `SceneSingleton` lifetime for scene-specific services
  - Added scene lifecycle management with proper cleanup
  - Enhanced instance tracking per scene

### Changed
- Improved service factories
  - Enhanced `MonoServiceFactory` with scene-aware instance creation
  - Added `SOServiceFactory` with scene-specific ScriptableObject handling
  - Improved instance validation and cleanup mechanisms
- Enhanced service tracking
  - Added comprehensive instance tracking system
  - Improved monitoring of service lifecycle
  - Added scene association tracking

### Fixed
- Proper cleanup of services during scene unload
- Better handling of duplicate instances across scenes
- Improved scene context validation
- Enhanced error reporting for scene-related issues

## [1.6.0] - 2024-02-28

### Changed
- Improved API consistency and clarity
  - Renamed `Get<TService>` to `GetService<TService>`
  - Renamed `GetAll<TService>` to `GetAllServices<TService>`
  - Renamed `TryGet<TService>` to `TryGetService<TService>`
  - Made registration methods internal for testing only
  - Updated documentation to reflect new method names

### Added
- Enhanced documentation organization
  - Added new advanced topics sections
  - Added "Disposable Service" documentation
  - Added "Async Service" documentation
  - Improved navigation between documentation pages
  - Added more code examples and best practices

### Fixed
- Documentation consistency and accuracy
  - Fixed method names in examples
  - Updated navigation links in advanced topics
  - Corrected service registration examples
  - Improved clarity of service lifecycle documentation

## [1.5.0] - 2024-02-26

### Added
- Enhanced async service support and validation
  - Added async dependency chain detection and validation
  - Added [Async] tag visualization in Service Inspector
  - Added warning system for non-async services with async dependencies
  - Added async dependency chain visualization in dependency tree
  - Added validation messages for proper async initialization

### Changed
- Improved Service Inspector UI organization
  - Moved service name to Basic Information section
  - Enhanced dependency tree visualization with async tags
  - Improved validation message display in Dependencies section
  - Added clear distinction between async and sync services
  - Better organization of service information sections

### Fixed
- Fixed async dependency validation in service chains
- Fixed validation message display for async services
- Improved async dependency detection accuracy
- Enhanced async initialization validation

## [1.3.0] - 2024-02-23

### Added
- Enhanced circular dependency detection and validation
  - Added validation cache to store dependency analysis results
  - Introduced dependency tree visualization in Service Inspector
  - Added initialization order display for services
  - Improved performance by caching validation results
- New dependency analysis features
  - Split dependency analysis into direct dependencies and full tree
  - Added clear distinction between interface and implementation dependencies
  - Improved cycle detection with better path tracking
  - Added validation message display in Service Inspector

### Changed
- Improved Service Inspector performance and UI
  - Reduced redundant dependency checks
  - Enhanced dependency tree visualization with proper indentation
  - Added initialization order display in Dependencies section
  - Improved validation message display and organization
  - Better handling of circular dependency warnings and errors
- Refactored dependency analysis system
  - Separated concerns between dependency collection and validation
  - Improved code organization and maintainability
  - Enhanced safety with better null checks and error handling
  - Optimized dependency tree traversal

### Fixed
- Resolved performance issues with redundant dependency checks
- Fixed indentation issues in dependency tree display
- Improved handling of validation cache during domain reloads
- Enhanced stability of dependency analysis system

## [1.2.3] - 2024-02-22

### Changed
- Enhanced documentation UI/UX
  - Added rotating carousel for advanced topics preview
  - Improved modal styling consistency across documentation
  - Updated service wizard color scheme and layout
  - Enhanced navigation between documentation pages
  - Improved service examples display and interaction
  - Fixed modal popup layout issues in service wizard
  - Updated heading styles for better visual hierarchy

## [1.2.2] - 2024-02-21

### Changed
- Improved code organization and maintainability
  - Consolidated validation logic into `ServiceValidationHelper` class
  - Consolidated MonoBehaviour validation into `ServiceValidator` class
  - Removed redundant `MonoServiceValidator` class
  - Consolidated all logging and diagnostics into `ServiceDiagnostics` class
  - Removed redundant `ServiceLogger` class
  - Made logging more consistent with configurable debug mode
  - Fixed accessibility issues in service registration logging
  - Improved encapsulation of internal service types
- Enhanced test support in validation system
  - Added test context validation skipping
  - Allows testing services regardless of their runtime context
  - Improved test reliability for Runtime services in editor

## [1.2.1] - 2024-02-21

### Changed
- Improved Service Inspector Window UI/UX
  - Enhanced pagination system with dynamic page size adjustment
  - Added clickable page numbers and navigation controls
  - Fixed pagination issues when docking/resizing inspector window
  - Optimized height calculations for better space utilization
  - Adjusted window height calculation to provide more content space
  - Added visual feedback for current page selection
  - Improved layout stability during window resizing

### Fixed
- Service Inspector Window now properly adjusts content when docked to different locations
- Resolved layout issues with GetLastRect errors
- Improved window size detection using Screen.height
- Fixed pagination recalculation timing to prevent continuous updates while resizing

## [1.2.0] - 2024-02-20

### Added
- Service Inspector Window
  - Real-time monitoring of registered services
  - Visual display of service status and dependencies
  - Filter and search functionality for services
- Quick Service Creation Tools
  - Menu items for creating service interfaces and implementations
  - Templates for common service patterns
  - Automatic test file generation

### Changed
- Enhanced editor workflow with service inspection capabilities
- Streamlined service creation process with templates
- Updated documentation with new tooling guides

## [1.1.0] - 2024-02-17

### Added
- ScriptableObject-based services support through `SOServiceBase`
- Automatic initialization and lifecycle management for SO services
- Thread-safe service initialization with error tracking
- Template-based service instantiation for SO services
- Example `GameSettingsService` implementation
- Comprehensive documentation for SO services

### Changed
- Updated documentation to include SO services examples and best practices
- Improved error handling with `InitializationError` tracking
- Enhanced thread safety in service lifecycle management

## [1.0.0] - 2024-02-16

### Added
- Initial release of the Service Locator package
- Thread-safe service registration and retrieval
- Support for both regular C# classes and MonoBehaviours
- Automatic scene-aware MonoBehaviour service discovery
- Support for multiple implementations of the same interface
- Singleton and Transient lifetime management
- DontDestroyOnLoad object support
- Automatic cache validation during scene changes
- Comprehensive documentation and examples 