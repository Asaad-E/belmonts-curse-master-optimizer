using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
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

        private static readonly HashSet<IntPtr> _configuredRenderers = new HashSet<IntPtr>();

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "RenderURPReflection")]
        [HarmonyPrefix]
        public static bool BeforeRenderURPReflection(PlanarReflectionRenderer __instance, ScriptableRenderContext context, Camera cam)
        {
            // If the Map is open, skip reflection passes entirely to save GPU & CPU culling
            if (MapPatches.IsMapOpen)
            {
                return false;
            }

            if (ReflectionMode == 2)
            {
                // Reflections turned Off
                return false;
            }

            if (ReflectionMode == 1)
            {
                // Frame-skip: Render every 2nd frame
                return (Time.frameCount % 2 == 0);
            }

            // Mode 0: Optimized Every Frame
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                IntPtr ptr = ((Il2CppObjectBase)__instance).Pointer;
                if (!_configuredRenderers.Contains(ptr))
                {
                    try
                    {
                        var settings = __instance._settings;
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

                            __instance.ApplySettings();
                            _configuredRenderers.Add(ptr);
                            Plugin.L.LogInfo("[WaterReflection] Applied optimized 60 FPS settings on PlanarReflectionRenderer instance.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.L.LogWarning("[WaterReflection] Error applying settings: " + ex.Message);
                    }
                }
            }

            // Render smoothly every frame
            return true;
        }

        [HarmonyPatch(typeof(PlanarReflectionRenderer), "OnEnable")]
        [HarmonyPostfix]
        public static void AfterEnable(PlanarReflectionRenderer __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                _configuredRenderers.Remove(((Il2CppObjectBase)__instance).Pointer);
            }
        }
    }
}
