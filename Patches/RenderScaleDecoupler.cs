using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// Per-camera URP render scale: only the gameplay camera renders below native resolution.
    ///
    /// Why this works without touching any Canvas:
    /// - HUD_Canvas, P_UI_GameMenus, dialogue bubbles, interact prompts and Doozy popups/tooltips are all
    ///   ScreenSpaceOverlay canvases, which Unity draws after URP at native resolution.
    /// - The only UI that goes through URP is drawn by dedicated Base cameras (WorldSpaceUICamera while the
    ///   pause menu is in its world-space mode, the loading-screen WorldSpaceCamera, the title-screen camera).
    ///   No camera stacking is used, so each of those can get its own scale.
    /// - In this URP 14 build, RenderCameraStack invokes BeginCameraRendering before InitializeCameraData,
    ///   which is where asset.renderScale is read. Writing the asset field from beginCameraRendering
    ///   therefore sets the scale of exactly the camera about to render.
    ///
    /// Per frame this costs one dictionary lookup and at most one float write per camera; no scans,
    /// no menu polling, no Canvas changes.
    /// </summary>
    [HarmonyPatch]
    public static class RenderScaleDecoupler
    {
        private enum CameraKind : byte
        {
            World,     // the gameplay camera: user scale
            Offscreen, // renders into a RenderTexture (map, reflections, captures): game's own scale
            Screen     // any other camera presenting to the screen (menus, loading, title): native
        }

        public static readonly int[] Percents = { 100, 95, 90, 85, 80, 75, 70 };

        internal static float UserScale = 1f;
        private static bool _active;

        private static UniversalRenderPipelineAsset _asset;
        private static IntPtr _assetPtr;
        private static bool _assetDirty = true;
        private static int _scaleOffset = -1;
        private static float _gameScale = 1f; // the value the game believes the asset holds
        private static float _written = -1f;  // the value currently in the asset

        private static bool _filterOverridden;
        private static UpscalingFilterSelection _origFilter;
        private static bool _origFsrOverride;
        private static float _origFsrSharpness;

        private static IntPtr _worldPtr;
        private static readonly Dictionary<IntPtr, CameraKind> Kinds = new Dictionary<IntPtr, CameraKind>();

        // Held so the IL2CPP-side delegates are never collected.
        private static Il2CppSystem.Action<ScriptableRenderContext, Camera> _beginCameraDelegate;
        private static UnityEngine.Events.UnityAction<Scene> _sceneUnloadedDelegate;
        private static bool _hudLogged;

        public static void Init()
        {
            IntPtr field = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<UniversalRenderPipelineAsset>.NativeClassPtr, "m_RenderScale");
            _scaleOffset = (int)IL2CPP.il2cpp_field_get_offset(field);

            _beginCameraDelegate = (Il2CppSystem.Action<ScriptableRenderContext, Camera>)new Action<ScriptableRenderContext, Camera>(OnBeginCameraRendering);
            RenderPipelineManager.add_beginCameraRendering(_beginCameraDelegate);

            _sceneUnloadedDelegate = (UnityEngine.Events.UnityAction<Scene>)new Action<Scene>(_ => Kinds.Clear());
            SceneManager.add_sceneUnloaded(_sceneUnloadedDelegate);
        }

        public static void SetUserPercent(int percent)
        {
            UserScale = Mathf.Clamp(percent, 70, 100) / 100f;
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
                }
            }
            if (_active && !_assetDirty)
            {
                ApplyUpscaler();
            }
            Plugin.L?.LogInfo($"[RenderScale] 3D resolution {Mathf.RoundToInt(UserScale * 100f)}% (UI and menus stay native)");
        }

        public static int PercentToIndex(int percent)
        {
            int idx = Array.IndexOf(Percents, percent);
            return idx < 0 ? 0 : idx;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_active)
            {
                return;
            }
            if (_assetDirty && !RefreshAsset())
            {
                return;
            }

            IntPtr ptr = camera.Pointer;
            if (!Kinds.TryGetValue(ptr, out CameraKind kind))
            {
                kind = Classify(camera, ptr);
                Kinds[ptr] = kind;
            }

            switch (kind)
            {
                case CameraKind.World:
                    WriteScale(Math.Min(UserScale, _gameScale));
                    break;
                case CameraKind.Offscreen:
                    WriteScale(_gameScale);
                    break;
                default:
                    WriteScale(Math.Max(1f, _gameScale));
                    break;
            }
        }

        private static CameraKind Classify(Camera camera, IntPtr ptr)
        {
            if (ptr == _worldPtr)
            {
                return CameraKind.World;
            }
            return (UnityEngine.Object)(object)camera.targetTexture != (UnityEngine.Object)null ? CameraKind.Offscreen : CameraKind.Screen;
        }

        private static unsafe float ReadScale()
        {
            return *(float*)((byte*)_assetPtr + _scaleOffset);
        }

        private static unsafe void WriteScale(float value)
        {
            if (value == _written || _assetPtr == IntPtr.Zero)
            {
                return;
            }
            *(float*)((byte*)_assetPtr + _scaleOffset) = value;
            _written = value;
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
                _written = _gameScale;
                Plugin.L?.LogInfo($"[RenderScale] Tracking URP asset '{urp.name}' (game scale {_gameScale:0.00})");
            }
            ApplyUpscaler();
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
            bool valid = (UnityEngine.Object)(object)newGameplayCamera != (UnityEngine.Object)null;
            _worldPtr = valid ? newGameplayCamera.Pointer : IntPtr.Zero;
            Kinds.Clear();
            if (valid)
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
            if (hud.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Plugin.L?.LogInfo($"[RenderScale] HUD root '{hud.name}' is ScreenSpaceOverlay: native resolution at any 3D scale");
            }
            else
            {
                Plugin.L?.LogWarning($"[RenderScale] HUD root '{hud.name}' is {hud.renderMode}: it will follow the 3D scale of its camera");
            }
        }

        // --- Keep the game's own RenderScaleManager authoritative over its value ---

        // SwitchToWorldSpaceUi animates the scale while the pause menu opens and closes.
        [HarmonyPatch(typeof(RenderScaleManager), nameof(RenderScaleManager.SetRenderScale))]
        [HarmonyPostfix]
        public static void AfterGameSetRenderScale()
        {
            if (_assetPtr == IntPtr.Zero || _assetDirty)
            {
                return;
            }
            _gameScale = ReadScale();
            _written = _gameScale;
        }

        // The game reads quality-level scales back from the assets; make sure it never sees ours.
        [HarmonyPatch(typeof(RenderScaleManager), nameof(RenderScaleManager.GetRenderScaleByQuality))]
        [HarmonyPrefix]
        public static void BeforeGameReadsRenderScale()
        {
            WriteScale(_gameScale);
        }

        /// <summary>A quality change recreates the pipeline, possibly with a different asset.</summary>
        [HarmonyPatch]
        public static class PipelinePatches
        {
            [HarmonyPatch(typeof(UniversalRenderPipeline), MethodType.Constructor, new Type[] { typeof(UniversalRenderPipelineAsset) })]
            [HarmonyPostfix]
            public static void AfterPipelineCreated()
            {
                _assetDirty = true;
            }
        }
    }
}
