using System;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.Editor;
using GAOS.ServiceLocator.Optional;
using GAOS.ServiceLocator.TestUtils;

namespace GAOS.ServiceLocator.Tests
{
    [TestFixture]
    public class ServiceLocatorTests
    {
        private ITestServiceUtility _registration;

        [SetUp]
        public void Setup()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false); 
            _registration = ServiceLocatorTestUtils.GetTestRegistration();
            _registration.Clear();
            ServiceLocator.SetSkipEditorContextValidation(true);
        }

        [TearDown]
        public void Cleanup()
        {
            _registration.Clear();
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false);
        }

        [Test]
        public void Get_SingletonService_ReturnsSameInstance()
        {
            // Arrange
            _registration.Register<ITestService, TestService>("TestService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Act
            var instance1 = ServiceLocator.GetService<ITestService>("TestService");
            var instance2 = ServiceLocator.GetService<ITestService>("TestService");

            // Assert
            Assert.That(instance1, Is.Not.Null);
            Assert.That(instance2, Is.Not.Null);
            Assert.That(instance1, Is.SameAs(instance2));
            Assert.That(instance1.GetValue(), Is.EqualTo("TestService"));
        }

        [Test]
        public void Get_TransientService_WithoutScope_ThrowsException()
        {
            // Assert
            Assert.Throws<InvalidOperationException>(() => 
                ServiceLocator.GetService<ITestService>("TransientService"));
        }

     

       
        [Test]
        public  void GetServiceNames_ReturnsCorrectNames()
        {
            // Arrange
            _registration.Register<ITestService, TestService>("TestService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            _registration.Register<ITestService, TransientTestService>("TransientService", ServiceLifetime.Transient, ServiceContext.EditorOnly);
            _registration.Register<ITestService, EditorOnlyService>("EditorService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            _registration.Register<ITestService, TestSOService>("TestSOService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Act
            var names = ServiceLocator.GetServiceNames<ITestService>().ToList();
            Debug.Log($"Found service names for ITestService:");
            foreach (var name in names)
            {
                Debug.Log($"- {name}");
            }

            // Assert
            Assert.That(names, Is.Not.Null);
            Assert.That(names, Is.EquivalentTo(new[] { 
                "TestService",
                "TransientService", 
                "EditorService",
                "TestSOService"
            }), "Expected all registered services");
        }

       
    }
} 