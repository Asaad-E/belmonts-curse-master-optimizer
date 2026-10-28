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
            // 1. Dynamic Zoom Optimization
            CameraZoomPatches.UpdateZoomOptimization();

            // 2. Map Screen Black-Screen Watchdog
            MapPatches.FailsafeCameraWatchdog();

            // 3. Active Incremental GC Heartbeat
            StutterAndGCPatches.IncrementalHeartbeat();

            // 4. Zero-Latency RenderScale Lock: Guarantee text sharpness every frame on Med/High
            var pipe = GraphicsSettings.currentRenderPipeline;
            if (pipe != null)
            {
                var urp = pipe.TryCast<UniversalRenderPipelineAsset>();
                if (urp != null)
                {
                    int quality = QualitySettings.GetQualityLevel();
                    float targetScale = (quality >= 1) ? 1.0f : 0.85f;
                    if (Mathf.Abs(urp.renderScale - targetScale) > 0.001f)
                    {
                        urp.renderScale = targetScale;
                        if (quality >= 1)
                        {
                            urp.upscalingFilter = UpscalingFilterSelection.Auto;
                        }
                    }
                }
            }

            // 5. Periodic engine maintenance every 1 second: upload buffer and SRP batcher
            tweakTimer -= Time.unscaledDeltaTime;
            if (tweakTimer <= 0f)
            {
                tweakTimer = 1.0f;
                StutterAndGCPatches.ApplyEngineTweaks();
                CullingAndCPUPatches.EnsureSRPBatcher();
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
