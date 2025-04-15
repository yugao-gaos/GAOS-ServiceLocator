using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Editor;

namespace GAOS.ServiceLocator.Tests
{
    [TestFixture]
    public class CircularDependencyTests
    {
        private ServiceTypeCache _typeCache;
        [SetUp]
        public void Setup()
        {
                    
            // Expect the log message for the implementation circular dependency
             _typeCache = ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false);  // Reset to normal state
        }

        // Test Case 2: Self-Referential Dependency
        [Test]
        public void SelfReferentialDependency_DetectsCircular()
        {
           
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(SelfRefService), _typeCache);
           
            // Assert
            Assert.That(result.hasCircular, Is.True); 
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: ISelfRefService (SelfRefService) → ISelfRefService (SelfRefService)");
        }

        // Test Case 3: Interface vs Implementation Circular Dependency
        [Test]
        public void InterfaceImplementationMixedDependency_DetectsCircular()
        {
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(ServiceAImpl), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            Assert.That(result.isImplementationDependency, Is.True);
            LogAssert.Expect(LogType.Error, "Implementation circular dependency detected: IServiceA (ServiceAImpl) → IServiceB (ServiceBImpl) → IServiceA (ServiceAImpl)");
        }

        // Test Case 5: Nested Circular Dependency
        [Test]
        public void NestedCircularDependency_DetectsCircular()
        {

            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(OuterAService), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: IOuterA (OuterAService) → IOuterB (OuterBService) → IInnerC (InnerCService) → IInnerD (InnerDService) → IInnerC");
        }

        // Test Case 6: Transient vs Singleton Mixed Circular Dependency
        [Test]
        public void TransientSingletonMixedDependency_DetectsCircular()
        {
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(SingletonService), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: ISingletonService (SingletonService) → ITransientService (TransientService) → ISingletonService (SingletonService)");
        }

        // Test Case A: Service -> Non-Service Class -> Service Dependency
        [Test]
        public void NonServiceClassDependency_DetectsCircular()
        {
          
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(ServiceWithHelper), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: IServiceWithHelper (ServiceWithHelper) → IDependentService (DependentService) → IServiceWithHelper (ServiceWithHelper)");
        }

        // Test Case B: Long Chain Deep Seek
        [Test]
        public void LongChainDeepSeek_DetectsCircular()
        {
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(ServiceOne), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: IServiceOne (ServiceOne) → IServiceTwo (ServiceTwo) → IServiceThree (ServiceThree) → IServiceFour (ServiceFour) → IServiceFive (ServiceFive) → IServiceSix (ServiceSix) → IServiceOne (ServiceOne)");
        }

        // Test Case C: Additional Declared Dependencies with ServiceLocator.Get
        [Test]
        public void AdditionalDeclaredDependencies_DetectsCircular()
        {
            // Act
            var result = ServiceEditorValidator.ValidateDependencies(typeof(ServiceWithGetCall), _typeCache);

            // Assert
            Assert.That(result.hasCircular, Is.True);
            LogAssert.Expect(LogType.Warning, "Interface Circular dependency detected: IServiceWithGetCall (ServiceWithGetCall) → IGetDependentService (GetDependentService) → IServiceWithGetCall (ServiceWithGetCall)");
        }

        // Test interfaces and implementations for ServiceLocator.Get test
        public interface IServiceWithGetCall { }
        public interface IGetDependentService { }

        [Service(typeof(IServiceWithGetCall), "ServiceWithGetCall", additionalDependencies: new[] { typeof(IGetDependentService) })]
        public class ServiceWithGetCall : IServiceWithGetCall
        {
            public void DoSomething()
            {
                // This Get call creates a circular dependency that should be detected
                var dependency = ServiceLocator.GetService<IGetDependentService>("GetDependentService");
            }
        }

        [Service(typeof(IGetDependentService), "GetDependentService", additionalDependencies: new[] { typeof(IServiceWithGetCall) })]
        public class GetDependentService : IGetDependentService
        {
            public void DoSomething()
            {
                // This Get call creates a circular dependency that should be detected
                var service = ServiceLocator.GetService<IServiceWithGetCall>("ServiceWithGetCall");
            }
        }
    }
} 