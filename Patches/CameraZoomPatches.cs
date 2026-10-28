using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BelmontsCurseMasterOptimizer.Patches
{
    public static class CameraZoomPatches
    {
        public static bool ZoomOptimizerEnabled = true;

        private static readonly Dictionary<IntPtr, float> _originalShadowDistances = new Dictionary<IntPtr, float>();
        private static bool _isCurrentlyZoomedOut = false;

        public static void UpdateZoomOptimization()
        {
            var pipe = GraphicsSettings.currentRenderPipeline;
            if (pipe == null) return;

            var urp = pipe.TryCast<UniversalRenderPipelineAsset>();
            if (urp == null) return;

            IntPtr urpPtr = ((Il2CppObjectBase)urp).Pointer;

            if (!_originalShadowDistances.ContainsKey(urpPtr))
            {
                _originalShadowDistances[urpPtr] = urp.shadowDistance;
            }

            float baseShadowDist = _originalShadowDistances[urpPtr];

            Camera cam = Camera.main;
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null || !ZoomOptimizerEnabled)
            {
                if (_isCurrentlyZoomedOut)
                {
                    RestoreDefaults(urp, baseShadowDist);
                }
                return;
            }

            try
            {
                float camDistZ = Mathf.Max(1.0f, Mathf.Abs(cam.transform.position.z));
                float visibleHeight;
                if (cam.orthographic)
                {
                    visibleHeight = cam.orthographicSize * 2.0f;
                }
                else
                {
                    // Clamp FOV to safe trigonometric range [1.0f, 160.0f] to guard against Cinemachine blend singularities
                    float safeFov = Mathf.Clamp(cam.fieldOfView, 1.0f, 160.0f);
                    visibleHeight = 2.0f * camDistZ * Mathf.Tan(safeFov * 0.5f * Mathf.Deg2Rad);
                }

                const float baselineHeight = 13.0f;
                // Hysteresis deadband: enter zoom-out state above 17.0f, exit below 15.0f
                const float zoomTriggerHeight = 17.0f;
                const float zoomRecoveryHeight = 15.0f;

                if (visibleHeight > (_isCurrentlyZoomedOut ? zoomRecoveryHeight : zoomTriggerHeight))
                {
                    _isCurrentlyZoomedOut = true;
                    float zoomFactor = visibleHeight / baselineHeight;

                    // Guarantee shadow cutoff stays at least 18m behind the Z=0 gameplay plane so characters never lose shadows!
                    float minSafePlayPlaneDist = cam.orthographic ? 45.0f : Mathf.Max(45.0f, camDistZ + 18.0f);
                    float effectiveBase = Mathf.Max(baseShadowDist, minSafePlayPlaneDist);
                    float targetDist = Mathf.Max(minSafePlayPlaneDist, effectiveBase / Mathf.Sqrt(zoomFactor));

                    if (Mathf.Abs(urp.shadowDistance - targetDist) > 0.5f)
                    {
                        urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, targetDist, Time.unscaledDeltaTime * 4.0f);
                    }

                    // CRITICAL LIGHT STABILITY FIX:
                    // NEVER mutate urp.maxAdditionalLightsCount dynamically at runtime!
                    // Changing maxAdditionalLightsCount forces URP to reallocate the GPU structured light buffer
                    // mid-frame, causing dynamic lights (torches, spark lights, ambient hazards) to suddenly pop on/off,
                    // producing screen-wide lighting flicker and visual glitches.
                }
                else if (_isCurrentlyZoomedOut)
                {
                    RestoreDefaults(urp, baseShadowDist);
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[ZoomOptimizer] Update error: " + ex.Message);
            }
        }

        private static void RestoreDefaults(UniversalRenderPipelineAsset urp, float baseShadowDist)
        {
            try
            {
                if (Mathf.Abs(urp.shadowDistance - baseShadowDist) > 0.1f)
                {
                    urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, baseShadowDist, Time.unscaledDeltaTime * 4.0f);
                    if (Mathf.Abs(urp.shadowDistance - baseShadowDist) <= 0.1f)
                    {
                        urp.shadowDistance = baseShadowDist;
                        _isCurrentlyZoomedOut = false;
                    }
                }
                else
                {
                    urp.shadowDistance = baseShadowDist;
                    _isCurrentlyZoomedOut = false;
                }
            }
            catch {}
        }
    }
}
