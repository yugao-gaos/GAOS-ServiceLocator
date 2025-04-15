using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.Optional;
using GAOS.ServiceLocator.TestUtils; 
using GAOS.ServiceLocator.Editor;
using System.Threading.Tasks;

namespace GAOS.ServiceLocator.Tests.PlayMode
{
    [TestFixture]
    public class ServicePoolingTests
    {
        private ITestServiceUtility _testUtil;

         [SetUp]
        public void Setup()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false);
            _testUtil = ServiceLocatorTestUtils.GetTestRegistration();
            
        }

        [TearDown]
        public void Cleanup()
        {
            _testUtil.Clear();
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false);
        }



       

        [UnityTest]
        public IEnumerator PooledService_ExpandsPool_WhenNeeded()
        {
            _testUtil.Register<IPoolableService, PooledMonoBehaviour>("PooledMonoServiceExpands", ServiceLifetime.Transient, ServiceContext.EditorOnly);
            
            // Need to find the service-specific pool parent
            Transform servicePoolParent = null;
            foreach (Transform child in ServiceLocator.ServicePoolTransform)
            {
                if (child.name == "IPoolableService_PooledMonoServiceExpands")
                {
                    servicePoolParent = child;
                    break;
                }
            }
            
            Assert.IsNotNull(servicePoolParent, "Service pool parent should exist");
            Debug.Log("PooledMonoServiceExpands A: " + servicePoolParent.childCount);
            Assert.That(servicePoolParent.childCount == 3, Is.True, "initial pool size should be 3");

            var instance1 = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceExpands");
            var instance2 = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceExpands");
            var instance3 = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceExpands");
            var instance4 = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceExpands");
            
            Debug.Log("PooledMonoServiceExpands B: " + servicePoolParent.childCount);

            yield return new WaitForEndOfFrame();

            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledMonoServiceExpands",instance1);
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledMonoServiceExpands",instance2);
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledMonoServiceExpands",instance3);
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledMonoServiceExpands",instance4);
            
            Debug.Log("PooledMonoServiceExpands C: " + servicePoolParent.childCount);
            
            yield return new WaitForEndOfFrame();
            
            Assert.That(servicePoolParent.childCount == 5, Is.True, "pool size should be 5");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator PooledService_MonoBehaviour_IsDeactivatedInPool()
        {
             _testUtil.Register<IPoolableService, PooledMonoBehaviour>("PooledMonoService", ServiceLifetime.Transient, ServiceContext.EditorOnly);
          
            // Get an instance to check its active state
            var instance = ServiceLocator.GetService<IPoolableService>("PooledMonoService") as MonoBehaviour;
            Assert.That(instance, Is.Not.Null, "Should get an instance");
            
            // Verify GameObject is active when in use
            Assert.That(instance.gameObject.activeSelf, Is.True, 
                "GameObject should be active when in use");
            
            // Release it back to pool
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledMonoService",instance);
            yield return null;
            
            // Verify GameObject is inactive in pool
            Assert.That(instance.gameObject.activeSelf, Is.False, 
                "GameObject should be inactive when in pool");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator PooledService_FromPool_AssociatesWithNewScene()
        {
            _testUtil.Register<IPoolableService, PooledMonoBehaviour>("PooledMonoServiceScene", ServiceLifetime.SceneTransient, ServiceContext.EditorOnly);
          

            var scene = SceneManager.CreateScene("TestScene1");
            SceneManager.SetActiveScene(scene);
            // Get service in the scene
            var service = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceScene");
            Assert.IsNotNull(service, "Service should be obtained");
            Assert.IsTrue(ServiceLocator.CheckServiceBelongsToScene<IPoolableService>(service, "PooledMonoServiceScene", SceneManager.GetActiveScene()), "Service should be associated with the current scene");
           
    
            // Create test scene
            var scene2 = SceneManager.CreateScene("TestScene2");
            SceneManager.SetActiveScene(scene2);
            SceneManager.UnloadSceneAsync( scene);
            
            yield return new WaitForSeconds(0.1f);


            
            // Get service in the scene
            var service2    = ServiceLocator.GetService<IPoolableService>("PooledMonoServiceScene");
            Assert.IsNotNull(service2, "Service should be obtained");

            Assert.IsTrue(service2== service, "Service should be the same instance returned from the pool");

            Assert.IsTrue(ServiceLocator.CheckServiceBelongsToScene<IPoolableService>(service2, "PooledMonoServiceScene", SceneManager.GetActiveScene()), "Service should be associated with the current scene");
            yield return null;

            
            SceneManager.UnloadSceneAsync( scene2);
            

        }

        [UnityTest]
        public IEnumerator DisposablePooledService_ReturnsToPool_WhenReleased()
        {
            // Arrange
            _testUtil.Register<IPoolableService, DisposablePooledMonoBehaviour>("DisposablePooledService", ServiceLifetime.Transient);
            
            // Act
            var instance = ServiceLocator.GetService<IPoolableService>("DisposablePooledService") as DisposablePooledMonoBehaviour;
            var go = instance.gameObject;
            var initialName = go.name;
            
            // Store reference to confirm it's the same GameObject later
            var instanceId = go.GetInstanceID();
            
            // Release the service
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("DisposablePooledService",instance);
            
            // Wait for async operations to complete
            yield return new WaitForSeconds(0.1f);
            
            // Assert
            Assert.IsTrue(instance.IsDisposed, "Service should be marked as disposed");
            Assert.IsTrue(instance.OnSystemDisposeCalled, "OnSystemDisposeAsync should have been called");
            Assert.IsTrue(instance.ReturnToPoolCalled, "OnReturnToPool should have been called");
            
            // Find the service-specific pool parent
            Transform servicePoolParent = null;
            foreach (Transform child in ServiceLocator.ServicePoolTransform)
            {
                if (child.name == "IPoolableService_DisposablePooledService")
                {
                    servicePoolParent = child;
                    break;
                }
            }
            
            Assert.IsNotNull(servicePoolParent, "Service pool parent should exist");
            
            // The GameObject should still exist (not destroyed)
            Assert.IsNotNull(go, "GameObject should not be destroyed");
            Assert.AreEqual(instanceId, go.GetInstanceID(), "Should be the same GameObject instance");
            Assert.IsFalse(go.activeInHierarchy, "GameObject should be inactive");
            Assert.AreEqual(servicePoolParent, go.transform.parent, "GameObject should be parented to the service-specific pool");
            
            // Get a new instance - should reuse the pooled one
            var newInstance = ServiceLocator.GetService<IPoolableService>("DisposablePooledService") as DisposablePooledMonoBehaviour;
            
            // Assert pooled instance state
            Assert.IsTrue(newInstance.TakeFromPoolCalled, "OnTakeFromPool should have been called on the new instance");
            Assert.AreEqual(instanceId, newInstance.gameObject.GetInstanceID(), "Should reuse the same GameObject instance");
            
            // Assert disposal state has been reset by the system
            Assert.IsFalse(newInstance.IsDisposed, "IsDisposed should be reset to false when taken from pool");
            Assert.IsTrue(newInstance.DisposalStateResetCalled, "DisposalStateResetCalled should be true, indicating the system called ResetDisposalState");
        }

        [UnityTest]
        public IEnumerator PoolParent_IsDestroyed_WhenServiceUnregistered()
        {
            // Register a poolable service
            _testUtil.Register<IPoolableService, PooledMonoBehaviour>("PooledServiceToUnregister", ServiceLifetime.Transient, ServiceContext.EditorOnly);
            
            // Need to find the service-specific pool parent
            Transform servicePoolParent = null;
            foreach (Transform child in ServiceLocator.ServicePoolTransform)
            {
                if (child.name == "IPoolableService_PooledServiceToUnregister")
                {
                    servicePoolParent = child;
                    break;
                }
            }
            
            Assert.IsNotNull(servicePoolParent, "Service pool parent should exist");
            
            // Get some instances to ensure the pool is initialized
            var instance1 = ServiceLocator.GetService<IPoolableService>("PooledServiceToUnregister");
            var instance2 = ServiceLocator.GetService<IPoolableService>("PooledServiceToUnregister");
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledServiceToUnregister",instance1);
            ServiceLocator.ReleaseServiceInstance<IPoolableService>("PooledServiceToUnregister", instance2);
            
            yield return new WaitForEndOfFrame();
            
            // Confirm parent still exists
            Assert.IsNotNull(servicePoolParent, "Service pool parent should still exist");
            
            // Unregister the service
            var unregisterTask = _testUtil.UnregisterService<IPoolableService>("PooledServiceToUnregister");
            
            // Wait for the unregistration to complete
            while (!unregisterTask.IsCompleted)
            {
                yield return null;
            }
            
            yield return new WaitForEndOfFrame();
            
            // Check if the parent was destroyed
            bool parentDestroyed = true;
            foreach (Transform child in ServiceLocator.ServicePoolTransform)
            {
                if (child.name == "IPoolableService_PooledServiceToUnregister")
                {
                    parentDestroyed = false;
                    break;
                }
            }
            
            Assert.IsTrue(parentDestroyed, "Service pool parent should be destroyed after unregistering");
        }

        // Interface and implementation for testing poolable services
        public interface IPoolableService
        {
            void SetPooledData(string data);
            string GetPooledData();
            bool TakeFromPoolCalled { get; }
            bool ReturnToPoolCalled { get; }
        }

        // Test implementation of a poolable regular C# service
        public class PooledTestService : IPoolableService, IServicePoolable
        {
            public int InitialPoolSize => 3;
            public int PoolExpansionSize => 2;
            
            private string _data;
            public bool TakeFromPoolCalled { get; private set; }
            public bool ReturnToPoolCalled { get; private set; }
            
            public void OnReturnToPool()
            {
                _data = null;
                ReturnToPoolCalled = true;
                TakeFromPoolCalled = false;
            }
            
            public void OnTakeFromPool()
            {
                TakeFromPoolCalled = true;
                ReturnToPoolCalled = false;
            }
            
            public void SetPooledData(string data)
            {
                _data = data;
            }
            
            public string GetPooledData()
            {
                return _data;
            }
        }





        // Test implementation of a poolable MonoBehaviour service
        [Service(typeof(IPoolableService), name: "PooledMonoService", lifetime: ServiceLifetime.Transient)]
        public class PooledMonoBehaviour : MonoBehaviour, IPoolableService, IServicePoolable
        {
            public int InitialPoolSize => 3;
            public int PoolExpansionSize => 2;
            
            private string _data;
            public bool TakeFromPoolCalled { get; private set; }
            public bool ReturnToPoolCalled { get; private set; }
            
            public void OnReturnToPool()
            {
                _data = null;
                ReturnToPoolCalled = true;
                TakeFromPoolCalled = false;
            }
            
            public void OnTakeFromPool()
            {
                TakeFromPoolCalled = true;
                ReturnToPoolCalled = false;
            }
            
            public void SetPooledData(string data)
            {
                _data = data;
            }
            
            public string GetPooledData()
            {
                return _data;
            }
        }

        // Poolable and Disposable MonoBehaviour implementation for testing
        public class DisposablePooledMonoBehaviour : MonoBehaviour, IPoolableService, IServiceDisposable, IServicePoolable
        {
            private bool _isDisposed;
            
            public bool IsDisposed => _isDisposed;
            public bool OnSystemDisposeCalled { get; private set; }
            public bool DisposalStateResetCalled { get; private set; }
            
            // IServicePoolable implementation
            public int InitialPoolSize => 1;
            public int PoolExpansionSize => 2;
            
            private string _data;
            public bool TakeFromPoolCalled { get; private set; }
            public bool ReturnToPoolCalled { get; private set; }
            
            public void OnReturnToPool()
            {
                _data = null;
                ReturnToPoolCalled = true;
                TakeFromPoolCalled = false;
            }
            
            public void OnTakeFromPool()
            {
                TakeFromPoolCalled = true;
                ReturnToPoolCalled = false;
            }
            
            public void SetPooledData(string data)
            {
                _data = data;
            }
            
            public string GetPooledData()
            {
                return _data;
            }
            
            // IServiceDisposable implementation
            public async ValueTask OnSystemDisposeAsync()
            {
                OnSystemDisposeCalled=true;
                _isDisposed = true;
                await Task.CompletedTask;
            }

            // Implement the new ResetDisposalState method explicitly
            void IServiceDisposable.ResetDisposalState(out bool isReset)
            {
                _isDisposed = false;
                OnSystemDisposeCalled = false;
                DisposalStateResetCalled = true;
                isReset = true;
            }
        }
    }
} 