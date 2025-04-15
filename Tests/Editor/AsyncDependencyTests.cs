using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Editor;
using GAOS.ServiceLocator.Optional;
using GAOS.ServiceLocator.TestUtils;

namespace GAOS.ServiceLocator.Tests
{
    [TestFixture]
    public class AsyncDependencyTests
    {
        private ServiceTypeCache _typeCache;
        private ITestServiceUtility _registration;

        [SetUp]
        public void Setup()
        {
            _typeCache = ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false);
            ServiceLocator.SetSkipEditorContextValidation(true);
            _registration = ServiceLocatorTestUtils.GetTestRegistration();
            _registration.Register<ISyncServiceA, SyncServiceA>("ServiceA", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            _registration.Register<ISyncServiceB, SyncServiceB>("ServiceB", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            _registration.Register<IAsyncServiceC, AsyncServiceC>("ServiceC", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
        }

        [TearDown]
        public void TearDown()
        {
            _registration.Clear();
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false);
            ServiceLocator.SetSkipEditorContextValidation(false);
        }

        [Test]
        public void ValidateDependencies_DetectsAsyncDependencyInChain()
        {
            // Expect validation error logs in the order they are detected
            // First SyncServiceB is checked as it's a direct dependency of SyncServiceA
            LogAssert.Expect(LogType.Error, 
                "Service SyncServiceB has async dependencies but does not implement IServiceInitializable. All services with async dependencies must implement IServiceInitializable.");

            // Then SyncServiceA is checked
            LogAssert.Expect(LogType.Error, 
                "Service SyncServiceA has async dependencies but does not implement IServiceInitializable. All services with async dependencies must implement IServiceInitializable.");

            // Act
            ServiceEditorValidator.EnableLogging = true;
            var result = ServiceEditorValidator.ValidateDependencies(typeof(ISyncServiceA), _typeCache);
            ServiceEditorValidator.EnableLogging = false;

        

            // Assert
            Assert.That(result.hasAsyncDependency, Is.True, "Should detect async dependency in chain");
            Assert.That(_typeCache.GetTypeInfo(typeof(ISyncServiceA)).HasAsyncDependency, Is.True, "SyncServiceA should have async dependency");
            Assert.That(_typeCache.GetTypeInfo(typeof(ISyncServiceB)).HasAsyncDependency, Is.True, "SyncServiceB should have async dependency");
        }

        [Test]
        public void Get_ServiceWithAsyncDependency_ThrowsException()
        {
            // Expect the error log
            LogAssert.Expect(LogType.Error, 
                "[ServiceLocator] Error accessing service SyncServiceA: Invalid synchronous access to async service SyncServiceA. Service has async dependencies in its chain. Use GetAsync() and consider making all types in the dependency chain IServiceInitializable to properly handle async initialization.");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => 
                ServiceLocator.GetService<ISyncServiceA>("ServiceA"));

            // Verify the exception message
            Assert.That(ex.Message, 
                Contains.Substring("Invalid synchronous access to async service"));
            Assert.That(ex.Message, 
                Contains.Substring("Use GetAsync() and consider making all types in the dependency chain IServiceInitializable"));
        }

      

        [Test]
        public void GetAsync_ServiceWithAsyncDependency_ThrowsException()
        {
            // Expect the error log for failed service resolution
            LogAssert.Expect(LogType.Error, 
                "[ServiceLocator] Cannot use GetAsyncService for non-async service SyncServiceB. Use GetService instead for synchronous services.");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ServiceLocatorException>(async () => 
                await ServiceLocator.GetAsyncService<ISyncServiceB>("ServiceB"));

            // Verify the outer exception message
            Assert.That(ex.Message, 
                Contains.Substring("Error resolving service SyncServiceB"));

            // Verify the inner exception is InvalidOperationException with correct message
            Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, 
                Contains.Substring("Cannot use GetAsyncService for non-async service"));
            Assert.That(ex.InnerException.Message, 
                Contains.Substring("Use GetService instead for synchronous services"));
        }

        // Test Services
        public interface ISyncServiceA { string GetValue(); }
        public interface ISyncServiceB { string GetValue(); }
        public interface IAsyncServiceC { string GetValue(); }

        [Service(typeof(ISyncServiceA), "ServiceA")]
        public class SyncServiceA : ISyncServiceA
        {
            private ISyncServiceB _serviceB;

            public SyncServiceA()
            {
                // Force sync initialization to demonstrate the issue
                _serviceB = ServiceLocator.GetService<ISyncServiceB>("ServiceB");
            }

            public string GetValue() => "ServiceA";
        }

        [Service(typeof(ISyncServiceB), "ServiceB")]
        public class SyncServiceB : ISyncServiceB
        {
            private IAsyncServiceC _serviceC;

            public SyncServiceB()
            {
                // Empty constructor - initialization moved to InitializeAsync
            }

            public string GetValue() => "ServiceB";
        }

        [Service(typeof(IAsyncServiceC), "ServiceC")]
        public class AsyncServiceC : IAsyncServiceC, IServiceInitializable
        {
            private bool _isInitialized;
            public bool IsInitialized => _isInitialized;

            public async Task InitializeAsync()
            {
                await Task.Delay(100); // Simulate some async initialization
                _isInitialized = true;
                Debug.Log("AsyncServiceC initialized");
            }

            public string GetValue() => "ServiceC";
        }


    }
} 