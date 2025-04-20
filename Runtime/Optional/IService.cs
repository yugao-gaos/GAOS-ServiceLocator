using System;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Base interface for all services. 
    /// Implementing this interface allows a class to be registered with the ServiceLocator.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Initializes the service. Called when the service is first created.
        /// </summary>
        void Initialize();
    }
} 