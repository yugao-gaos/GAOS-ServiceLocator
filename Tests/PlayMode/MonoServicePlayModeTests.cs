using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using GAOS.ServiceLocator.Editor;
using GAOS.ServiceLocator.TestUtils;
using GAOS.ServiceLocator.Optional;

namespace GAOS.ServiceLocator.Tests.PlayMode
{
    [TestFixture]
    public class MonoServicePlayModeTests
    {
        private const string TEST_SCENE_1 = "TestScene1";
        private const string TEST_SCENE_2 = "TestScene2";
        private ITestServiceUtility _registration;

        [SetUp]
        public void Setup()
        {
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: true, enableLogging: false);
            _registration = ServiceLocatorTestUtils.GetTestRegistration();
        }

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // Wait for a frame to ensure scene is properly initialized
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up test scenes
            yield return UnloadTestScenes();
            ServiceTypeCacheBuilder.RebuildTypeCache(isUnitTest: false, enableLogging: false); 
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_PersistsAcrossScenes()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.GetActiveScene();
            yield return null;
            
            // Arrange - Register service in first scene
            _registration.Register<ITestService, TestMonoBehaviour>("TestService", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("TestService");
            Assert.That(instance1, Is.Not.Null, "Service should be created in first scene");
            
            // Store initial scene path for validation
            var initialScenePath = (instance1 as MonoBehaviour).gameObject.scene.path;
            
            // Act - Load second scene
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Get service in second scene
            var instance2 = ServiceLocator.GetService<ITestService>("TestService");
            
            // Assert
            Assert.That(instance2, Is.Not.Null, "Service should be available in second scene");
            Assert.That(instance2, Is.SameAs(instance1), "Should return same instance across scenes");
            Assert.That((instance2 as MonoBehaviour).gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"), 
                "Service GameObject should be in DontDestroyOnLoad scene");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_MovesToDontDestroyOnLoadWhenCreated()
        {
            // Register and get singleton service
            _registration.Register<ITestService, TestMonoBehaviour>("SingletonTest", ServiceLifetime.Singleton);
            var instance = ServiceLocator.GetService<ITestService>("SingletonTest");
            var go = (instance as MonoBehaviour).gameObject;
            
            // Verify instance is in DontDestroyOnLoad scene
            Assert.That(go.scene.name, Is.EqualTo("DontDestroyOnLoad"), "Singleton should be moved to DontDestroyOnLoad scene");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_ReusesExistingInstance()
        {
            // Register and get singleton service multiple times
            _registration.Register<ITestService, TestMonoBehaviour>("SingletonTest2", ServiceLifetime.Singleton);
            
            var instance1 = ServiceLocator.GetService<ITestService>("SingletonTest2");
            var instance2 = ServiceLocator.GetService<ITestService>("SingletonTest2");
            var instance3 = ServiceLocator.GetService<ITestService>("SingletonTest2");
            
            // Verify all instances are the same
            Assert.That(instance2, Is.SameAs(instance1), "Second get should return same instance");
            Assert.That(instance3, Is.SameAs(instance1), "Third get should return same instance");
            
            // Verify GameObject is the same
            var go1 = (instance1 as MonoBehaviour).gameObject;
            var go2 = (instance2 as MonoBehaviour).gameObject;
            var go3 = (instance3 as MonoBehaviour).gameObject;
            
            Assert.That(go2, Is.SameAs(go1), "GameObjects should be the same");
            Assert.That(go3, Is.SameAs(go1), "GameObjects should be the same");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_SurvivesSceneUnload()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.CreateScene("Scene1");
            SceneManager.SetActiveScene(scene1);
            yield return null;

            // Register and get singleton service
            _registration.Register<ITestService, TestMonoBehaviour>("SingletonTest3", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("SingletonTest3");
            var go1 = (instance1 as MonoBehaviour).gameObject;
            
            // Create and switch to new scene
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Unload first scene
            if (scene1.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene1);
            }
            
            yield return null;
            
            // Get instance again
            var instance2 = ServiceLocator.GetService<ITestService>("SingletonTest3");
            var go2 = (instance2 as MonoBehaviour).gameObject;
            
            // Verify instance survived scene unload
            Assert.That(instance2, Is.SameAs(instance1), "Should return same instance after scene unload");
            Assert.That(go2, Is.SameAs(go1), "GameObject should survive scene unload");
            Assert.That(go2.scene.name, Is.EqualTo("DontDestroyOnLoad"), "GameObject should still be in DontDestroyOnLoad scene");
            
            // Verify instance is still valid
            Assert.That(instance2 != null && ((MonoBehaviour)instance2), "Instance should still be valid");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_PreservesStateAcrossScenes()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.CreateScene("Scene1");
            SceneManager.SetActiveScene(scene1);
            yield return null;

            // Register and get singleton service
            _registration.Register<ITestService, TestStatefulMonoBehaviour>("SingletonTest4", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("SingletonTest4") as TestStatefulMonoBehaviour;
            
            // Set some state
            instance1.SetValue("TestState");
            
            // Create and switch to new scene
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Unload first scene
            if (scene1.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene1);
            }
            
            yield return null;
            
            // Get instance in new scene
            var instance2 = ServiceLocator.GetService<ITestService>("SingletonTest4") as TestStatefulMonoBehaviour;
            
            // Verify state is preserved
            Assert.That(instance2.GetValue(), Is.EqualTo("TestState"), "State should be preserved across scenes");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransientService_CreatesNewInstanceEachTime()
        {
            // Store reference to active scene
            var activeScene = SceneManager.GetActiveScene();
            yield return null;
            
            // Arrange - Register transient service with a unique base name for this test
            _registration.Register<ITestService, TestMonoBehaviour>("TestService", ServiceLifetime.Transient);
            
            // Act - Create scope and get instances
            var instance1 = ServiceLocator.GetService<ITestService>("TestService") as TestMonoBehaviour;
            var instance2 = ServiceLocator.GetService<ITestService>("TestService") as TestMonoBehaviour;
            
            // Assert
            Assert.That(instance1, Is.Not.Null, "First instance should be created");
            Assert.That(instance2, Is.Not.Null, "Second instance should be created");
            Assert.That(instance2, Is.Not.SameAs(instance1), "Should return different instances");
            
            // Verify instances are in different GameObjects
            var go1 = instance1.gameObject;
            var go2 = instance2.gameObject;
            Assert.That(go2, Is.Not.SameAs(go1), "Instances should be in different GameObjects");
            
            // Verify GameObject names
            Assert.That(go1.name, Is.EqualTo("TestService_1"), "First GameObject should be named 'TestService_1'");
            Assert.That(go2.name, Is.EqualTo("TestService_2"), "Second GameObject should be named 'TestService_2'");
            
            // Verify both instances are in DontDestroyOnLoad scene
            Assert.That(go1.scene.name, Is.EqualTo("DontDestroyOnLoad"), "First instance should be in DontDestroyOnLoad scene");
            Assert.That(go2.scene.name, Is.EqualTo("DontDestroyOnLoad"), "Second instance should be in DontDestroyOnLoad scene");

       
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransientService_CreatesInstanceInActiveScene()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.GetActiveScene();
            yield return null;

            Debug.Log("Creating second scene");
            // Create second scene
            var scene2 = SceneManager.CreateScene("Scene2");
            yield return new WaitForEndOfFrame();
            Debug.Log("Created second scene");
            // Test instances in both scenes
            var scenes = new[] { scene1, scene2 };
            for (int i = 0; i < scenes.Length; i++)
            {
                // Switch to target scene
                SceneManager.SetActiveScene(scenes[i]);
                yield return new WaitForEndOfFrame();

                // Create service and instance for this scene
                _registration.Register<ITestService, TestMonoBehaviour>("TestService", ServiceLifetime.Transient);
                Debug.Log("Registered service");
                var instance = ServiceLocator.GetService<ITestService>("TestService") as TestMonoBehaviour;
                var go = instance.gameObject;
                Debug.Log($"Created instance in scene {scenes[i].name} + go.name: {go.name}");
                
                // Verify instance is in DontDestroyOnLoad scene
                Assert.That(go.scene.name, Is.EqualTo("DontDestroyOnLoad"), "Instance should be in DontDestroyOnLoad scene");
                Assert.That(instance.gameObject == null, Is.False, "Instance should not be disposed initially");
                
                yield return new WaitForEndOfFrame();
                Debug.Log("Starting unregistration process");
                
                // Unregister service which should trigger disposal
                var unregisterTask = _registration.UnregisterService<ITestService>("TestService");
                Debug.Log("Unregistration task created");
                
                while (!unregisterTask.IsCompleted)
                {
                    Debug.Log("Waiting for unregistration task to complete...");
                    yield return null;
                }
                Debug.Log("Unregistration task completed");
                
                yield return new WaitForEndOfFrame();
                Debug.Log("Waiting for GameObject destruction...");

                // Wait for GameObject to be destroyed
                float timeout = 5f;
                float elapsed = 0f;
                while (go != null && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (go != null)
                {
                    Debug.LogError($"GameObject still exists after {timeout}s timeout");
                }

                // Verify instance was disposed
                Assert.That(go == null, Is.True, "GameObject should be destroyed after unregistering");
                Debug.Log("Instance disposed successfully");
            }
            
            Debug.Log("Cleaning up scenes");
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_KeepsInstanceWhenLoadingAdditiveScene()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.GetActiveScene();
            yield return null;
            
            // Register and get singleton service
            _registration.Register<ITestService, TestMonoBehaviour>("AdditiveTest", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("AdditiveTest");
            var go1 = (instance1 as MonoBehaviour).gameObject;
            
            // Create second scene additively
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Get instance again
            var instance2 = ServiceLocator.GetService<ITestService>("AdditiveTest");
            var go2 = (instance2 as MonoBehaviour).gameObject;
            
            // Verify instance remains the same
            Assert.That(instance2, Is.SameAs(instance1), "Should return same instance after additive scene load");
            Assert.That(go2, Is.SameAs(go1), "GameObject should remain the same");
            Assert.That(go2.scene.name, Is.EqualTo("DontDestroyOnLoad"), "GameObject should stay in DontDestroyOnLoad scene");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_HandlesSceneReload()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.CreateScene("Scene1");
            SceneManager.SetActiveScene(scene1);
            yield return new WaitForEndOfFrame();
            
            // Register and get singleton service
            _registration.Register<ITestService, TestStatefulMonoBehaviour>("ReloadTest", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("ReloadTest") as TestStatefulMonoBehaviour;
            
            // Set initial state
            instance1.SetValue("BeforeReload");
            
            // Create and switch to new scene
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Now that we have a new active scene, unload the first scene
            if (scene1.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene1);
            }
            yield return null;
            
            // Get instance after reload
            var instance2 = ServiceLocator.GetService<ITestService>("ReloadTest") as TestStatefulMonoBehaviour;
            
            // Verify instance and state persists
            Assert.That(instance2, Is.SameAs(instance1), "Should return same instance after scene reload");
            Assert.That(instance2.GetValue(), Is.EqualTo("BeforeReload"), "State should be preserved across scene reload");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingletonService_PreservesReferencesDuringSceneOperations()
        {
            // Create initial scene
            var scene1 = SceneManager.CreateScene("Scene1");
            SceneManager.SetActiveScene(scene1);
            yield return null;

            // Create a reference object in the scene
            var refObject = new GameObject("ReferenceObject");
            var refComponent = refObject.AddComponent<TestReferenceComponent>();
            
            // Register and get singleton service
            _registration.Register<ITestService, TestReferenceMonoBehaviour>("ReferenceTest", ServiceLifetime.Singleton);
            var instance1 = ServiceLocator.GetService<ITestService>("ReferenceTest") as TestReferenceMonoBehaviour;
            
            // Set up reference and verify initial state
            instance1.SetReference(refComponent);
            Assert.That(instance1.GetReference(), Is.SameAs(refComponent), "Reference should be set initially");
            
            // Load new scene
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Get instance in new scene
            var instance2 = ServiceLocator.GetService<ITestService>("ReferenceTest") as TestReferenceMonoBehaviour;
            
            // Verify instance maintains its reference before scene unload
            Assert.That(instance2.GetReference(), Is.SameAs(refComponent), "Reference should be maintained across scene operations");
            Assert.That(refComponent != null, "Reference object should still exist before scene unload");
            
            // Unload first scene
            if (scene1.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene1);
            }
            
            yield return null;
            
            // Verify reference behavior after scene unload
            Assert.That(refComponent == null, "Referenced component should be destroyed with its scene");
            Assert.That(instance2.GetReference() == null, "Service's reference should be null after scene unload");
            Assert.That(instance2.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"), "Service should remain in DontDestroyOnLoad");
            
            // Cleanup
            if (scene2.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene2);
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransientService_HandlesAdditiveSceneLoading()
        {
            // Store reference to initial scene
            var scene1 = SceneManager.GetActiveScene();
            yield return null;
            
            // Register service
            _registration.Register<ITestService, TestMonoBehaviour>("TestService", ServiceLifetime.Transient);
            
            // Create instances in base scene
            var instance1 = ServiceLocator.GetService<ITestService>("TestService") as TestMonoBehaviour;
            var go1 = instance1.gameObject;
            
            // Create second scene additively
            var scene2 = SceneManager.CreateScene("Scene2");
            SceneManager.SetActiveScene(scene2);
            yield return new WaitForEndOfFrame();
            
            // Create instance in second scene
            var instance2 = ServiceLocator.GetService<ITestService>("TestService") as TestMonoBehaviour;
            var go2 = instance2.gameObject;
            
            // Verify instances are in DontDestroyOnLoad scene and not disposed
            Assert.That(go1.scene.name, Is.EqualTo("DontDestroyOnLoad"), "First instance should be in DontDestroyOnLoad scene");
            Assert.That(go2.scene.name, Is.EqualTo("DontDestroyOnLoad"), "Second instance should be in DontDestroyOnLoad scene");
            Assert.That(instance2, Is.Not.SameAs(instance1), "Should be different instances");
        
            
            if (scene2.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene2);
            
            yield return null;
        }

        private IEnumerator UnloadTestScenes()
        {
            // Get all loaded scenes
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name != "DontDestroyOnLoad")
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            yield return null;
        }

        // Test interfaces and classes
        public interface ITestService { }
        
        private class TestMonoBehaviour : MonoBehaviour, ITestService
        {
        }
        
        private class TestStatefulMonoBehaviour : MonoBehaviour, ITestService
        {
            private string _value;
            public void SetValue(string value) => _value = value;
            public string GetValue() => _value;
        }
        
        private class TestReferenceComponent : MonoBehaviour { }
        
        private class TestReferenceMonoBehaviour : MonoBehaviour, ITestService
        {
            private TestReferenceComponent _reference;
            public void SetReference(TestReferenceComponent reference) => _reference = reference;
            public TestReferenceComponent GetReference() => _reference;
        }

        private class DisposableTestMonoBehaviour : MonoBehaviour, ITestService, IServiceDisposable
        {
            public bool IsDisposed { get; private set; }
            
            private string _value;
            
            public async ValueTask OnSystemDisposeAsync()
            {
                IsDisposed = true;
                await Task.CompletedTask;
            }
            
            void IServiceDisposable.ResetDisposalState(out bool isReset)
            {
                IsDisposed = false;
                isReset = true;
            }
            
            public void SetValue(string value) => _value = value;
            public string GetValue() => _value;
        }

        [UnityTest]
        public IEnumerator DisposableService_ProperlyDisposesOnUnregister()
        {
            // Arrange
            _registration.Register<ITestService, DisposableTestMonoBehaviour>("DisposableTest", ServiceLifetime.Transient);
            var instance = ServiceLocator.GetService<ITestService>("DisposableTest") as DisposableTestMonoBehaviour;
            var go = instance.gameObject;
            
            // Set some state to verify
            instance.SetValue("TestValue");
            
            // Verify initial state
            Assert.That(instance.IsDisposed, Is.False, "Instance should not be disposed initially");
            Assert.That(instance.GetValue(), Is.EqualTo("TestValue"), "Instance should have correct initial value");
            Assert.That(go != null, Is.True, "GameObject should exist initially");

            // Act - Unregister service
            var unregisterTask = _registration.UnregisterService<ITestService>("DisposableTest");
            
            // Wait for unregistration to complete
            while (!unregisterTask.IsCompleted)
            {
                yield return null;
            }

            // Wait a frame to allow Unity to process the destruction
            yield return null;

            // Assert
            Assert.That(instance.IsDisposed, Is.True, "Instance should be disposed after unregistering");
            Assert.That(go == null, Is.True, "GameObject should be destroyed after unregistering");
        }

        [UnityTest]
        public IEnumerator DisposableService_HandlesMultipleInstances()
        {
            // Arrange
            _registration.Register<ITestService, DisposableTestMonoBehaviour>("DisposableTest", ServiceLifetime.Transient);
            
            // Create multiple instances
            var instance1 = ServiceLocator.GetService<ITestService>("DisposableTest") as DisposableTestMonoBehaviour;
            var instance2 = ServiceLocator.GetService<ITestService>("DisposableTest") as DisposableTestMonoBehaviour;
            
            var go1 = instance1.gameObject;
            var go2 = instance2.gameObject;
            
            // Set different values to verify
            instance1.SetValue("Instance1");
            instance2.SetValue("Instance2");
            
            // Verify initial state
            Assert.That(instance1.IsDisposed, Is.False, "First instance should not be disposed initially");
            Assert.That(instance2.IsDisposed, Is.False, "Second instance should not be disposed initially");
            Assert.That(instance1.GetValue(), Is.EqualTo("Instance1"), "First instance should have correct value");
            Assert.That(instance2.GetValue(), Is.EqualTo("Instance2"), "Second instance should have correct value");

            // Act - Unregister service
            var unregisterTask = _registration.UnregisterService<ITestService>("DisposableTest");
            
            // Wait for unregistration to complete
            while (!unregisterTask.IsCompleted)
            {
                yield return null;
            }

            // Wait a frame to allow Unity to process the destruction
            yield return null;

            // Assert
            Assert.That(instance1.IsDisposed, Is.True, "First instance should be disposed");
            Assert.That(instance2.IsDisposed, Is.True, "Second instance should be disposed");
            Assert.That(go1 == null, Is.True, "First GameObject should be destroyed");
            Assert.That(go2 == null, Is.True, "Second GameObject should be destroyed");
        }
    }
} 