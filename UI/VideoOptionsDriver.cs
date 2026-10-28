using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
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
            // Update zoom optimization every frame
            CameraZoomPatches.UpdateZoomOptimization();

            // Periodic engine maintenance every 1 second:
            // Ensures upload buffer, SRP batcher, and renderScale (native 1.0 on Med/High) remain locked
            tweakTimer -= Time.unscaledDeltaTime;
            if (tweakTimer <= 0f)
            {
                tweakTimer = 1.0f;
                StutterAndGCPatches.ApplyEngineTweaks();
                CullingAndCPUPatches.EnsureSRPBatcher();
                VideoOptions.EnforceRenderScale(QualitySettings.GetQualityLevel());
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
