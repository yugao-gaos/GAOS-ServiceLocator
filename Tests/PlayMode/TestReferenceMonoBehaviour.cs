using UnityEngine;
using GAOS.ServiceLocator.Tests;

namespace GAOS.ServiceLocator.Tests.PlayMode
{
    public class TestReferenceMonoBehaviour : MonoBehaviour, ITestService
    {
        private TestReferenceComponent _reference;
        
        public void SetReference(TestReferenceComponent reference) => _reference = reference;
        public TestReferenceComponent GetReference() => _reference;
        public string GetValue() => "TestReferenceMonoBehaviour";
    }
} 