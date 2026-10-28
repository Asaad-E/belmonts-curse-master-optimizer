using System;
using System.Collections.Generic;
using Doozy.Runtime.UIManager.Containers;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
        private static bool _hasSavedWorldCullingMask = false;
        private static int _defaultWorldCullingMask = -1;

        private static UIMapManager _activeMapManager = null;
        private static UIMapLevelManager _activeLevelManager = null;
        private static GameMenuManager _activeGameMenuManager = null;

        public static bool IsMapActive()
        {
            try
            {
                // 1. Check UIMapManager and its child containers
                var mapMgr = _activeMapManager ?? UIMapManager.Get();
                if ((UnityEngine.Object)(object)mapMgr != (UnityEngine.Object)null)
                {
                    if ((UnityEngine.Object)(object)mapMgr.mapContainer != (UnityEngine.Object)null &&
                        (mapMgr.mapContainer.isVisible || mapMgr.mapContainer.isShowing))
                    {
                        return true;
                    }

                    var lvlMgr = mapMgr.levelManager ?? _activeLevelManager;
                    if ((UnityEngine.Object)(object)lvlMgr != (UnityEngine.Object)null)
                    {
                        if ((UnityEngine.Object)(object)lvlMgr.levelMapContainer != (UnityEngine.Object)null &&
                            (lvlMgr.levelMapContainer.isVisible || lvlMgr.levelMapContainer.isShowing))
                        {
                            return true;
                        }
                    }

                    var wrldMgr = mapMgr.worldManager;
                    if ((UnityEngine.Object)(object)wrldMgr != (UnityEngine.Object)null)
                    {
                        if ((UnityEngine.Object)(object)wrldMgr.worldMapContainer != (UnityEngine.Object)null &&
                            (wrldMgr.worldMapContainer.isVisible || wrldMgr.worldMapContainer.isShowing))
                        {
                            return true;
                        }
                    }
                }

                // 2. Check GameMenuManager Map Tab
                if ((UnityEngine.Object)(object)_activeGameMenuManager != (UnityEngine.Object)null && _activeGameMenuManager.IsShowing())
                {
                    var mapTab = _activeGameMenuManager.mapTab;
                    if ((UnityEngine.Object)(object)mapTab != (UnityEngine.Object)null &&
                        (UnityEngine.Object)(object)mapTab.tabContainer != (UnityEngine.Object)null &&
                        (mapTab.tabContainer.isVisible || mapTab.tabContainer.isShowing))
                    {
                        return true;
                    }
                }
            }
            catch {}
            return false;
        }

        public static void FreezeWorldCamera()
        {
            if (!FastMapEnabled || IsMapOpen) return;
            try
            {
                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask != 0)
                {
                    _savedCamera = cam;
                    _savedWorldCullingMask = cam.cullingMask;
                    _hasSavedWorldCullingMask = true;
                    _defaultWorldCullingMask = cam.cullingMask;

                    // Strictly cull 3D draw calls. NEVER touch clearFlags or backgroundColor!
                    cam.cullingMask = 0;
                    IsMapOpen = true;
                    Plugin.L.LogDebug($"[Map] FreezeWorldCamera: 3D camera frozen (saved mask: {_savedWorldCullingMask})");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] FreezeWorldCamera exception: " + ex.Message);
            }
        }

        public static void RestoreWorldCamera()
        {
            try
            {
                int maskToRestore = (_hasSavedWorldCullingMask && _savedWorldCullingMask != 0) 
                    ? _savedWorldCullingMask 
                    : (_defaultWorldCullingMask > 0 ? _defaultWorldCullingMask : (-1 & ~(1 << 5)));

                Camera saved = _savedCamera;
                if ((UnityEngine.Object)(object)saved != (UnityEngine.Object)null)
                {
                    saved.cullingMask = maskToRestore;
                    Plugin.L.LogDebug($"[Map] Restored saved camera cullingMask to {maskToRestore}");
                }

                // UNCONDITIONAL FAILSAFE: Always inspect active Camera.main independently
                Camera main = Camera.main;
                if ((UnityEngine.Object)(object)main != (UnityEngine.Object)null && main.cullingMask == 0)
                {
                    main.cullingMask = maskToRestore;
                    Plugin.L.LogDebug($"[Map] Restored Camera.main cullingMask from stranded 0 to {maskToRestore}");
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
                _hasSavedWorldCullingMask = false;
            }
        }

        private static void OptimizeMapCamera(Camera cam)
        {
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null) return;
            try
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if ((UnityEngine.Object)(object)data != (UnityEngine.Object)null)
                {
                    data.renderShadows = false;
                    data.renderPostProcessing = false;
                    data.requiresDepthTexture = false;
                    data.requiresColorTexture = false;
                }
            }
            catch {}
        }

        // Failsafe watchdog called from VideoOptionsDriver.Update() every frame
        public static void FailsafeCameraWatchdog()
        {
            try
            {
                // Architectural Rule: A watchdog must NEVER freeze the camera.
                // Its ONLY responsibility is rescuing Camera.main if stranded with cullingMask == 0 while map is closed.
                if (!IsMapActive())
                {
                    Camera cam = Camera.main;
                    if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask == 0)
                    {
                        Plugin.L.LogWarning("[Map] Watchdog detected stranded cullingMask == 0 while map inactive. Rescuing world camera!");
                        RestoreWorldCamera();
                    }
                    else if (IsMapOpen)
                    {
                        RestoreWorldCamera();
                    }
                }
            }
            catch {}
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

                FreezeWorldCamera();
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

        [HarmonyPatch(typeof(GameMenuManager), "OnMenuOpen")]
        [HarmonyPostfix]
        public static void AfterGameMenuOpen(GameMenuManager __instance)
        {
            _activeGameMenuManager = __instance;
            // Record default camera cullingMask if valid
            Camera cam = Camera.main;
            if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask != 0)
            {
                _defaultWorldCullingMask = cam.cullingMask;
            }
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnMap")]
        [HarmonyPostfix]
        public static void AfterOpenOnMap(GameMenuManager __instance)
        {
            _activeGameMenuManager = __instance;
            FreezeWorldCamera();
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnMenuClose")]
        [HarmonyPostfix]
        public static void AfterGameMenuClose()
        {
            _activeGameMenuManager = null;
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(GameMenuManager), "Close")]
        [HarmonyPostfix]
        public static void AfterGameMenuCloseDirect()
        {
            _activeGameMenuManager = null;
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(UIMapWorldManager), "OpenWorldMap", new Type[0])]
        [HarmonyPostfix]
        public static void AfterOpenWorldMap(UIMapWorldManager __instance)
        {
            try
            {
                FreezeWorldCamera();
                // Disable LevelMapCamera to eliminate redundant draw calls while in Area/World view
                if (_activeLevelManager != null && _activeLevelManager._currentMapObject != null && _activeLevelManager._currentMapObject.mapCamera != null)
                {
                    var cam = _activeLevelManager._currentMapObject.mapCamera.mapCamera;
                    if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null)
                    {
                        cam.enabled = false;
                    }
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapWorldManager), "CloseWorldMap")]
        [HarmonyPostfix]
        public static void AfterCloseWorldMap()
        {
            try
            {
                // Re-enable LevelMapCamera when returning to sub-area view
                if (_activeLevelManager != null && _activeLevelManager._currentMapObject != null && _activeLevelManager._currentMapObject.mapCamera != null)
                {
                    var cam = _activeLevelManager._currentMapObject.mapCamera.mapCamera;
                    if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null)
                    {
                        cam.enabled = true;
                    }
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapWorldManager), "TryCloseWorldMap")]
        [HarmonyPostfix]
        public static void AfterTryCloseWorldMap()
        {
            if (!IsMapActive())
            {
                RestoreWorldCamera();
            }
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "ApplyRenderTexture")]
        [HarmonyPostfix]
        public static void AfterApplyLevelRenderTexture(UIMapLevelManager __instance, LevelMap levelMap)
        {
            try
            {
                if ((UnityEngine.Object)(object)levelMap != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)levelMap.mapCamera != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)levelMap.mapCamera.mapCamera != (UnityEngine.Object)null)
                {
                    OptimizeMapCamera(levelMap.mapCamera.mapCamera);
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapWorldManager), "ApplyRenderTexture")]
        [HarmonyPostfix]
        public static void AfterApplyWorldRenderTexture(UIMapWorldManager __instance, WorldMap worldMap)
        {
            try
            {
                if ((UnityEngine.Object)(object)worldMap != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)worldMap.mapCamera != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)worldMap.mapCamera.mapCamera != (UnityEngine.Object)null)
                {
                    OptimizeMapCamera(worldMap.mapCamera.mapCamera);
                }
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnDisable")]
        [HarmonyPostfix]
        public static void AfterMapLevelDisable()
        {
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterMapLevelDestroy()
        {
            RestoreWorldCamera();
            _activeLevelManager = null;
            _activeMapManager = null;
        }

        [HarmonyPatch(typeof(RoomManager.RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterRoomEnter()
        {
            RestoreWorldCamera();
            Camera cam = Camera.main;
            if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask != 0)
            {
                _defaultWorldCullingMask = cam.cullingMask;
            }
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

        [HarmonyPatch(typeof(UIMapLevelManager), "CloseLevelMap")]
        [HarmonyPostfix]
        public static void AfterCloseLevelMap()
        {
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "TryCloseLevelMap")]
        [HarmonyPostfix]
        public static void AfterTryCloseLevelMap()
        {
            RestoreWorldCamera();
        }

        [HarmonyPatch(typeof(UIMapLevelManager), "OnCloseLevelMapCompleted")]
        [HarmonyPostfix]
        public static void AfterCloseLevelMapCompleted()
        {
            RestoreWorldCamera();
        }

        // Purge cached Addressables map instances when changing levels to prevent unbounded memory accumulation
        [HarmonyPatch(typeof(LevelStateLogic), "Unload")]
        [HarmonyPrefix]
        public static void BeforeLevelUnload()
        {
            try
            {
                RestoreWorldCamera();
                PurgeMapCache();
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] BeforeLevelUnload exception: " + ex.Message);
            }
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
                        // Reset map pointers and opening type to clean state
                        lvlMgr._currentMapObject = null;
                        lvlMgr._currentOpenMapSceneOperationLevel = null;
                        lvlMgr._currentSceneOpeningType = UIMapLevelManager.LevelMapSceneOpeningType.LoadScene;
                        _activeLevelManager = null;
                        _activeMapManager = null;
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
