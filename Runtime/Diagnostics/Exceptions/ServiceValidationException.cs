using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Thrown when a service fails validation (e.g., invalid type, missing dependencies)
    /// </summary>
    public class ServiceValidationException : ServiceLocatorException
    {
        public ServiceValidationException(string message) : base(message) { }
        public ServiceValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
} 