using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Exception thrown when a requested service is not found
    /// </summary>
    public class ServiceNotFoundException : ServiceLocatorException
    {
        public ServiceNotFoundException(string message) : base(message)
        {
        }

        public ServiceNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 