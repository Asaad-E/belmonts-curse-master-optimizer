using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

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

        [HarmonyPatch(typeof(VideoSettings), "IsSettingActive")]
        [HarmonyPostfix]
        private static void VideoIsActive(string settingPath, ref bool __result)
        {
            if (VideoOptions.Find(settingPath) != null)
            {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(SettingsManager), "IsSettingActive")]
        [HarmonyPostfix]
        private static void ManagerIsActive(string settingPath, ref bool __result)
        {
            if (VideoOptions.Find(settingPath) != null)
            {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(VideoSettings), "TryGetEnumSetting")]
        [HarmonyPostfix]
        private static void VideoTryGet(string path, ref EnumSetting setting, ref bool __result)
        {
            VideoOption videoOption = (__result ? null : VideoOptions.Find(path));
            if (videoOption != null)
            {
                setting = videoOption.Setting;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(SettingsManager), "TryGetEnumSetting")]
        [HarmonyPostfix]
        private static void ManagerTryGet(string path, ref EnumSetting value, ref bool __result)
        {
            VideoOption videoOption = (__result ? null : VideoOptions.Find(path));
            if (videoOption != null)
            {
                value = videoOption.Setting;
                __result = true;
            }
        }
    }
}
