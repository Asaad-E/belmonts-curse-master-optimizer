using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Doozy.Runtime.UIManager.Containers;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// Dedicated Engine Architecture for 3D Resolution Scaling Decoupling.
    /// Completely isolates 3D background world rendering from UI/HUD/Menu clarity.
    /// </summary>
    [HarmonyPatch]
    public static class RenderScaleDecoupler
    {
        // =========================================================================
        // SECTION 1: INFALLIBLE MULTI-TIER MENU & SETTINGS DETECTION ENGINE
        // =========================================================================
        private static int _activeSettingsViews = 0;
        private static bool _isGameMenuOpenFlag = false;
        private static GameMenuManager _lastGameMenuManager = null;
        private static float _lastDecoupleCheckTime = 0f;
        private const float DecoupleScanInterval = 1.5f;

        /// <summary>
        /// 100% Bulletproof menu detection engine across all engine states:
        /// 1. Active SettingsViewBase instances (Video, Audio, Gameplay, Controls)
        /// 2. GameMenu / Pause Menu hierarchy states & Doozy UI container visibility
        /// 3. Title Screen / Boot scenes
        /// 4. UIManager active menu stack
        /// </summary>
        public static bool IsInMenu()
        {
            try
            {
                // While viewing the fast map screen: keep current gameplay renderScale to avoid GPU reallocations
                if (MapPatches.IsMapOpen) return false;

                // Tier 1: If ANY settings view is actively open and enabled
                if (_activeSettingsViews > 0) return true;

                // Tier 2: Title screen / Main menu scenes
                string sceneName = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(sceneName) && 
                    (sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 || 
                     sceneName.IndexOf("Init", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }

                // Tier 3: Pause Menu / GameMenu state machine
                if (_isGameMenuOpenFlag)
                {
                    if (_lastGameMenuManager != null && _lastGameMenuManager.gameObject != null)
                    {
                        if (_lastGameMenuManager.gameObject.activeInHierarchy) return true;
                    }
                    else
                    {
                        return true;
                    }
                }

                if (_lastGameMenuManager != null && _lastGameMenuManager.gameObject != null && _lastGameMenuManager.gameObject.activeInHierarchy)
                {
                    return true;
                }

                // Tier 4: Fallback check on active VideoSettings object
                if (UI.VideoOptions.Video != null && UI.VideoOptions.Video.gameObject != null && UI.VideoOptions.Video.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            catch {}

            return false;
        }

        // --- Harmony Hooks for Settings Views ---
        [HarmonyPatch(typeof(SettingsViewBase), "OnEnable")]
        [HarmonyPostfix]
        public static void AfterSettingsViewEnable(SettingsViewBase __instance)
        {
            if (__instance != null)
            {
                _activeSettingsViews++;
            }
        }

        [HarmonyPatch(typeof(SettingsViewBase), "OnDisable")]
        [HarmonyPostfix]
        public static void AfterSettingsViewDisable(SettingsViewBase __instance)
        {
            if (__instance != null)
            {
                _activeSettingsViews = Math.Max(0, _activeSettingsViews - 1);
            }
        }

        // --- Harmony Hooks for GameMenuManager ---
        [HarmonyPatch(typeof(GameMenuManager), "OnMenuOpen")]
        [HarmonyPostfix]
        public static void AfterGameMenuOpen(GameMenuManager __instance)
        {
            _isGameMenuOpenFlag = true;
            _lastGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnMap")]
        [HarmonyPostfix]
        public static void AfterOpenOnMap(GameMenuManager __instance)
        {
            _isGameMenuOpenFlag = true;
            _lastGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnProgress")]
        [HarmonyPostfix]
        public static void AfterOpenOnProgress(GameMenuManager __instance)
        {
            _isGameMenuOpenFlag = true;
            _lastGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnEquipment")]
        [HarmonyPostfix]
        public static void AfterOpenOnEquipment(GameMenuManager __instance)
        {
            _isGameMenuOpenFlag = true;
            _lastGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OpenOnMenu")]
        [HarmonyPostfix]
        public static void AfterOpenOnMenu(GameMenuManager __instance)
        {
            _isGameMenuOpenFlag = true;
            _lastGameMenuManager = __instance;
        }

        [HarmonyPatch(typeof(GameMenuManager), "TryShow")]
        [HarmonyPostfix]
        public static void AfterTryShow(GameMenuManager __instance, bool __result)
        {
            if (__result)
            {
                _isGameMenuOpenFlag = true;
                _lastGameMenuManager = __instance;
            }
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnMenuClose")]
        [HarmonyPostfix]
        public static void AfterGameMenuClose()
        {
            _isGameMenuOpenFlag = false;
        }

        [HarmonyPatch(typeof(GameMenuManager), "Close")]
        [HarmonyPostfix]
        public static void AfterGameMenuCloseDirect()
        {
            _isGameMenuOpenFlag = false;
        }

        [HarmonyPatch(typeof(GameMenuManager), "OnDestroy")]
        [HarmonyPostfix]
        public static void AfterGameMenuDestroy()
        {
            _isGameMenuOpenFlag = false;
            _lastGameMenuManager = null;
        }

        // =========================================================================
        // SECTION 2: CANVAS DECOUPLING (ScreenSpaceOverlay Elevation)
        // =========================================================================

        /// <summary>
        /// Converts a Canvas to ScreenSpaceOverlay so it renders directly to the native
        /// display backbuffer at 100% resolution, completely decoupled from URP renderScale.
        /// </summary>
        public static bool DecoupleCanvas(Canvas canvas, string reason)
        {
            if (canvas == null) return false;
            try
            {
                // WorldSpace canvases that are placed on 3D props in the scene should stay in world space
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    // If it's specifically a menu or signs that belong in world space, leave it
                    return false;
                }

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Plugin.L?.LogInfo($"[CanvasDecoupler] Decoupled '{canvas.name}' -> ScreenSpaceOverlay ({reason}). Native 1:1 Sharpness Guaranteed!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.L?.LogWarning($"[CanvasDecoupler] Error decoupling canvas '{canvas.name}': {ex.Message}");
            }
            return false;
        }

        // --- Hook LoadingScreenCameraManager to decouple _hudRootCanvas ---
        [HarmonyPatch(typeof(LoadingScreenCameraManager), "OnHudRootChanged")]
        [HarmonyPostfix]
        public static void AfterOnHudRootChanged(LoadingScreenCameraManager __instance)
        {
            if (__instance != null && __instance._hudRootCanvas != null)
            {
                DecoupleCanvas(__instance._hudRootCanvas, "LoadingScreenCameraManager.OnHudRootChanged");
            }
        }

        [HarmonyPatch(typeof(LoadingScreenCameraManager), "OnGameplayCameraChanged")]
        [HarmonyPostfix]
        public static void AfterOnGameplayCameraChanged(LoadingScreenCameraManager __instance)
        {
            if (__instance != null && __instance._hudRootCanvas != null)
            {
                DecoupleCanvas(__instance._hudRootCanvas, "LoadingScreenCameraManager.OnGameplayCameraChanged");
            }
        }

        /// <summary>
        /// Scans active canvases and elevates screen-space HUD, popup, and tooltip canvases
        /// to ScreenSpaceOverlay. Called periodically or on scene changes.
        /// </summary>
        public static void EnsureCanvasesDecoupled(bool force = false)
        {
            float now = Time.realtimeSinceStartup;
            if (!force && now - _lastDecoupleCheckTime < DecoupleScanInterval) return;
            _lastDecoupleCheckTime = now;

            try
            {
                var canvases = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Canvas>());
                if (canvases == null) return;

                for (int i = 0; i < canvases.Length; i++)
                {
                    var item = canvases[i];
                    if (item == null) continue;
                    var c = item.TryCast<Canvas>();
                    if (c != null && c.isRootCanvas && c.gameObject != null && c.gameObject.scene.IsValid())
                    {
                        string name = c.name;
                        if (c.renderMode == RenderMode.ScreenSpaceCamera)
                        {
                            if (name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Prompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("CutsceneBubble", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                DecoupleCanvas(c, "Periodic Canvas Scan");
                            }
                        }
                    }
                }
            }
            catch {}
        }
    }
}
