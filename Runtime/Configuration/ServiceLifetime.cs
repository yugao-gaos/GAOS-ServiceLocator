using System;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Defines how a service instance should be managed by the ServiceLocator
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// A single instance is created and reused for all requests
        /// </summary>
        Singleton,

        /// <summary>
        /// A single instance per scene is created and reused for all requests within that scene.
        /// Instance is disposed when the scene unloads.
        /// </summary>
        SceneSingleton,

        /// <summary>
        /// A new instance is created for each request
        /// </summary>
        Transient,
        
        /// <summary>
        /// A new instance is created for each request within a scene.
        /// All instances are disposed when the scene unloads.
        /// </summary>
        SceneTransient
    }
} 