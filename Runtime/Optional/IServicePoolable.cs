using System;

namespace GAOS.ServiceLocator.Optional
{
    /// <summary>
    /// Interface for services that can be pooled.
    /// Implementing this interface allows service instances to be reused
    /// rather than destroyed when released.
    /// </summary>
    public interface IServicePoolable
    {
        /// <summary>
        /// Gets the initial size of the pool for this service.
        /// </summary>
        int InitialPoolSize { get; }
        
        /// <summary>
        /// Gets the number of instances to add when the pool needs to expand.
        /// </summary>
        int PoolExpansionSize { get; }
        
 
        
        /// <summary>
        /// Called when the service instance is returned to the pool.
        /// Use this to reset the instance state.
        /// </summary>
        void OnReturnToPool();
        
        /// <summary>
        /// Called when the service instance is taken from the pool.
        /// Use this to prepare the instance for use.
        /// </summary>
        void OnTakeFromPool();
    }
} 