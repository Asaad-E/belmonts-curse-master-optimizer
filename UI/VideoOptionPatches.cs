using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BelmontsCurseMasterOptimizer.UI
{
    [HarmonyPatch]
    public static class VideoOptionPatches
    {
        private static readonly HashSet<int> started = new HashSet<int>();

        [HarmonyPatch(typeof(SettingsViewBase), "Start")]
        [HarmonyPrefix]
        private static void BeforeViewStart(SettingsViewBase __instance)
        {
            if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null)
            {
                started.Add(((UnityEngine.Object)__instance).GetInstanceID());
            }
            try
            {
                VideoOptions.RegisterFallback();
                VideoOptions.AddRows(__instance);
            }
            catch (Exception ex)
            {
                Plugin.L.LogError(ex);
            }
        }

        [HarmonyPatch(typeof(SettingsViewBase), "ConstructUI")]
        [HarmonyPrefix]
        private static void BeforeConstructUI(SettingsViewBase __instance)
        {
            try
            {
                VideoOptions.RegisterFallback();
                VideoOptions.AddRows(__instance);
            }
            catch (Exception ex)
            {
                Plugin.L.LogError(ex);
            }
        }

        [HarmonyPatch(typeof(SettingsViewBase), "OnEnable")]
        [HarmonyPostfix]
        private static void AfterViewEnable(SettingsViewBase __instance)
        {
            if ((UnityEngine.Object)(object)__instance == (UnityEngine.Object)null || !started.Contains(((UnityEngine.Object)__instance).GetInstanceID()))
            {
                return;
            }
            try
            {
                VideoOptions.AddRows(__instance, late: true);
            }
            catch (Exception ex)
            {
                Plugin.L.LogError(ex);
            }
        }

        // Safety net for option changes that did not raise onValueChanged: re-check when the view applies,
        // resets, discards or closes. ApplyAll only acts on values that actually changed.
        [HarmonyPatch(typeof(SettingsViewBase), "OnDisable")]
        [HarmonyPostfix]
        private static void AfterViewDisable()
        {
            SafeApplyAll();
        }

        [HarmonyPatch(typeof(SettingsViewBase), "ApplyChanges")]
        [HarmonyPostfix]
        private static void AfterViewApplyChanges()
        {
            SafeApplyAll();
        }

        [HarmonyPatch(typeof(SettingsViewBase), "ResetSettings")]
        [HarmonyPostfix]
        private static void AfterViewResetSettings()
        {
            SafeApplyAll();
        }

        [HarmonyPatch(typeof(SettingsViewBase), "DiscardChanges")]
        [HarmonyPostfix]
        private static void AfterViewDiscardChanges()
        {
            SafeApplyAll();
        }

        private static void SafeApplyAll()
        {
            try
            {
                VideoOptions.ApplyAll();
            }
            catch (Exception ex)
            {
                Plugin.L.LogError(ex);
            }
        }

        // Registration: every call the game makes into its settings hands us the live VideoSettings.
        [HarmonyPatch(typeof(VideoSettings), "OnEnable")]
        [HarmonyPostfix]
        private static void AfterVideoEnable(VideoSettings __instance)
        {
            VideoOptions.TryRegister(__instance);
        }

        [HarmonyPatch(typeof(VideoSettings), "Load")]
        [HarmonyPostfix]
        private static void AfterVideoLoad(VideoSettings __instance)
        {
            VideoOptions.TryRegister(__instance);
        }

        [HarmonyPatch(typeof(SettingsManager), "Load")]
        [HarmonyPostfix]
        private static void AfterManagerLoad(SettingsManager __instance)
        {
            if (!VideoOptions.Registered) VideoOptions.TryRegister(__instance._videoSettings);
        }

        [HarmonyPatch(typeof(VideoSettings), "IsSettingActive")]
        [HarmonyPostfix]
        private static void VideoIsActive(VideoSettings __instance, string settingPath, ref bool __result)
        {
            VideoOptions.TryRegister(__instance);
            if (VideoOptions.Find(settingPath) != null)
            {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(SettingsManager), "IsSettingActive")]
        [HarmonyPostfix]
        private static void ManagerIsActive(SettingsManager __instance, string settingPath, ref bool __result)
        {
            if (!VideoOptions.Registered) VideoOptions.TryRegister(__instance._videoSettings);
            if (VideoOptions.Find(settingPath) != null)
            {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(VideoSettings), "TryGetEnumSetting")]
        [HarmonyPostfix]
        private static void VideoTryGet(VideoSettings __instance, string path, ref EnumSetting setting, ref bool __result)
        {
            VideoOptions.TryRegister(__instance);
            VideoOption videoOption = (__result ? null : VideoOptions.Find(path));
            if (videoOption != null)
            {
                setting = videoOption.Setting;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(SettingsManager), "TryGetEnumSetting")]
        [HarmonyPostfix]
        private static void ManagerTryGet(SettingsManager __instance, string path, ref EnumSetting value, ref bool __result)
        {
            if (!VideoOptions.Registered) VideoOptions.TryRegister(__instance._videoSettings);
            VideoOption videoOption = (__result ? null : VideoOptions.Find(path));
            if (videoOption != null)
            {
                value = videoOption.Setting;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(VideoSettings), "ApplyFramerateSettings")]
        [HarmonyPostfix]
        private static void AfterApplyFramerate(VideoSettings __instance)
        {
            try
            {
                if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null && __instance._maxFPS != null)
                {
                    int currentVal = __instance._maxFPS.value;
                    var opts = __instance._maxFPS._options;
                    if (opts != null && currentVal >= 0 && currentVal < opts.Length)
                    {
                        var opt = ((Il2CppArrayBase<EnumSetting.EnumSettingOption>)(object)opts)[currentVal];
                        if (opt != null)
                        {
                            float fpsData = opt.data;
                            if (Mathf.Approximately(fpsData, 40f) || Mathf.Approximately(fpsData, 45f))
                            {
                                Application.targetFrameRate = (int)fpsData;
                                QualitySettings.vSyncCount = 0; // Disable Unity vsync so Gamescope handles 40/45 Hz frame pacing cleanly
                                Plugin.L.LogInfo($"[FPS] Applied {fpsData} FPS lock with vSyncCount = 0 for Steam Deck pacing.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[FPS] AfterApplyFramerate: " + ex.Message);
            }
        }
    }
}
