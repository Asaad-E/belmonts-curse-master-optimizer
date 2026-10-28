using System;
using System.Collections.Generic;
using Doozy.Runtime.UIManager.Containers;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private static CameraClearFlags _savedClearFlags;
        private static Color _savedBgColor;
        private static UIMapManager _activeMapManager = null;
        private static UIMapLevelManager _activeLevelManager = null;

        private static bool IsMapUIActive(UIMapManager mgr)
        {
            if ((UnityEngine.Object)(object)mgr == (UnityEngine.Object)null) return false;

            try
            {
                UIMapLevelManager lvlMgr = mgr.levelManager;
                if ((UnityEngine.Object)(object)lvlMgr != (UnityEngine.Object)null)
                {
                    if (lvlMgr.IsOpened() || lvlMgr.IsOpening()) return true;
                    if ((UnityEngine.Object)(object)lvlMgr.levelMapContainer != (UnityEngine.Object)null &&
                        (lvlMgr.levelMapContainer.isVisible || lvlMgr.levelMapContainer.isShowing))
                    {
                        return true;
                    }
                }

                if ((UnityEngine.Object)(object)mgr.mapContainer != (UnityEngine.Object)null &&
                    (mgr.mapContainer.isVisible || mgr.mapContainer.isShowing))
                {
                    return true;
                }
            }
            catch {}
            return false;
        }

        [HarmonyPatch(typeof(UIMapManager), "OpenMap")]
        [HarmonyPostfix]
        public static void AfterOpenMap(UIMapManager __instance)
        {
            if (!FastMapEnabled) return;

            try
            {
                _activeMapManager = __instance;
                if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)__instance.levelManager != (UnityEngine.Object)null)
                {
                    _activeLevelManager = __instance.levelManager;
                }

                // Guard against premature culling if OpenMap early-returned without showing map
                if (IsMapOpen || !IsMapUIActive(__instance)) return;

                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask != 0)
                {
                    _savedCamera = cam;
                    _savedWorldCullingMask = cam.cullingMask;
                    _savedClearFlags = cam.clearFlags;
                    _savedBgColor = cam.backgroundColor;

                    cam.cullingMask = 0; // Freeze 3D scene rendering behind map UI
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    IsMapOpen = true;
                    Plugin.L.LogDebug($"[Map] AfterOpenMap: 3D camera frozen (saved mask: {_savedWorldCullingMask})");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] AfterOpenMap error: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UIMapManager), "OnMapClosed")]
        [HarmonyPostfix]
        public static void AfterMapClosed(UIMapManager __instance)
        {
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnDisable")]
        [HarmonyPostfix]
        public static void AfterMapLevelDisable()
        {
            if (IsMapOpen)
            {
                RestoreWorldCamera();
            }
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterMapLevelDestroy()
        {
            if (IsMapOpen)
            {
                RestoreWorldCamera();
            }
            _activeLevelManager = null;
            _activeMapManager = null;
        }

        [HarmonyPatch(typeof(RoomManager.RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterRoomEnter()
        {
            if (IsMapOpen || ((UnityEngine.Object)(object)Camera.main != (UnityEngine.Object)null && Camera.main.cullingMask == 0))
            {
                RestoreWorldCamera();
            }
        }

        public static void RestoreWorldCamera()
        {
            try
            {
                int maskToRestore = (_savedWorldCullingMask > 0) ? _savedWorldCullingMask : (-1 & ~(1 << 5));
                Camera cam = ((UnityEngine.Object)(object)_savedCamera != (UnityEngine.Object)null) ? _savedCamera : Camera.main;

                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null)
                {
                    cam.cullingMask = maskToRestore;
                    cam.clearFlags = _savedClearFlags != 0 ? _savedClearFlags : CameraClearFlags.Skybox;
                    cam.backgroundColor = _savedBgColor;
                    Plugin.L.LogDebug($"[Map] Restored world camera cullingMask to {maskToRestore}");
                }
                else if ((UnityEngine.Object)(object)Camera.main != (UnityEngine.Object)null && Camera.main.cullingMask == 0)
                {
                    Camera.main.cullingMask = -1 & ~(1 << 5);
                    Camera.main.clearFlags = CameraClearFlags.Skybox;
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
            try
            {
                if (IsMapOpen)
                {
                    // If map is flagged open but UI containers are actually inactive (e.g. death or cutscene bypass), recover!
                    if (!IsMapUIActive(_activeMapManager))
                    {
                        Plugin.L.LogWarning("[Map] Watchdog detected inactive UI while IsMapOpen=true. Recovering camera!");
                        RestoreWorldCamera();
                    }
                }
                else
                {
                    Camera cam = Camera.main;
                    if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask == 0)
                    {
                        Plugin.L.LogWarning("[Map] Watchdog detected stranded cullingMask == 0. Recovering world camera!");
                        RestoreWorldCamera();
                    }
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnLoadLevelMapCompleted")]
        [HarmonyPostfix]
        public static void AfterLoadLevelMapCompleted(UIMapLevelManager __instance)
        {
            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return;

            try
            {
                _activeLevelManager = __instance;
                if (__instance._preLoadedMaps == null)
                {
                    __instance._preLoadedMaps = new Il2CppSystem.Collections.Generic.Dictionary<LevelID, LevelMap>();
                }

                if ((UnityEngine.Object)(object)__instance._currentMapObject != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)__instance._currentOpenMapSceneOperationLevel != (UnityEngine.Object)null)
                {
                    LevelID targetLevel = __instance._currentOpenMapSceneOperationLevel;
                    if (!__instance._preLoadedMaps.ContainsKey(targetLevel))
                    {
                        __instance._preLoadedMaps[targetLevel] = __instance._currentMapObject;
                        Plugin.L.LogInfo("[Map] Cached LevelMap in _preLoadedMaps for " + ((UnityEngine.Object)targetLevel).name);
                    }
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
                _activeLevelManager = __instance;
                if (__instance._preLoadedMaps != null && (UnityEngine.Object)(object)targetLevel != (UnityEngine.Object)null)
                {
                    if (__instance._preLoadedMaps.ContainsKey(targetLevel))
                    {
                        LevelMap map = __instance._preLoadedMaps[targetLevel];
                        if ((UnityEngine.Object)(object)map != (UnityEngine.Object)null &&
                            (UnityEngine.Object)(object)((Component)map).gameObject != (UnityEngine.Object)null)
                        {
                            __result = UIMapLevelManager.LevelMapSceneOpeningType.ActivatePreLoaded;
                            Plugin.L.LogDebug("[Map] Opening preloaded LevelMap for " + ((UnityEngine.Object)targetLevel).name);
                            return false; // Skip original load
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
                _activeLevelManager = __instance;
                if (__instance._preLoadedMaps != null &&
                    (UnityEngine.Object)(object)__instance._currentOpenMapSceneOperationLevel != (UnityEngine.Object)null &&
                    __instance._preLoadedMaps.ContainsKey(__instance._currentOpenMapSceneOperationLevel) &&
                    (UnityEngine.Object)(object)__instance._preLoadedMaps[__instance._currentOpenMapSceneOperationLevel] != (UnityEngine.Object)null)
                {
                    __instance._currentSceneOpeningType = UIMapLevelManager.LevelMapSceneOpeningType.ActivatePreLoaded;
                    Plugin.L.LogDebug("[Map] Switched close operation to ActivatePreLoaded (instant <1ms closure)");
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
                RestoreWorldCamera();

                UIMapLevelManager lvlMgr = _activeLevelManager;
                if ((UnityEngine.Object)(object)lvlMgr == (UnityEngine.Object)null)
                {
                    UIMapManager mapMgr = UIMapManager.Get();
                    if ((UnityEngine.Object)(object)mapMgr != (UnityEngine.Object)null)
                    {
                        lvlMgr = mapMgr.levelManager;
                    }
                }

                if ((UnityEngine.Object)(object)lvlMgr != (UnityEngine.Object)null)
                {
                    var preloaded = lvlMgr._preLoadedMaps;
                    if (preloaded != null && preloaded.Count > 0)
                    {
                        var enumerator = preloaded.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            LevelMap map = enumerator.Current.Value;
                            if ((UnityEngine.Object)(object)map != (UnityEngine.Object)null &&
                                (UnityEngine.Object)(object)((Component)map).gameObject != (UnityEngine.Object)null)
                            {
                                Scene mapScene = ((Component)map).gameObject.scene;
                                if (mapScene.IsValid() && mapScene.isLoaded && mapScene != SceneManager.GetActiveScene())
                                {
                                    SceneManager.UnloadSceneAsync(mapScene);
                                }
                                else
                                {
                                    UnityEngine.Object.Destroy(((Component)map).gameObject);
                                }
                            }
                        }
                        preloaded.Clear();
                        Plugin.L.LogInfo("[Map] Purged and unloaded stale _preLoadedMaps on level change.");
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
