using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using GAOS.ServiceLocator.Diagnostics;
using GAOS.ServiceLocator.Optional;

namespace GAOS.ServiceLocator
{
    internal static class RegularServiceFactory
    {
        public static object GetInstance(Type typeInfo, string name)
        {

            return Activator.CreateInstance(typeInfo);
        }



    }
} 