using GAOS.Logger;
using UnityEngine;

namespace GAOS.ServiceLocator.Diagnostics
{
    public class ServiceLocatorLogSystem : ILogSystem
    {
        public string LogPrefix => "[ServiceLocator]";
        public string LogPrefixColor => "#00FFFF"; // Cyan color for visibility
        public LogLevel DefaultLogLevel => LogLevel.Info;

        private static ServiceLocatorLogSystem _instance;
        public static ServiceLocatorLogSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServiceLocatorLogSystem();
                    GLog.RegisterSystem(_instance);
                }
                return _instance;
            }
        }
    }
} 