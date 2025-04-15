namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Defines the type of assembly a service is defined in
    /// </summary>
    public enum ServiceAssemblyType
    {
        /// <summary>
        /// Service is in a runtime assembly (default)
        /// </summary>
        Runtime,

        /// <summary>
        /// Service is in an editor assembly
        /// </summary>
        Editor,

        /// <summary>
        /// Service is in a test assembly (overrides Editor if both)
        /// </summary>
        Test
    }
} 