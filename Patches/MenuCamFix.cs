using System;
using HarmonyLib;
using UnityEngine;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// Fits the pause menu inside WorldSpaceUICamera on 16:10 screens (Steam Deck).
    ///
    /// Event driven: the game's SwitchToWorldSpaceUi coroutines hand us the menu camera and canvas while the
    /// menu opens and closes, so nothing is searched for. The component stays disabled (no LateUpdate) except
    /// while the menu is in world space, where it only refits if the screen size changes.
    /// </summary>
    public class MenuCamFix : MonoBehaviour
    {
        private const string MenuCameraName = "WorldSpaceUICamera";

        internal static MenuCamFix Instance;
        private static IntPtr _switcherPtr;

        private static Camera _cam;
        private static IntPtr _camPtr;
        private static Canvas _canvas;
        private static float _origSize = -1f;
        private static int _fitWidth;
        private static int _fitHeight;

        public MenuCamFix(IntPtr p) : base(p)
        {
            Instance = this;
        }

        private void Awake()
        {
            enabled = false;
        }

        // Only runs while the menu is shown in world space.
        private void LateUpdate()
        {
            if (!IsWorldSpaceShown())
            {
                Restore();
                return;
            }
            if (Screen.width != _fitWidth || Screen.height != _fitHeight)
            {
                Fit();
            }
        }

        internal static bool Track(SwitchToWorldSpaceUi switcher)
        {
            if ((UnityEngine.Object)(object)switcher == (UnityEngine.Object)null)
            {
                return false;
            }
            IntPtr ptr = switcher.Pointer;
            if (ptr == _switcherPtr && (UnityEngine.Object)(object)_cam != (UnityEngine.Object)null)
            {
                return true;
            }
            // The loading screen uses SwitchToWorldSpaceUi too; only the pause menu camera was ever adjusted.
            Camera cam = switcher.worldSpaceCamera;
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null || cam.name != MenuCameraName)
            {
                return false;
            }
            _switcherPtr = ptr;
            _canvas = switcher.worldSpaceCanvas;
            if (cam.Pointer != _camPtr)
            {
                _cam = cam;
                _camPtr = cam.Pointer;
                _origSize = cam.orthographicSize;
            }
            return true;
        }

        internal static bool IsWorldSpaceShown()
        {
            return (UnityEngine.Object)(object)_cam != (UnityEngine.Object)null
                && (UnityEngine.Object)(object)_canvas != (UnityEngine.Object)null
                && _canvas.renderMode == RenderMode.WorldSpace
                && _cam.isActiveAndEnabled;
        }

        internal static void Fit()
        {
            _fitWidth = Screen.width;
            _fitHeight = Screen.height;
            if (!_cam.orthographic || (Plugin.FixMenuZoomConfig != null && !Plugin.FixMenuZoomConfig.Value))
            {
                SetSize(_origSize);
                return;
            }
            RectTransform rt = _canvas.GetComponent<RectTransform>();
            Rect rect = rt.rect;
            Vector3 scale = rt.lossyScale;
            float targetSize = Mathf.Max(rect.height * scale.y * 0.5f, rect.width * scale.x * 0.5f / _cam.aspect);
            SetSize(Mathf.Max(targetSize, _origSize));
        }

        internal static void Restore()
        {
            if ((UnityEngine.Object)(object)_cam != (UnityEngine.Object)null)
            {
                SetSize(_origSize);
            }
            if ((UnityEngine.Object)(object)Instance != (UnityEngine.Object)null)
            {
                Instance.enabled = false;
            }
        }

        private static void SetSize(float size)
        {
            if (size > 0f && _cam.orthographicSize != size)
            {
                _cam.orthographicSize = size;
            }
        }
    }

    /// <summary>Hooks the pause menu's world-space transition coroutines (they only run while it opens or closes).</summary>
    [HarmonyPatch]
    public static class MenuCamFixPatches
    {
        [HarmonyPatch(typeof(SwitchToWorldSpaceUi._CoroutineToWorld_d__12), nameof(SwitchToWorldSpaceUi._CoroutineToWorld_d__12.MoveNext))]
        [HarmonyPostfix]
        public static void AfterToWorldStep(SwitchToWorldSpaceUi._CoroutineToWorld_d__12 __instance)
        {
            if (!MenuCamFix.Track(__instance.__4__this) || !MenuCamFix.IsWorldSpaceShown())
            {
                return;
            }
            MenuCamFix.Fit();
            if ((UnityEngine.Object)(object)MenuCamFix.Instance != (UnityEngine.Object)null)
            {
                MenuCamFix.Instance.enabled = true;
            }
        }

        [HarmonyPatch(typeof(SwitchToWorldSpaceUi._CoroutineToOverlay_d__10), nameof(SwitchToWorldSpaceUi._CoroutineToOverlay_d__10.MoveNext))]
        [HarmonyPostfix]
        public static void AfterToOverlayStep(SwitchToWorldSpaceUi._CoroutineToOverlay_d__10 __instance)
        {
            // Keep the fit during the fade-out; restore once the canvas has left world space.
            if (MenuCamFix.Track(__instance.__4__this) && !MenuCamFix.IsWorldSpaceShown())
            {
                MenuCamFix.Restore();
            }
        }
    }
}
