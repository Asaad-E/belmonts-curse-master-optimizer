using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class WaterReflectionPatches
    {
        // 0 = Optimized (Every Frame, 60 FPS)
        // 1 = Frame-Skip (Every 2nd frame)
        // 2 = Off
        public static int ReflectionMode = 0;

        private static readonly HashSet<int> _configuredInstanceIds = new HashSet<int>();

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "OnEnable")]
        [HarmonyPostfix]
        public static void AfterEnable(PlanarReflectionRenderer __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                ConfigureRendererInstance(__instance);
            }
        }

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "OnDisable")]
        [HarmonyPostfix]
        public static void AfterDisable(PlanarReflectionRenderer __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                _configuredInstanceIds.Remove(((UnityEngine.Object)__instance).GetInstanceID());
            }
        }

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterDestroy(PlanarReflectionRenderer __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                _configuredInstanceIds.Remove(((UnityEngine.Object)__instance).GetInstanceID());
            }
        }

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "OnQualityChanged")]
        [HarmonyPostfix]
        public static void AfterQualityChanged(PlanarReflectionRenderer __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                _configuredInstanceIds.Remove(((UnityEngine.Object)__instance).GetInstanceID());
                if (ReflectionMode != 2)
                {
                    ConfigureRendererInstance(__instance);
                }
            }
        }

        public static void ReapplyAllWaterSettings()
        {
            try
            {
                _configuredInstanceIds.Clear();
                if (ReflectionMode == 2) return;

                foreach (PlanarReflectionRenderer renderer in UnityEngine.Object.FindObjectsOfType<PlanarReflectionRenderer>())
                {
                    if ((UnityEngine.Object)(object)renderer != (UnityEngine.Object)null)
                    {
                        ConfigureRendererInstance(renderer);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[WaterReflection] ReapplyAllWaterSettings error: " + ex.Message);
            }
        }

        private static void ConfigureRendererInstance(PlanarReflectionRenderer renderer)
        {
            if ((UnityEngine.Object)(object)renderer == (UnityEngine.Object)null) return;

            int id = ((UnityEngine.Object)renderer).GetInstanceID();
            _configuredInstanceIds.Add(id);

            try
            {
                var settings = renderer._settings;
                if (settings != null)
                {
                    // Strip shadow maps from reflection camera pass (saves hundreds of shadow draw calls)
                    settings.renderShadows = false;
                    // Strip post-processing bloom/color grading from reflection camera
                    settings.usePostFX = false;

                    // Downscale reflection texture resolution to save fillrate & vertex shader cost
                    var res = settings.resolutionByQuality;
                    if (res != null)
                    {
                        res.lowResolutionMultiplier = 0.25f;
                        res.midResolutionMultiplier = 0.35f;
                        res.highResolutionMultiplier = 0.50f;
                    }

                    // Apply safely outside the active URP render loop
                    renderer.ApplySettings();
                    Plugin.L.LogInfo($"[WaterReflection] Configured planar reflection renderer instance #{id} with 60 FPS settings.");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[WaterReflection] Error applying settings: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "RenderURPReflection")]
        [HarmonyPrefix]
        public static bool BeforeRenderURPReflection(PlanarReflectionRenderer __instance, ScriptableRenderContext context, Camera cam)
        {
            // If the Map is open, skip reflection passes entirely to save GPU & CPU culling
            if (MapPatches.IsMapOpen || ReflectionMode == 2)
            {
                return false;
            }

            // CRITICAL: Ensure renderer is configured BEFORE the frame-skip check!
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                int id = ((UnityEngine.Object)__instance).GetInstanceID();
                if (!_configuredInstanceIds.Contains(id))
                {
                    ConfigureRendererInstance(__instance);
                }
            }

            if (ReflectionMode == 1)
            {
                // Frame-skip: Render every 2nd frame
                return (Time.frameCount % 2 == 0);
            }

            // Render smoothly every frame
            return true;
        }
    }
}
