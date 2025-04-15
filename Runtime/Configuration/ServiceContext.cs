using System;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Defines the context in which a service operates
    /// </summary>
    public enum ServiceContext
    {
        /// <summary>
        /// Service operates only during runtime (play mode)
        /// </summary>
        Runtime,

        /// <summary>
        /// Service operates in both editor and runtime
        /// </summary>
        RuntimeAndEditor,

        /// <summary>
        /// Service operates only in editor
        /// </summary>
        EditorOnly
    }
} 