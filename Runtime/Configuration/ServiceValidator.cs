using System;
using UnityEngine;
using System.Linq;
using GAOS.ServiceLocator.Diagnostics;
using UnityEngine.SceneManagement;

namespace GAOS.ServiceLocator
{
    internal static class ServiceValidator
    {

        internal static void ValidateServiceName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Service name cannot be null or empty", nameof(name));
            }
        }

        internal static void ValidateInterfaceImplementation(Type serviceType, Type implementationType)
        {
            if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException(
                    $"Type {implementationType.Name} does not implement interface {serviceType.Name}"
                );
            }
        }

        internal static bool ValidateMonoBehaviourInstance(MonoBehaviour instance)
        {
            if (instance == null) return false;
            if (!instance.gameObject.activeInHierarchy) return false;
            if (!instance.enabled) return false;
            return true;
        }

    }
}