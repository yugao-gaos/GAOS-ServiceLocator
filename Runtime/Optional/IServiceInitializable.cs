using System.Threading.Tasks;

namespace GAOS.ServiceLocator.Optional
{
    /// <summary>
    /// Interface for services that require async initialization
    /// </summary>
    public interface IServiceInitializable
    {
        Task InitializeAsync();
        bool IsInitialized { get; }
    }
} 