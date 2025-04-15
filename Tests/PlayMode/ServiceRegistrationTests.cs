using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.TestUtils;
using GAOS.ServiceLocator;
using GAOS.ServiceLocator.Editor;

namespace GAOS.ServiceLocator.Tests
{
    internal class ServiceRegistrationTests
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

        [Test]
        public void RegisterRegularService_ShouldWork()
        {
            // Arrange
            const string serviceName = "TestService";

            // Act - Register as singleton explicitly
            _registration.Register<ITestService, TestService>(serviceName, ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Assert
            var service = ServiceLocator.GetService<ITestService>(serviceName);
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.TypeOf<TestService>());
        }

        [UnityTest]
        public IEnumerator RegisterMonoBehaviourService_ShouldWork()
        {
            // Arrange
            const string serviceName = "TestMonoService";
            var go = new GameObject(serviceName);
            go.AddComponent<TestMonoBehaviourService>();

            // Act
            _registration.Register<ITestService, TestMonoBehaviourService>(serviceName, ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            
            yield return null;

            // Assert
            var service = ServiceLocator.GetService<ITestService>(serviceName);
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.TypeOf<TestMonoBehaviourService>());

            // Cleanup
            Object.Destroy(go);
        }

        [Test]
        public void RegisterScriptableObjectService_ShouldWork()
        {
            // Arrange
            const string serviceName = "TestSOService";
            var so = ScriptableObject.CreateInstance<TestScriptableObjectService>();

            // Act
            _registration.Register<ITestService, TestScriptableObjectService>(serviceName, ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Assert
            var service = ServiceLocator.GetService<ITestService>(serviceName);
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.TypeOf<TestScriptableObjectService>());

            // Cleanup
            Object.Destroy(so);
        }

       

        // Test interfaces and classes
        public interface ITestService { }
        
        [Service(typeof(ITestService), "TestService", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
        private class TestService : ITestService { }
        
        [Service(typeof(ITestService), "TestMonoService", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
        private class TestMonoBehaviourService : MonoBehaviour, ITestService { }
        
        [Service(typeof(ITestService), "TestSOService", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
        private class TestScriptableObjectService : ScriptableObject, ITestService { }
    }
} 