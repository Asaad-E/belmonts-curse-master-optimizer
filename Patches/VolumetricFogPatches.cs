using System;
using HarmonyLib;
using UnityEngine;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class VolumetricFogPatches
    {
        // 0 = Full (High Quality / Vanilla)
        // 1 = Optimized (Balanced - 16 samples, 0.5x RT resolution)
        // 2 = Disabled (Off - zero raymarching passes, maximum battery saving)
        public static int VolumetricFogMode = 1;

        public static void ApplyToAllActiveLightShafts()
        {
            try
            {
                var controllers = UnityEngine.Object.FindObjectsOfType<LightShaftController>();
                if (controllers == null || controllers.Length == 0) return;

                foreach (var c in controllers)
                {
                    if (c == null) continue;

                    try
                    {
                        if (VolumetricFogMode == 2)
                        {
                            if (c._feature != null)
                            {
                                c._feature.SetEnable(false);
                                c._feature.m_Active = false;
                            }
                        }
                        else
                        {
                            if (c._feature != null)
                            {
                                c._feature.SetEnable(true);
                                c._feature.m_Active = true;
                            }
                        }

                        c.SetParameters();
                    }
                    catch {}
                }

                Plugin.L?.LogInfo($"[VolumetricFog] Reapplied settings to {controllers.Length} active light shaft controllers (Mode: {VolumetricFogMode}).");
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "GetSampleCountByQuality")]
        [HarmonyPostfix]
        public static void Postfix_GetSampleCount(ref int __result)
        {
            try
            {
                if (VolumetricFogMode == 2)
                {
                    __result = 0;
                }
                else if (VolumetricFogMode == 1)
                {
                    __result = Math.Min(__result, 16);
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "GetResolutionMultiplierByQuality")]
        [HarmonyPostfix]
        public static void Postfix_GetResolutionMultiplier(ref float __result)
        {
            try
            {
                if (VolumetricFogMode == 2)
                {
                    __result = 0.25f;
                }
                else if (VolumetricFogMode == 1)
                {
                    __result = Math.Min(__result, 0.5f);
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "get_LightShaftEnabled")]
        [HarmonyPostfix]
        public static void Postfix_GetLightShaftEnabled(ref bool __result)
        {
            try
            {
                if (VolumetricFogMode == 2)
                {
                    __result = false;
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "get_enable")]
        [HarmonyPostfix]
        public static void Postfix_GetEnable(PostProcessController __instance, ref bool __result)
        {
            try
            {
                if (__instance != null && __instance.TryCast<LightShaftController>() != null && VolumetricFogMode == 2)
                {
                    __result = false;
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "SetParameters")]
        [HarmonyPrefix]
        public static void Prefix_SetParameters(LightShaftController __instance)
        {
            try
            {
                if (__instance == null) return;

                if (VolumetricFogMode == 2)
                {
                    if (__instance._feature != null)
                    {
                        __instance._feature.SetEnable(false);
                        __instance._feature.m_Active = false;
                    }
                }
                else
                {
                    if (__instance._feature != null)
                    {
                        __instance._feature.SetEnable(true);
                        __instance._feature.m_Active = true;
                    }
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(LightShaftController), "SetParameters")]
        [HarmonyPostfix]
        public static void Postfix_SetParameters(LightShaftController __instance)
        {
            try
            {
                if (__instance == null) return;

                if (VolumetricFogMode == 1 && __instance.material != null)
                {
                    // Clamp sample count uniform in material to avoid excessive raymarching loops
                    int samples = Math.Min(16, __instance.GetSampleCountByQuality());
                    __instance.material.SetFloat(__instance._NumberOfSamples, samples);
                }
                else if (VolumetricFogMode == 2 && __instance.material != null)
                {
                    __instance.material.SetFloat(__instance._NumberOfSamples, 0);
                }
            }
            catch {}
        }
    }
}
