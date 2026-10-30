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
        public static bool IsMapOpen = false;

        private static UIMapManager _activeMapManager = null;
        private static UIMapLevelManager _activeLevelManager = null;
        private static GameMenuManager _activeGameMenuManager = null;

        public static bool IsGameMenuOpen
        {
            get
            {
                try
                {
                    if ((UnityEngine.Object)(object)_activeGameMenuManager != (UnityEngine.Object)null)
                    {
                        return _activeGameMenuManager.IsShowing();
                    }
                }
                catch {}
                return false;
            }
        }

        // Failsafe watchdog: Ensures Camera.main is NEVER left at cullingMask == 0
        public static void FailsafeCameraWatchdog()
        {
            try
            {
                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask == 0)
                {
                    cam.cullingMask = -1 & ~(1 << 5);
                    MapPerf.Note("watchdog re-enabled world camera (game had set cullingMask 0)");
                }
            }
            catch {}
        }

        public static void RestoreWorldCamera()
        {
            try
            {
                Camera cam = Camera.main;
                if ((UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.cullingMask == 0)
                {
                    cam.cullingMask = -1 & ~(1 << 5);
                }
            }
            catch {}
        }

        private static void OptimizeMapCamera(Camera cam)
        {
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null) return;
            try
            {
                cam.allowMSAA = false;
                cam.allowHDR = false;
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

        [HarmonyPatch(typeof(UIMapManager), "OpenMap")]
        [HarmonyPostfix]
        public static void AfterOpenMap(UIMapManager __instance)
        {
            IsMapOpen = true;
            MapPerf.Begin("open map");
            try
            {
                var lm = (UnityEngine.Object)(object)__instance != (UnityEngine.Object)null ? __instance.levelManager : null;
                if ((UnityEngine.Object)(object)lm != (UnityEngine.Object)null)
                {
                    MapPerf.Note($"state: gamePreloaded={lm.HOTFIX_Preloaded()} cached={(lm._preLoadedMaps != null ? lm._preLoadedMaps.Count : -1)} delayRefresh={lm.delayLevelMapRefreshOnOpen}");
                }
            }
            catch (Exception ex)
            {
                MapPerf.Note("state error: " + ex.Message);
            }
            if (!FastMapEnabled) return;

            try
            {
                _activeMapManager = __instance;
                if ((UnityEngine.Object)(object)__instance != (UnityEngine.Object)null &&
                    (UnityEngine.Object)(object)__instance.levelManager != (UnityEngine.Object)null)
                {
                    _activeLevelManager = __instance.levelManager;
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Map] AfterOpenMap error: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UIMapManager), "OnMapClosed")]
        [HarmonyPostfix]
        public static void AfterOnMapClosed()
        {
            IsMapOpen = false;
            MapPerf.Begin("close map");
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnMenuOpen")]
        [HarmonyPostfix]
        public static void AfterGameMenuOpen(GameMenuManager __instance)
        {
            _activeGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnMap")]
        [HarmonyPostfix]
        public static void AfterOpenOnMap(GameMenuManager __instance)
        {
            MapPerf.Begin("menu opened on map");
            IsMapOpen = true;
            _activeGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnMenuClose")]
        [HarmonyPostfix]
        public static void AfterGameMenuClose()
        {
            MapPerf.Note("menu closed");
            IsMapOpen = false;
            _activeGameMenuManager = null;
        }

        [HarmonyPatch(typeof(GameMenuManager), "Close")]
        [HarmonyPostfix]
        public static void AfterGameMenuCloseDirect()
        {
            IsMapOpen = false;
            _activeGameMenuManager = null;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterGameMenuDestroy()
        {
            IsMapOpen = false;
            _activeGameMenuManager = null;
        }

        [HarmonyPatch(typeof(UIMapLevelTransition), "PlayTransition")]
        [HarmonyPrefix]
        public static void BeforePlayTransition(UIMapLevelTransition __instance)
        {
            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return;
            try
            {
                // Snappy 0.20s transition: eliminates the 1.5 - 2.0s sluggish dual-camera rendering overlap!
                __instance.transitionInDuration = 0.20f;
                __instance.transitionOutDuration = 0.15f;
            }
            catch {}
        }

        [HarmonyPatch(typeof(UIMapWorldManager), "OpenWorldMap", new Type[0])]
        [HarmonyPostfix]
        public static void AfterOpenWorldMap(UIMapWorldManager __instance)
        {
            try
            {
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

        [HarmonyPatch(typeof(UIMapLevelManager), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterMapLevelDestroy()
        {
            _activeLevelManager = null;
            _activeMapManager = null;
        }

        [HarmonyPatch(typeof(RoomManager.RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterRoomEnter()
        {
            MapPerf.Begin("room entered (gameplay baseline)");
            RestoreWorldCamera();
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

                if ((UnityEngine.Object)(object)__instance._currentMapObject != (UnityEngine.Object)null)
                {
                    LevelID targetLevel = __instance._currentOpenMapSceneOperationLevel;
                    if ((UnityEngine.Object)(object)targetLevel == (UnityEngine.Object)null)
                    {
                        targetLevel = __instance._currentlyOpenedLevel;
                    }
                    if ((UnityEngine.Object)(object)targetLevel != (UnityEngine.Object)null && !__instance._preLoadedMaps.ContainsKey(targetLevel))
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
            IsMapOpen = true;
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

        [HarmonyPatch(typeof(UIMapLevelManager), "GetLevelMapOpeningType")]
        [HarmonyPostfix]
        public static void AfterGetLevelMapOpeningType(UIMapLevelManager.LevelMapSceneOpeningType __result)
        {
            MapPerf.Note("opening type " + __result);
        }

        // --- Measurement only: what the game does while the map opens and closes ---

        [HarmonyPatch(typeof(UIMapLevelManager), "LoadMapScene")]
        [HarmonyPrefix]
        public static void NoteLoadMapScene() => MapPerf.Note("LoadMapScene (map scene loaded from disk)");

        [HarmonyPatch(typeof(UIMapLevelManager), "OnLoadLevelMapCompleted")]
        [HarmonyPrefix]
        public static void NoteLoadCompleted() => MapPerf.Note("map scene load completed");

        [HarmonyPatch(typeof(UIMapLevelManager), "ActivatePreLoadedMap")]
        [HarmonyPrefix]
        public static void NoteActivatePreLoaded() => MapPerf.Note("ActivatePreLoadedMap");

        [HarmonyPatch(typeof(UIMapLevelManager), "OnOpenLevelMapCompleted")]
        [HarmonyPrefix]
        public static void NoteOpenCompleted() => MapPerf.Note("open completed");

        [HarmonyPatch(typeof(UIMapLevelManager), "UnloadCurrentMapScene")]
        [HarmonyPrefix]
        public static void NoteUnloadScene() => MapPerf.Note("UnloadCurrentMapScene");

        [HarmonyPatch(typeof(UIMapLevelManager), "DeactivateCurrentPreLoadedMap")]
        [HarmonyPrefix]
        public static void NoteDeactivatePreLoaded() => MapPerf.Note("DeactivateCurrentPreLoadedMap");

        [HarmonyPatch(typeof(UIMapLevelManager), "OnCloseLevelMapCompleted")]
        [HarmonyPrefix]
        public static void NoteCloseCompleted() => MapPerf.Note("close completed");

        [HarmonyPatch(typeof(UIMapLevelManager), "CloseCurrentMapInternal")]
        [HarmonyPrefix]
        public static void BeforeCloseCurrentMapInternal(UIMapLevelManager __instance)
        {
            IsMapOpen = false;
            if (!FastMapEnabled || (UnityEngine.Object)(object)__instance == (UnityEngine.Object)null) return;

            try
            {
                _activeLevelManager = __instance;
                if ((UnityEngine.Object)(object)__instance._currentMapObject != (UnityEngine.Object)null)
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
            try
            {
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
                        Plugin.L.LogInfo($"[Map] Purged and unloaded stale _preLoadedMaps on level change (gamePreloaded={lvlMgr.HOTFIX_Preloaded()}).");
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
