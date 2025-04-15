using UnityEngine;
using System;
using System.Linq;
using GAOS.ServiceLocator.Diagnostics;
using Object = UnityEngine.Object;

namespace GAOS.ServiceLocator
{
    internal static class MonoServiceFactory
    {
       

        public static MonoBehaviour FindSceneInstance(Type typeInfo, string name, string sceneName)
        {
            Object[] objects = Object.FindObjectsByType(typeInfo, FindObjectsSortMode.None);

            foreach(Object obj in objects)
            {
                if(obj is MonoBehaviour mb)
                {
                    if(mb.gameObject.scene.name == sceneName)
                    {
                        if(mb.gameObject.name == name)
                        {
                            return mb;
                        }
                    }           
                }
            }
            return null;
        }

     

      

   
        public static MonoBehaviour CreateInstance(Type typeInfo, string name)
        {
            // This method must be called from the main thread
            if (!ServiceCoroutineRunner.IsMainThread())
            {
                throw new InvalidOperationException(
                    "CreateInstance must be called from the main thread. " +
                    "Use ServiceCoroutineRunner.RunOnMainThread to schedule this operation."
                );
            }
            
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);

            var instance = go.AddComponent(typeInfo) as MonoBehaviour;
            if (instance == null)
            {
                UnityEngine.Object.Destroy(go);
                throw new ServiceInitializationException(
                    $"Failed to create instance of {typeInfo.Name}. " +
                    "Make sure it inherits from MonoBehaviour."
                );
            }
            return instance;
        }


    
    }
} 