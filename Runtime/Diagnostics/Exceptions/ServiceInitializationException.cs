using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Exception thrown when a service fails to initialize
    /// </summary>
    public class ServiceInitializationException : ServiceLocatorException
    {
        public ServiceInitializationException(string message) : base(message)
        {
        }

        public ServiceInitializationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 