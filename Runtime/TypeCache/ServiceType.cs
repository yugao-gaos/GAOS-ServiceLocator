using System;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Defines the type of service implementation
    /// </summary>
    public enum ServiceType
    {
        /// <summary>
        /// Regular C# class service
        /// </summary>
        Regular,

        /// <summary>
        /// MonoBehaviour-based service that can be attached to GameObjects
        /// </summary>
        MonoBehaviour,

        /// <summary>
        /// ScriptableObject-based service that can be configured in the Unity Inspector
        /// </summary>
        ScriptableObject
    }
} 