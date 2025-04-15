using UnityEngine;
using GAOS.ServiceLocator.Tests;
using GAOS.ServiceLocator.Optional;
using System.Threading.Tasks;
using System;

namespace GAOS.ServiceLocator.Tests.PlayMode
{
    public class TestMonoBehaviour : MonoBehaviour, ITestService, IServiceDisposable
    {
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        public async ValueTask OnSystemDisposeAsync()
        {
           _isDisposed = true;
           await Task.CompletedTask;
        }

        public string GetValue() => "TestMonoBehaviour";

        // Implement ResetDisposalState method explicitly
        void IServiceDisposable.ResetDisposalState(out bool isReset)
        {
            _isDisposed = false;
            isReset = true;
        }
    }
} 