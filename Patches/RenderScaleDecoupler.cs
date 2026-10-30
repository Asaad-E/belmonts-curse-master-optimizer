using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// 3D resolution scale with native UI.
    ///
    /// Confirmed in game:
    /// - The scale only takes effect when asset.renderScale is set before the frame renders (from Update);
    ///   changing it per camera during the render has no visible effect.
    /// - HUD, prompts and the resume button are ScreenSpaceOverlay canvases and stay sharp at any scale.
    /// - The pause menu and the loading screen are drawn by their own cameras in world space; while one of
    ///   them is shown the game's own scale is used, so they stay native.
    ///
    /// Per frame: one camera check, one canvas check and a float compare; the asset is only written when the
    /// target changes.
    /// </summary>
    [HarmonyPatch]
    public static class RenderScaleDecoupler
    {
        public static readonly int[] Percents = { 100, 50, 5 }; // test values: drastic steps to confirm the scale takes effect

        internal static float UserScale = 1f;
        private static bool _active;

        private static UniversalRenderPipelineAsset _asset;
        private static IntPtr _assetPtr;
        private static bool _assetDirty = true;
        private static bool _upscalerPending = true;
        private static int _nextAssetCheck;
        private static int _scaleOffset = -1;
        private static float _gameScale = 1f; // the value the game believes the asset holds

        private static bool _filterOverridden;
        private static UpscalingFilterSelection _origFilter;
        private static bool _origFsrOverride;
        private static float _origFsrSharpness;

        private static Camera _worldCam;
        private static Canvas _menuCanvas;    // pause menu (WorldSpaceUICamera)
        private static Canvas _loadingCanvas; // loading screen (WorldSpaceCamera)
        private static bool _hudLogged;

        public static void Init()
        {
            IntPtr field = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<UniversalRenderPipelineAsset>.NativeClassPtr, "m_RenderScale");
            _scaleOffset = (int)IL2CPP.il2cpp_field_get_offset(field);
        }

        public static void SetUserPercent(int percent)
        {
            UserScale = Mathf.Clamp(percent, 5, 100) / 100f;
            bool active = UserScale < 0.995f;
            if (active != _active)
            {
                _active = active;
                if (!active)
                {
                    RestoreAsset();
                }
                else
                {
                    _assetDirty = true;
                    _upscalerPending = true;
                }
            }
            if (_active && !_assetDirty)
            {
                ApplyUpscaler();
            }
            Plugin.L?.LogInfo($"[RenderScale] 3D resolution {Mathf.RoundToInt(UserScale * 100f)}% (UI and menus stay native)");
        }

        /// <summary>Called after the mod changes the quality level, which can switch the pipeline asset.</summary>
        public static void MarkAssetDirty()
        {
            _assetDirty = true;
        }

        public static int PercentToIndex(int percent)
        {
            int idx = Array.IndexOf(Percents, percent);
            return idx < 0 ? 0 : idx;
        }

        /// <summary>Called every frame from VideoOptionsDriver.Update, before rendering.</summary>
        public static void Tick()
        {
            if (!_active)
            {
                return;
            }
            // The game swaps the pipeline asset on its own (e.g. after loading a save), so re-check it twice a second.
            if (Time.frameCount >= _nextAssetCheck)
            {
                _nextAssetCheck = Time.frameCount + 30;
                _assetDirty = true;
            }
            if (_assetDirty && !RefreshAsset())
            {
                return;
            }
            float target = IsGameplayView() ? Math.Min(UserScale, _gameScale) : _gameScale;
            if (ReadScale() != target)
            {
                WriteScale(target);
            }
        }

        private static bool IsGameplayView()
        {
            if ((UnityEngine.Object)(object)_worldCam == (UnityEngine.Object)null || !_worldCam.isActiveAndEnabled)
            {
                return false;
            }
            return !IsWorldSpace(_menuCanvas) && !IsWorldSpace(_loadingCanvas);
        }

        private static bool IsWorldSpace(Canvas canvas)
        {
            return (UnityEngine.Object)(object)canvas != (UnityEngine.Object)null && canvas.renderMode == RenderMode.WorldSpace;
        }

        private static unsafe float ReadScale()
        {
            return *(float*)((byte*)_assetPtr + _scaleOffset);
        }

        private static unsafe void WriteScale(float value)
        {
            if (_assetPtr == IntPtr.Zero)
            {
                return;
            }
            *(float*)((byte*)_assetPtr + _scaleOffset) = value;
        }

        private static bool RefreshAsset()
        {
            RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
            if ((UnityEngine.Object)(object)current == (UnityEngine.Object)null)
            {
                return false;
            }
            UniversalRenderPipelineAsset urp = current.TryCast<UniversalRenderPipelineAsset>();
            if ((UnityEngine.Object)(object)urp == (UnityEngine.Object)null)
            {
                return false;
            }
            _assetDirty = false;
            if (urp.Pointer != _assetPtr)
            {
                // Hand the previous quality level's asset back exactly as the game left it.
                RestoreAsset();
                _asset = urp;
                _assetPtr = urp.Pointer;
                _gameScale = ReadScale();
                _upscalerPending = true;
                Plugin.L?.LogInfo($"[RenderScale] Tracking URP asset '{urp.name}' (game scale {_gameScale:0.00})");
            }
            if (_upscalerPending)
            {
                _upscalerPending = false;
                ApplyUpscaler();
            }
            return true;
        }

        private static void RestoreAsset()
        {
            if (_assetPtr == IntPtr.Zero)
            {
                return;
            }
            WriteScale(_gameScale);
            if (_filterOverridden)
            {
                _asset.upscalingFilter = _origFilter;
                _asset.fsrOverrideSharpness = _origFsrOverride;
                _asset.fsrSharpness = _origFsrSharpness;
                _filterOverridden = false;
            }
        }

        private static void ApplyUpscaler()
        {
            int mode = Plugin.UpscalerConfig != null ? Plugin.UpscalerConfig.Value : 1;
            if (_assetPtr == IntPtr.Zero || mode == 0)
            {
                return;
            }
            if (!_filterOverridden)
            {
                _origFilter = _asset.upscalingFilter;
                _origFsrOverride = _asset.fsrOverrideSharpness;
                _origFsrSharpness = _asset.fsrSharpness;
                _filterOverridden = true;
            }
            if (mode == 1)
            {
                _asset.upscalingFilter = UpscalingFilterSelection.FSR;
                _asset.fsrOverrideSharpness = true;
                _asset.fsrSharpness = Mathf.Clamp01(Plugin.FsrSharpnessConfig != null ? Plugin.FsrSharpnessConfig.Value : 0.8f);
            }
            else
            {
                _asset.upscalingFilter = UpscalingFilterSelection.Linear;
            }
        }

        // --- Gameplay camera tracking (event driven: fires on enable and on every level load) ---

        [HarmonyPatch(typeof(LoadingScreenCameraManager), nameof(LoadingScreenCameraManager.OnGameplayCameraChanged))]
        [HarmonyPostfix]
        public static void AfterGameplayCameraChanged(Camera newGameplayCamera)
        {
            _worldCam = newGameplayCamera;
            if ((UnityEngine.Object)(object)newGameplayCamera != (UnityEngine.Object)null)
            {
                Plugin.L?.LogInfo($"[RenderScale] Gameplay camera: '{newGameplayCamera.name}'");
            }
        }

        // One-time runtime confirmation that the HUD root is a ScreenSpaceOverlay canvas (unaffected by render scale).
        [HarmonyPatch(typeof(LoadingScreenCameraManager), nameof(LoadingScreenCameraManager.OnHudRootChanged))]
        [HarmonyPostfix]
        public static void AfterHudRootChanged(LoadingScreenCameraManager __instance)
        {
            if (_hudLogged)
            {
                return;
            }
            Canvas hud = __instance._hudRootCanvas;
            if ((UnityEngine.Object)(object)hud == (UnityEngine.Object)null)
            {
                return;
            }
            _hudLogged = true;
            Plugin.L?.LogInfo($"[RenderScale] HUD root '{hud.name}' is {hud.renderMode}");
        }

        // --- Pause menu and loading screen: the game switches them to world space with these coroutines ---

        [HarmonyPatch(typeof(SwitchToWorldSpaceUi._CoroutineToWorld_d__12), nameof(SwitchToWorldSpaceUi._CoroutineToWorld_d__12.MoveNext))]
        [HarmonyPostfix]
        public static void AfterToWorldStep(SwitchToWorldSpaceUi._CoroutineToWorld_d__12 __instance)
        {
            SwitchToWorldSpaceUi switcher = __instance.__4__this;
            if ((UnityEngine.Object)(object)switcher == (UnityEngine.Object)null)
            {
                return;
            }
            Camera cam = switcher.worldSpaceCamera;
            Canvas canvas = switcher.worldSpaceCanvas;
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null || (UnityEngine.Object)(object)canvas == (UnityEngine.Object)null)
            {
                return;
            }
            if (cam.name == "WorldSpaceUICamera")
            {
                _menuCanvas = canvas;
            }
            else
            {
                _loadingCanvas = canvas;
            }
        }

        // --- Keep the game's own RenderScaleManager authoritative over its value ---

        // The game sets its scale when menus open and close; remember it as the baseline.
        [HarmonyPatch(typeof(RenderScaleManager), nameof(RenderScaleManager.SetRenderScale))]
        [HarmonyPostfix]
        public static void AfterGameSetRenderScale()
        {
            if (_assetPtr == IntPtr.Zero || _assetDirty)
            {
                return;
            }
            _gameScale = ReadScale();
        }

        // The game reads quality-level scales back from the assets; make sure it never sees ours.
        [HarmonyPatch(typeof(RenderScaleManager), nameof(RenderScaleManager.GetRenderScaleByQuality))]
        [HarmonyPrefix]
        public static void BeforeGameReadsRenderScale()
        {
            WriteScale(_gameScale);
        }
    }
}
