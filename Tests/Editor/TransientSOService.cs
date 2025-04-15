using UnityEngine;
using System;
using System.Threading.Tasks;
using GAOS.ServiceLocator;
using GAOS.ServiceLocator.Optional;
namespace GAOS.ServiceLocator.Tests
{
    [Service(typeof(ITransientService), name: "TransientSOService", lifetime: ServiceLifetime.Transient)]
    public class TransientSOService : ScriptableObject, ITransientService, IServiceDisposable
    {
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        public async ValueTask OnSystemDisposeAsync()
        {
            _isDisposed = true;
            await Task.CompletedTask;
        }


        // Implement ResetDisposalState method explicitly
        void IServiceDisposable.ResetDisposalState(out bool isReset)
        {
            _isDisposed = false;
            isReset = true;
        }
    }
} 