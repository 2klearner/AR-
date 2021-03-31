using System;
using UnityEngine;

namespace easyar
{
    public class EasyARController : MonoBehaviour
    {
        public bool ShowPopupMessage = true;

        private static EasyARSettings settings;

        public static EasyARController Instance { get; private set; }

        public static bool Initialized { get; private set; }


        public static bool ARCoreLoadFailed { get; private set; }

            public static DelayedCallbackScheduler Scheduler { get; private set; }


        public static EasyARSettings Settings
        {
            get
            {
                if (!settings)
                {
                    settings = Resources.Load<EasyARSettings>(settingsPath);
                }
                return settings;
            }
        }
        private static string settingsPath { get { return "EasyAR/Settings"; } }


        public ThreadWorker Worker { get; private set; }

        internal Display Display { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void GlobalInitialization()
        {
            Debug.Log("EasyAR Sense Unity Plugin Version " + EasyARVersion.FullVersion);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Settings.ARCoreSupport)
            {
                try
                {
                    using (var systemClass = new AndroidJavaClass("java.lang.System"))
                    {
                        systemClass.CallStatic("loadLibrary", "arcore_sdk_c");
                    }
                }
                catch (AndroidJavaException)
                {
                    ARCoreLoadFailed = true;
                }
            }
#endif
            Initialized = false;
            Scheduler = new DelayedCallbackScheduler();
#if UNITY_EDITOR
            Log.setLogFuncWithScheduler(Scheduler, (LogLevel, msg) =>
            {
                switch (LogLevel)
                {
                    case LogLevel.Error:
                        Debug.LogError(msg);
                        break;
                    case LogLevel.Warning:
                        Debug.LogWarning(msg);
                        break;
                    case LogLevel.Info:
                        Debug.Log(msg);
                        break;
                    default:
                        break;
                }
            });
#endif
            var key = Settings.LicenseKey;
            System.AppDomain.CurrentDomain.DomainUnload += (sender, e) =>
            {
#if UNITY_EDITOR
                Log.resetLogFunc();
#endif
                if (Scheduler != null)
                {
                    Scheduler.Dispose();
                }
                settings = null;
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var easyarEngineClass = new AndroidJavaClass("cn.easyar.Engine"))
            {
                var activityclassloader = currentActivity.Call<AndroidJavaObject>("getClass").Call<AndroidJavaObject>("getClassLoader");
                if (activityclassloader == null)
                {
                    Debug.Log("ActivityClassLoader is null");
                }
                easyarEngineClass.CallStatic("loadLibraries");
                if (!easyarEngineClass.CallStatic<bool>("setupActivity", currentActivity))
                {
                    Debug.LogError("EasyAR Sense Initialize Fail");
                    Initialized = false;
                    return;
                }
            }
#endif
            if (!Engine.initialize(key.Trim()))
            {
                Debug.LogError("EasyAR Sense Initialize Fail");
                Initialized = false;
                return;
            }
            else
            {
                Initialized = true;
            }
            System.AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Debug.Log("UnhandledException: " + e.ExceptionObject.ToString());
            };
        }

        private void Awake()
        {
            Instance = this;
            Display = new Display();
            Worker = new ThreadWorker();
            if (!Initialized)
            {
                ShowErrorMessage();
            }
        }

        private void Update()
        {
            if (!Initialized)
            {
                return;
            }
            var error = Engine.errorMessage();
            if (!string.IsNullOrEmpty(error))
            {
                ShowErrorMessage();
                Initialized = false;
            }
            if (Scheduler != null)
            {
                while (Scheduler.runOne())
                {
                }
            }
        }


        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                Engine.onPause();
            }
            else
            {
                Engine.onResume();
            }
        }


        private void OnDestroy()
        {
            Worker.Dispose();
            Display.Dispose();
        }

        private void ShowErrorMessage()
        {
            if (Application.isEditor || string.IsNullOrEmpty(Settings.LicenseKey))
            {
                GUIPopup.EnqueueMessage(Engine.errorMessage() + Environment.NewLine +
                    "Fill a valid Key in EasyAR Settings Asset" + Environment.NewLine +
                    "Menu entry: <EasyAR/Change License Key>" + Environment.NewLine +
                    "Asset Path: " + settingsPath + Environment.NewLine +
                    "Get from EasyAR Develop Center (www.easyar.com) -> SDK Authorization", 10000);
            }
            else
            {
                GUIPopup.EnqueueMessage(Engine.errorMessage() + Environment.NewLine +
                    "Get from EasyAR Develop Center (www.easyar.com) -> SDK Authorization", 10000);
            }
        }
    }
}
