using System;
using System.Threading.Tasks;

namespace GAOS.ServiceLocator.Optional
{
    /// <summary>
    /// Interface for services that need to perform cleanup when they are no longer needed.
    /// IMPORTANT: Do not call OnSystemDisposeAsync directly. Always use ServiceLocator.ReleaseServiceInstance instead.
    /// </summary>
    public interface IServiceDisposable
    {
        
        /// <summary>
        /// Gets whether this service has been disposed.
        /// </summary>
        bool IsDisposed { get; }
        
        /// <summary>
        /// SYSTEM USE ONLY. Do not call this method directly.
        /// Service cleanup should only be triggered via ServiceLocator.ReleaseServiceInstance.
        /// This method should be implemented explicitly by the service implementation.
        /// 
        /// This method is called internally by the ServiceLocator system to clean up resources.
        /// Services must implement this method to properly clean up their resources.
        /// 
        /// </summary>
        /// <returns>A ValueTask representing the disposal operation.</returns>
        ValueTask OnSystemDisposeAsync();
        
        /// <summary>
        /// SYSTEM USE ONLY. Resets the disposal state when a service is retrieved from the pool.
        /// This method should be implemented explicitly by the service implementation.
        /// 
        /// This method is called internally by the ServiceLocator system to reset the disposal state
        /// of a service that has been disposed but is being reused from the object pool.
        /// </summary>
        /// <param name="isReset">Set to true if the disposal state was successfully reset, otherwise false.</param>
        void ResetDisposalState(out bool isReset);
    }
} 