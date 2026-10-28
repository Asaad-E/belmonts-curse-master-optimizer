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
        private static readonly Dictionary<IntPtr, int> _originalAdditionalLights = new Dictionary<IntPtr, int>();
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
                _originalAdditionalLights[urpPtr] = urp.maxAdditionalLightsCount;
            }

            float baseShadowDist = _originalShadowDistances[urpPtr];
            int baseLights = _originalAdditionalLights[urpPtr];

            Camera cam = Camera.main;
            if ((UnityEngine.Object)(object)cam == (UnityEngine.Object)null || !ZoomOptimizerEnabled)
            {
                if (_isCurrentlyZoomedOut)
                {
                    RestoreDefaults(urp, baseShadowDist, baseLights);
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
                const float zoomTriggerHeight = 16.5f; // ~1.27x zoom out

                if (visibleHeight > zoomTriggerHeight)
                {
                    _isCurrentlyZoomedOut = true;
                    float zoomFactor = visibleHeight / baselineHeight;

                    // Guarantee shadow cutoff stays at least 18m behind the Z=0 gameplay plane so characters never lose shadows!
                    float minSafePlayPlaneDist = cam.orthographic ? 45.0f : Mathf.Max(45.0f, camDistZ + 18.0f);
                    float effectiveBase = Mathf.Max(baseShadowDist, minSafePlayPlaneDist);
                    float targetDist = Mathf.Max(minSafePlayPlaneDist, effectiveBase / Mathf.Sqrt(zoomFactor));

                    urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, targetDist, Time.unscaledDeltaTime * 4.0f);

                    // Cap additional dynamic lights to 4 on wide views to avoid draw-call explosion
                    if (zoomFactor > 1.25f && urp.maxAdditionalLightsCount > 4)
                    {
                        urp.maxAdditionalLightsCount = 4;
                    }
                }
                else if (_isCurrentlyZoomedOut)
                {
                    RestoreDefaults(urp, baseShadowDist, baseLights);
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[ZoomOptimizer] Update error: " + ex.Message);
            }
        }

        private static void RestoreDefaults(UniversalRenderPipelineAsset urp, float baseShadowDist, int baseLights)
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
                    _isCurrentlyZoomedOut = false;
                }

                if (urp.maxAdditionalLightsCount != baseLights)
                {
                    urp.maxAdditionalLightsCount = baseLights;
                }
            }
            catch {}
        }
    }
}
