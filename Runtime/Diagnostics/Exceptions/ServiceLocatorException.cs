using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Base exception class for ServiceLocator-related errors
    /// </summary>
    public class ServiceLocatorException : Exception
    {
        public ServiceLocatorException(string message) : base(message)
        {
        }

        public ServiceLocatorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 