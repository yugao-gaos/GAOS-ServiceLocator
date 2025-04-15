using System;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Attribute to mark a class as a service that should be automatically registered with the ServiceLocator.
    /// The service must specify its interface type, even if it's the same as the implementation type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceAttribute : Attribute
    {
        /// <summary>
        /// The interface type this service implements
        /// </summary>
        public Type ServiceInterface { get; }

        /// <summary>
        /// The lifetime of the service
        /// </summary>
        public ServiceLifetime Lifetime { get; }

        /// <summary>
        /// Name to identify this service implementation
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The context in which this service operates
        /// </summary>
        public ServiceContext Context { get; }

        /// <summary>
        /// Additional service dependencies that are not detectable through static analysis
        /// </summary>
        public Type[] AdditionalDependencies { get; }

        /// <summary>
        /// Creates a new ServiceAttribute
        /// </summary>
        /// <param name="serviceInterface">The interface type this service implements. Required even if the service is its own interface.</param>
        /// <param name="name">Name to identify this implementation</param>
        /// <param name="lifetime">The lifetime of the service</param>
        /// <param name="context">The context in which this service operates</param>
        /// <param name="additionalDependencies">Additional service dependencies that cannot be detected through static analysis</param>
        public ServiceAttribute(Type serviceInterface, string name, ServiceLifetime lifetime = ServiceLifetime.Singleton, 
            ServiceContext context = ServiceContext.Runtime, params Type[] additionalDependencies)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Service name cannot be null or empty", nameof(name));

            ServiceInterface = serviceInterface ?? throw new ArgumentNullException(nameof(serviceInterface));
            Name = name;
            Lifetime = lifetime;
            Context = context;
            AdditionalDependencies = additionalDependencies ?? Array.Empty<Type>();
        }
    }
} 