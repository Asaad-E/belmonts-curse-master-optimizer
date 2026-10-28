using System;
using HarmonyLib;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class MapPatches
    {
        public static bool FastMapEnabled = true;
        public static bool IsMapOpen { get; private set; } = false;

        private static Camera _savedCamera = null;
        private static int _savedWorldCullingMask = -1;

        [HarmonyPatch(typeof(UIMapManager), "OpenMap")]
        [HarmonyPrefix]
        public static void BeforeOpenMap(UIMapManager __instance)
        {
            if (!FastMapEnabled) return;
            if (IsMapOpen) return; // Re-entrancy guard

            try
            {
                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null)
                {
                    // Guard against capturing an invalid or already-zeroed culling mask
                    int currentMask = cam.cullingMask;
                    if (currentMask == 0)
                    {
                        currentMask = -1 & ~(1 << 5); // Default: Everything except UI layer (5)
                    }

                    _savedCamera = cam;
                    _savedWorldCullingMask = currentMask;
                    cam.cullingMask = 0; // Freeze 3D scene rendering behind map UI
                    IsMapOpen = true;
                    Plugin.L.LogDebug($"[Map] World camera cullingMask set to 0 (saved: {_savedWorldCullingMask})");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] BeforeOpenMap exception: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UIMapManager), "OnMapClosed")]
        [HarmonyPostfix]
        public static void AfterMapClosed(UIMapManager __instance)
        {
            RestoreWorldCamera();
        }

        public static void RestoreWorldCamera()
        {
            if (!IsMapOpen && _savedWorldCullingMask == -1) return;

            try
            {
                int maskToRestore = (_savedWorldCullingMask > 0) ? _savedWorldCullingMask : (-1 & ~(1 << 5));

                if ((UnityEngine.Object)(object)_savedCamera != (UnityEngine.Object)null)
                {
                    _savedCamera.cullingMask = maskToRestore;
                    Plugin.L.LogDebug($"[Map] Restored saved camera cullingMask to {maskToRestore}");
                }
                else
                {
                    Camera cam = Camera.main;
                    if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null)
                    {
                        cam.cullingMask = maskToRestore;
                        Plugin.L.LogDebug($"[Map] Restored Camera.main cullingMask to {maskToRestore}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] RestoreWorldCamera exception: " + ex.Message);
            }
            finally
            {
                IsMapOpen = false;
                _savedCamera = null;
                _savedWorldCullingMask = -1;
            }
        }

        // Failsafe watchdog called from VideoOptionsDriver.Update() every frame
        public static void FailsafeCameraWatchdog()
        {
            if (!IsMapOpen)
            {
                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask == 0)
                {
                    Plugin.L.LogWarning("[Map] Watchdog detected stranded cullingMask == 0. Recovering world camera!");
                    cam.cullingMask = -1 & ~(1 << 5);
                }
            }
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnLoadLevelMapCompleted")]
        [HarmonyPostfix]
        public static void AfterLoadLevelMapCompleted(UIMapLevelManager __instance)
        {
            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return;

            try
            {
                if (__instance._preLoadedMaps == null)
                {
                    __instance._preLoadedMaps = new Il2CppSystem.Collections.Generic.Dictionary<LevelID, LevelMap>();
                }

                if ((UnityEngine.Object)(object)__instance._currentMapObject != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)__instance._currentOpenMapSceneOperationLevel != (UnityEngine.Object)null)
                {
                    __instance._preLoadedMaps[__instance._currentOpenMapSceneOperationLevel] = __instance._currentMapObject;
                    Plugin.L.LogInfo("[Map] Cached LevelMap in _preLoadedMaps for " + ((UnityEngine.Object)__instance._currentOpenMapSceneOperationLevel).name);
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] AfterLoadLevelMapCompleted: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "GetLevelMapOpeningType")]
        [HarmonyPrefix]
        public static bool PrefixGetLevelMapOpeningType(UIMapLevelManager __instance, LevelID targetLevel, ref UIMapLevelManager.LevelMapSceneOpeningType __result)
        {
            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return true;

            try
            {
                if (__instance._preLoadedMaps != null && (UnityEngine.Object)(object)targetLevel != (UnityEngine.Object)null)
                {
                    if (__instance._preLoadedMaps.ContainsKey(targetLevel))
                    {
                        LevelMap map = __instance._preLoadedMaps[targetLevel];
                        if ((UnityEngine.Object)(object)map != (UnityEngine.Object)null)
                        {
                            __result = UIMapLevelManager.LevelMapSceneOpeningType.ActivatePreLoaded;
                            Plugin.L.LogDebug("[Map] Opening preloaded LevelMap for " + ((UnityEngine.Object)targetLevel).name);
                            return false; // Skip original
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] GetLevelMapOpeningType: " + ex.Message);
            }
            return true;
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "CloseCurrentMapInternal")]
        [HarmonyPrefix]
        public static void BeforeCloseCurrentMapInternal(UIMapLevelManager __instance)
        {
            RestoreWorldCamera();

            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return;

            try
            {
                // If map is cached, switch to ActivatePreLoaded so the engine calls DeactivateCurrentPreLoadedMap()
                // instead of UnloadCurrentMapScene() (which triggers the 2-second freeze)
                if (__instance._preLoadedMaps != null &&
                    (UnityEngine.Object)(object)__instance._currentOpenMapSceneOperationLevel != (UnityEngine.Object)null &&
                    __instance._preLoadedMaps.ContainsKey(__instance._currentOpenMapSceneOperationLevel) &&
                    (UnityEngine.Object)(object)__instance._preLoadedMaps[__instance._currentOpenMapSceneOperationLevel] != (UnityEngine.Object)null)
                {
                    __instance._currentSceneOpeningType = UIMapLevelManager.LevelMapSceneOpeningType.ActivatePreLoaded;
                    Plugin.L.LogDebug("[Map] Switched close operation to ActivatePreLoaded (skipping UnloadSceneAsync)");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] BeforeCloseCurrentMapInternal: " + ex.Message);
            }
        }

        // Purge cached Addressables map instances when changing levels to prevent unbounded memory accumulation
        [HarmonyPatch(typeof(LevelStateLogic), "Unload")]
        [HarmonyPrefix]
        public static void BeforeLevelUnload()
        {
            RestoreWorldCamera();
            PurgeMapCache();
        }

        public static void PurgeMapCache()
        {
            try
            {
                UIMapManager mapMgr = UIMapManager.Get();
                if ((UnityEngine.Object)(object)mapMgr != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)mapMgr.levelManager != (UnityEngine.Object)null)
                {
                    var preloaded = mapMgr.levelManager._preLoadedMaps;
                    if (preloaded != null)
                    {
                        preloaded.Clear();
                        Plugin.L.LogInfo("[Map] Purged stale _preLoadedMaps on level change to prevent memory leaks.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] PurgeMapCache exception: " + ex.Message);
            }
        }
    }
}
