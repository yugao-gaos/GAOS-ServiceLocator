using UnityEngine;

namespace GAOS.ServiceLocator
{
    /// <summary>
    /// Singleton MonoBehaviour that manages pooled service instances
    /// </summary>
    public class ServicePool : MonoBehaviour
    {
        private static ServicePool _instance;
        
        /// <summary>
        /// Singleton instance with automatic creation if needed
        /// </summary>
        public static ServicePool Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindObjectOfType<ServicePool>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var go = new GameObject("ServicePool");
                        _instance = go.AddComponent<ServicePool>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Another instance exists, destroy this one
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
