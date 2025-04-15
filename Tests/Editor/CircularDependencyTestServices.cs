using UnityEngine;

namespace GAOS.ServiceLocator.Tests
{
    // Test Case 2: Self-Referential Dependency
    public interface ISelfRefService { }
    [Service(typeof(ISelfRefService), "SelfRef", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class SelfRefService : ISelfRefService
    {
        private readonly ISelfRefService _self;  // Self-referential dependency through field
        
        public SelfRefService(ISelfRefService self)  // Self-referential dependency through constructor
        {
            _self = self;
        }
    }

    // Test Case 3: Interface vs Implementation Circular Dependency
    public interface IServiceA { }
    public interface IServiceB { }

    [Service(typeof(IServiceA), "ServiceA", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceAImpl : IServiceA
    {
        private readonly IServiceB _b;
        public ServiceAImpl(IServiceB b)
        {
            _b = b;
        }
    }

    [Service(typeof(IServiceB), "ServiceB", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceBImpl : IServiceB
    {
        private readonly ServiceAImpl _a;  // Note: Directly depending on implementation
        public ServiceBImpl(ServiceAImpl a)
        {
            _a = a;
        }
    }

    // Test Case 5: Nested Circular Dependency
    public interface IOuterA { }
    public interface IOuterB { }
    public interface IInnerC { }
    public interface IInnerD { }

    [Service(typeof(IOuterA), "OuterA", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class OuterAService : IOuterA
    {
        private readonly IOuterB _b;
        public OuterAService(IOuterB b)
        {
            _b = b;
        }
    }

    [Service(typeof(IOuterB), "OuterB", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class OuterBService : IOuterB
    {
        private readonly IInnerC _c;
        public OuterBService(IInnerC c)
        {
            _c = c;
        }
    }

    [Service(typeof(IInnerC), "InnerC", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class InnerCService : IInnerC
    {
        private readonly IInnerD _d;
        public InnerCService(IInnerD d)
        {
            _d = d;
        }
    }

    [Service(typeof(IInnerD), "InnerD", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class InnerDService : IInnerD
    {
        private readonly IInnerC _c;
        public InnerDService(IInnerC c)
        {
            _c = c;
        }
    }

    // Test Case 6: Transient vs Singleton Mixed Circular Dependency
    public interface ISingletonService { }
    public interface ITransientService { }

    [Service(typeof(ISingletonService), "Singleton", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class SingletonService : ISingletonService
    {
        private readonly ITransientService _transient;
        public SingletonService(ITransientService transient)
        {
            _transient = transient;
        }
    }

    [Service(typeof(ITransientService), "Transient", ServiceLifetime.Transient, ServiceContext.EditorOnly)]
    public class TransientService : ITransientService
    {
        private readonly ISingletonService _singleton;
        public TransientService(ISingletonService singleton)
        {
            _singleton = singleton;
        }
    }

    // Test Case A: Service -> Non-Service Class -> Service Dependency
    public interface IServiceWithHelper { }
    public interface IDependentService { }

    public class NonServiceHelper
    {
        private readonly IDependentService _dependent;
        public NonServiceHelper(IDependentService dependent)
        {
            _dependent = dependent;
        }
    }

    [Service(typeof(IServiceWithHelper), "ServiceWithHelper", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceWithHelper : IServiceWithHelper
    {
        private readonly NonServiceHelper _helper;
        private readonly IDependentService _dependent;
        public ServiceWithHelper(IDependentService dependent)
        {
            _dependent = dependent;
            _helper = new NonServiceHelper(dependent);
        }
    }

    [Service(typeof(IDependentService), "DependentService", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class DependentService : IDependentService
    {
        private readonly IServiceWithHelper _service;
        public DependentService(IServiceWithHelper service)
        {
            _service = service;
        }
    }

    // Test Case B: Long Chain Deep Seek
    public interface IServiceOne { }
    public interface IServiceTwo { }
    public interface IServiceThree { }
    public interface IServiceFour { }
    public interface IServiceFive { }
    public interface IServiceSix { }

    [Service(typeof(IServiceOne), "One", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceOne : IServiceOne
    {
        private readonly IServiceTwo _two;
        public ServiceOne(IServiceTwo two)
        {
            _two = two;
        }
    }

    [Service(typeof(IServiceTwo), "Two", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceTwo : IServiceTwo
    {
        private readonly IServiceThree _three;
        public ServiceTwo(IServiceThree three)
        {
            _three = three;
        }
    }

    [Service(typeof(IServiceThree), "Three", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceThree : IServiceThree
    {
        private readonly IServiceFour _four;
        public ServiceThree(IServiceFour four)
        {
            _four = four;
        }
    }

    [Service(typeof(IServiceFour), "Four", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceFour : IServiceFour
    {
        private readonly IServiceFive _five;
        public ServiceFour(IServiceFive five)
        {
            _five = five;
        }
    }

    [Service(typeof(IServiceFive), "Five", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceFive : IServiceFive
    {
        private readonly IServiceSix _six;
        public ServiceFive(IServiceSix six)
        {
            _six = six;
        }
    }

    [Service(typeof(IServiceSix), "Six", ServiceLifetime.Singleton, ServiceContext.EditorOnly)]
    public class ServiceSix : IServiceSix
    {
        private readonly IServiceOne _one;
        public ServiceSix(IServiceOne one)
        {
            _one = one;
        }
    }
} 