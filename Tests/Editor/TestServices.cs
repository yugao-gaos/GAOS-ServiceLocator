using UnityEngine;
using GAOS.ServiceLocator;
using GAOS.ServiceLocator.Optional;
namespace GAOS.ServiceLocator.Tests
{
    public interface ITestService
    {
        public string GetValue();
    }

    [Service(typeof(ITestService), "TestService", ServiceLifetime.Singleton, ServiceContext.Runtime)]
    public class TestService : ITestService
    {
        public string GetValue() => "TestService";
    }

    [Service(typeof(ITestService), "TransientService", ServiceLifetime.Transient, ServiceContext.Runtime)]
    public class TransientTestService : ITestService
    {
        private static int _instanceCount;
        private readonly int _instanceId;

        public TransientTestService()
        {
            _instanceId = ++_instanceCount;
        }

        public string GetValue() => $"TransientService_{_instanceId}";
    }

    [Service(typeof(ITestService), "EditorService", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class EditorOnlyService : ITestService
    {
        public string GetValue() => "EditorService";
    }

    public interface ITestScopedService
    {
        int GetNumber();
    }

    public class TestScopedService : ITestScopedService
    {
        private readonly int _number;

        public TestScopedService(int number)
        {
            _number = number;
        }

        public int GetNumber()
        {
            return _number;
        }
    }
} 