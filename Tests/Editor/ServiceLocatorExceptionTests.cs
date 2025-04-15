using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Editor;

namespace GAOS.ServiceLocator.Tests
{
    [TestFixture]
    public class ServiceLocatorExceptionTests
    {
        private ServiceTypeCache _typeCache;

        [SetUp]
        public void Setup()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false); 
            ServiceLocator.SetSkipEditorContextValidation(true);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false); 
            ServiceLocator.SetSkipEditorContextValidation(false);
        }

        [Test]
        public void Register_DuplicateService_ThrowsException()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestService>("TestService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ServiceLocator.Register<ITestService, TestService>("TestService", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            });
            Assert.That(ex.Message, Contains.Substring("is already registered"));
        }

        [Test]
        public void Get_UnregisteredService_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ServiceLocator.GetService<ITestService>("NonExistentService");
            });
            Assert.That(ex.Message, Contains.Substring("is not registered"));
        }

       

        [Test]
        public void Register_EmptyName_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                ServiceLocator.Register<ITestService, TestService>("", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            });
            Assert.That(ex.Message, Contains.Substring("name cannot be null or empty"));
        }

        [Test]
        public void Register_NullName_ThrowsException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                ServiceLocator.Register<ITestService, TestService>(null, ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            });
            Assert.That(ex.Message, Contains.Substring("name cannot be null or empty"));
        }

        [Test]
        public void Register_DifferentImplementations_ReturnsCorrectInstances()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestService>("Service1", ServiceLifetime.Singleton, ServiceContext.EditorOnly);
            ServiceLocator.Register<ITestService, DifferentService>("Service2", ServiceLifetime.Singleton, ServiceContext.EditorOnly);

            // Act
            var instance1 = ServiceLocator.GetService<ITestService>("Service1");
            var instance2 = ServiceLocator.GetService<ITestService>("Service2");

            // Assert
            Assert.That(instance1, Is.TypeOf<TestService>());
            Assert.That(instance2, Is.TypeOf<DifferentService>());
            Assert.That(instance1.GetValue(), Is.EqualTo("TestService"));
            Assert.That(instance2.GetValue(), Is.EqualTo("DifferentService"));
        }

    


        [Test]
        public void TryGet_UnregisteredService_ReturnsFalse()
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ServiceLocator.GetService<ITestService>("NonExistentService");
            });
            Assert.That(ex.Message, Contains.Substring("is not registered"));
        }

        // Helper classes for testing
        private interface ICircularA { }
        private interface ICircularB { }

        private class CircularA : ICircularA
        {
            private ICircularB _b;
            public CircularA()
            {
                _b = ServiceLocator.GetService<ICircularB>("CircularB");
            }
        }

        private class CircularB : ICircularB
        {
            private ICircularA _a;
            public CircularB()
            {
                _a = ServiceLocator.GetService<ICircularA>("CircularA");
            }
        }


        private class DifferentService : ITestService
        {
            public string GetValue() => "DifferentService";
        }
    }
} 