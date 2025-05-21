using GAOS.Logger;
using UnityEngine;

namespace GAOS.ServiceLocator.Editor.Diagnostics
{
    public class ServiceLocatorEditorLogSystem : ILogSystem
    {
        public string LogPrefix => "[ServiceLocator.Editor]";
        public string LogPrefixColor => "#00FFFF"; // Cyan color to match runtime logger
        public LogLevel DefaultLogLevel => LogLevel.Debug; // More verbose by default in editor

        private static ServiceLocatorEditorLogSystem _instance;
        public static ServiceLocatorEditorLogSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServiceLocatorEditorLogSystem();
                    GLog.RegisterSystem(_instance);
                }
                return _instance;
            }
        }
    }
} 