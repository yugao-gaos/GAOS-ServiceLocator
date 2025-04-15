using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GAOS.ServiceLocator.Tracking
{
    internal class ServiceTracker
    {
        private class ServiceTypeStats
        {
            public int TotalCreated { get; private set; }
            public DateTime LastCreated { get; private set; }
            public List<WeakReference> ActiveInstances { get; } = new List<WeakReference>();

            public void IncrementCreated()
            {
                TotalCreated++;
                LastCreated = DateTime.UtcNow;
            }

            public void CleanupStaleReferences()
            {
                ActiveInstances.RemoveAll(wr => !wr.IsAlive);
            }
        }

        private static readonly Dictionary<Type, ServiceTypeStats> _stats = new Dictionary<Type, ServiceTypeStats>();
        private static readonly object _lock = new object();

        public static void TrackInstance(object instance, Type serviceType)
        {
            if (instance == null) return;

            lock (_lock)
            {
                if (!_stats.TryGetValue(serviceType, out var stats))
                {
                    stats = new ServiceTypeStats();
                    _stats[serviceType] = stats;
                }

                stats.IncrementCreated();
                stats.ActiveInstances.Add(new WeakReference(instance));
                stats.CleanupStaleReferences();

                Debug.Log($"[ServiceTracker] Created new instance of {serviceType.Name}\n" +
                         $"Total Created: {stats.TotalCreated}\n" +
                         $"Active Instances: {stats.ActiveInstances.Count(wr => wr.IsAlive)}");
            }
        }

        public static void UntrackInstance(object instance)
        {
            if (instance == null) return;

            lock (_lock)
            {
                var serviceType = instance.GetType();
                if (_stats.TryGetValue(serviceType, out var stats))
                {
                    stats.ActiveInstances.RemoveAll(wr => !wr.IsAlive || wr.Target == instance);
                    Debug.Log($"[ServiceTracker] Untracked instance of {serviceType.Name}\n" +
                             $"Active Instances: {stats.ActiveInstances.Count(wr => wr.IsAlive)}");
                }
            }
        }

        public static ServiceTrackerStats GetStats(Type serviceType)
        {
            lock (_lock)
            {
                if (_stats.TryGetValue(serviceType, out var stats))
                {
                    stats.CleanupStaleReferences();
                    return new ServiceTrackerStats
                    {
                        TotalCreated = stats.TotalCreated,
                        LastCreated = stats.LastCreated,
                        ActiveInstanceCount = stats.ActiveInstances.Count(wr => wr.IsAlive)
                    };
                }

                return new ServiceTrackerStats();
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _stats.Clear();
            }
        }
    }

    public struct ServiceTrackerStats
    {
        public int TotalCreated { get; set; }
        public DateTime LastCreated { get; set; }
        public int ActiveInstanceCount { get; set; }
    }
} 