using System;

namespace GAOS.ServiceLocator.Diagnostics
{
    /// <summary>
    /// Exception thrown when a circular dependency is detected between services
    /// </summary>
    public class CircularDependencyException : ServiceLocatorException
    {
        /// <summary>
        /// Creates a new CircularDependencyException
        /// </summary>
        public CircularDependencyException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new CircularDependencyException with an inner exception
        /// </summary>
        public CircularDependencyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 