using System;
using UnityEngine;
using GAOS.ServiceLocator;
using System.Threading.Tasks;
namespace GAOS.ServiceLocator.TestUtils
{
    public interface ITestServiceUtility
    {
        void Register<TService, TImplementation>(string name, ServiceLifetime lifetime = ServiceLifetime.Singleton, ServiceContext context = ServiceContext.Runtime) 
            where TImplementation : class, TService;
        

        Task UnregisterService<TService>(string name);
        Task UnregisterService(Type serviceType, string name);
        Task Clear();
        
        // For test use only - allows runtime context services in editor assemblies during testing
        void SetSkipEditorContextValidation(bool skip);
    }

    internal class TestServiceUtility : ITestServiceUtility
    {
        public void Register<TService, TImplementation>(string name, ServiceLifetime lifetime = ServiceLifetime.Singleton, ServiceContext context = ServiceContext.Runtime) 
            where TImplementation : class, TService
        {
            ServiceLocator.Register<TService, TImplementation>(name, lifetime, context);
        }


        public async Task UnregisterService<TService>(string name)
        {
            await ServiceLocator.UnregisterService(typeof(TService), name);
        }

        public async Task UnregisterService(Type serviceType, string name)
        {
            await ServiceLocator.UnregisterService(serviceType, name);
        }

        public async Task Clear()
        {
            await ServiceLocator.Clear();
        }

        public void SetSkipEditorContextValidation(bool skip)
        {
            ServiceLocator.SetSkipEditorContextValidation(skip);
        }
    }

    public static class ServiceLocatorTestUtils
    {
        public static ITestServiceUtility GetTestRegistration()
        {
            return new TestServiceUtility();
        }
    }
} 