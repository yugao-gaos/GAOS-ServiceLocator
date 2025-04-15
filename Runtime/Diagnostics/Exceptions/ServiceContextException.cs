using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Thrown when a service is accessed in an invalid context (e.g., Runtime service accessed outside play mode)
    /// </summary>
    public class ServiceContextException : ServiceLocatorException
    {
        public ServiceContextException(string message) : base(message) { }
        public ServiceContextException(string message, Exception innerException) : base(message, innerException) { }
    }
} 