using UnityEngine;
using System;
using GAOS.ServiceLocator.Optional;
using System.Threading.Tasks;
namespace GAOS.ServiceLocator.Tests
{
    [Service(typeof(ITestService), name: "TestSOService", lifetime: ServiceLifetime.Transient)]
    public class TestSOService : ScriptableObject, ITestService, IServiceDisposable
    {
        private bool _isDisposed;
        public bool IsDisposed => _isDisposed;
        public async ValueTask OnSystemDisposeAsync()
        {
           _isDisposed = true;
           await Task.CompletedTask;
        }

        public string GetValue() => "TestSOService";


        // Implement ResetDisposalState method explicitly
        void IServiceDisposable.ResetDisposalState(out bool isReset)
        {
            _isDisposed = false;
            isReset = true;
        }
    }
} 