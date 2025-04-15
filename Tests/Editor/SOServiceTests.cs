using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using GAOS.ServiceLocator.Editor;
using GAOS.ServiceLocator.Optional;
using GAOS.ServiceLocator.TestUtils;

namespace GAOS.ServiceLocator.Tests
{
    public class SOServiceTests
    {
        private ITestServiceUtility _registration;

        [SetUp]
        public void Setup()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false); 
            _registration = ServiceLocatorTestUtils.GetTestRegistration();
        }

        [TearDown]
        public void Cleanup()
        {
            _registration.Clear();
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false);
        }

        [UnityTest]
        public IEnumerator SOService_RegisterAndGet_ReturnsSameInstance()
        {
            // Arrange
            _registration.Register<TestSOService, TestSOService>("TestSO", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            yield return null;

            // Act
            var instance1 = ServiceLocator.GetService<TestSOService>("TestSO");
            var instance2 = ServiceLocator.GetService<TestSOService>("TestSO");

            // Assert
            Assert.That(instance1, Is.Not.Null);
            Assert.That(instance2, Is.Not.Null);
            Assert.That(instance1, Is.SameAs(instance2));
            Assert.That(instance1.GetValue(), Is.EqualTo("TestSOService"));
        }

        [UnityTest]
        public IEnumerator SOService_Cleanup_ClearsInstance()
        {
            // Arrange
            _registration.Register<TestSOService, TestSOService>("TestSO", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            yield return null;

            var instance = ServiceLocator.GetService<TestSOService>("TestSO");
            Assert.That(instance, Is.Not.Null);

            // Act
            var task = _registration.UnregisterService<TestSOService>("TestSO");

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.GetService<TestSOService>("TestSO"));
        }

        [UnityTest]
        public IEnumerator SOService_TransientLifetime_ReturnsNewInstance()
        {
            // Arrange
            _registration.Register<ITransientService, TransientSOService>("TransientSOService", ServiceLifetime.Transient, ServiceContext.EditorOnly);
         
            // Act
            var instance1 = ServiceLocator.GetService<ITransientService>("TransientSOService");
            var instance2 = ServiceLocator.GetService<ITransientService>("TransientSOService");

            // Assert
            Assert.That(instance1, Is.Not.Null);
            Assert.That(instance2, Is.Not.Null);
            Assert.That(instance1, Is.Not.SameAs(instance2), "Transient ScriptableObject services should return new instances");

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransientService_Disposal_DisposesCorrectly()
        {
            // Arrange
            _registration.Register<ITransientService, TransientSOService>("TransientSOService", ServiceLifetime.Transient, ServiceContext.EditorOnly);
            
            // Act - Get multiple instances
            var instance1 = ServiceLocator.GetService<ITransientService>("TransientSOService") as TransientSOService;
            var instance2 = ServiceLocator.GetService<ITransientService>("TransientSOService") as TransientSOService;
            
            Assert.That(instance1.IsDisposed, Is.False, "Instance 1 should not be disposed initially");
            Assert.That(instance2.IsDisposed, Is.False, "Instance 2 should not be disposed initially");

            // Clear registration which should trigger disposal
            _registration.Clear();
            
            // Wait a frame to allow async disposal to complete
            yield return null;

            // Assert
            Assert.That(instance1.IsDisposed, Is.True, "Instance 1 should be disposed after clearing");
            Assert.That(instance2.IsDisposed, Is.True, "Instance 2 should be disposed after clearing");
        }

        [UnityTest]
        public IEnumerator TransientService_IndividualDisposal_WorksCorrectly()
        {
            // Arrange
            _registration.Register<ITransientService, TransientSOService>("TransientSOService", ServiceLifetime.Transient, ServiceContext.EditorOnly);
            
            // Act - Get multiple instances
            var instance1 = ServiceLocator.GetService<ITransientService>("TransientSOService") as TransientSOService;
            var instance2 = ServiceLocator.GetService<ITransientService>("TransientSOService") as TransientSOService;
            
            // Dispose first instance manually
            ServiceLocator.ReleaseServiceInstance<ITransientService>("TransientSOService",instance1);
            
            // Assert
            Assert.That(instance1.IsDisposed, Is.True, "Instance 1 should be disposed");
            Assert.That(instance2.IsDisposed, Is.False, "Instance 2 should still be active");
            
            // Get a new instance after disposing one
            var instance3 = ServiceLocator.GetService<ITransientService>("TransientSOService") as TransientSOService;
            Assert.That(instance3.IsDisposed, Is.False, "New instance should not be disposed");
            Assert.That(instance3, Is.Not.SameAs(instance1), "New instance should be different from disposed instance");

            yield return null;
        }

       

       
    }
} 