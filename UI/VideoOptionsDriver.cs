using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using BelmontsCurseMasterOptimizer.Patches;

namespace BelmontsCurseMasterOptimizer.UI
{
    public class VideoOptionsDriver : MonoBehaviour
    {
        private float refresh;
        private float tweakTimer;

        public VideoOptionsDriver(IntPtr p) : base(p)
        {
        }

        private void Update()
        {
            // 1. Dynamic Zoom Optimization (supports perspective pullback)
            CameraZoomPatches.UpdateZoomOptimization();

            // 2. Map Screen Black-Screen & Freeze Watchdog
            MapPatches.FailsafeCameraWatchdog();
            MapPatches.MapRefreshTick();
            ParticlePatches.Tick();

            // 3. Poll asynchronous string table loading after language changes
            VideoOptions.PollPendingLocaleStrings(Time.unscaledDeltaTime);

            // 6. Periodic engine maintenance every 1 second: upload buffer and GC pacing
            tweakTimer -= Time.unscaledDeltaTime;
            if (tweakTimer <= 0f)
            {
                tweakTimer = 1.0f;
                StutterAndGCPatches.ApplyEngineTweaks();
            }

            if (!VideoOptions.Registered)
            {
                refresh -= Time.unscaledDeltaTime;
                if (refresh > 0f)
                {
                    return;
                }
                refresh = 0.5f;

                foreach (UnityEngine.Object item in (Il2CppArrayBase<UnityEngine.Object>)(object)Resources.FindObjectsOfTypeAll(Il2CppType.Of<VideoSettings>()))
                {
                    VideoSettings val = ((Il2CppObjectBase)item).Cast<VideoSettings>();
                    Scene scene = ((Component)val).gameObject.scene;
                    if (scene.IsValid() && ((CommonSettings)val)._enumSettings != null)
                    {
                        try
                        {
                            VideoOptions.Register(val);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Plugin.L.LogError(ex);
                            refresh = 1E+09f;
                            break;
                        }
                    }
                }
                return;
            }

            VideoOptions.ApplyAll();
            if (VideoOptions.ReapplyFrames >= 0 && VideoOptions.ReapplyFrames-- == 0)
            {
                VideoOptions.ReapplyGameFrameSettings();
            }
        }
    }
}
