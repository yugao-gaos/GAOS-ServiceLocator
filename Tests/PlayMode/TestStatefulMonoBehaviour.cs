using UnityEngine;
using GAOS.ServiceLocator.Tests;

namespace GAOS.ServiceLocator.Tests.PlayMode
{
    public class TestStatefulMonoBehaviour : MonoBehaviour, ITestService
    {
        private string _value = "Initial";
        
        public void SetValue(string value) => _value = value;
        public string GetValue() => _value;
    }
} 