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
            bool isValidOrtho = (UnityEngine.Object)(object)cam != (UnityEngine.Object)null && cam.orthographic;

            // Cutscene or perspective camera: smoothly restore baseline values rather than aborting and leaving shadows choked!
            if (!isValidOrtho || !ZoomOptimizerEnabled)
            {
                if (Mathf.Abs(urp.shadowDistance - baseShadowDist) > 0.1f)
                {
                    urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, baseShadowDist, Time.unscaledDeltaTime * 4.0f);
                }
                if (urp.maxAdditionalLightsCount != baseLights)
                {
                    urp.maxAdditionalLightsCount = baseLights;
                }
                return;
            }

            try
            {
                const float baseSize = 5.5f;
                float currentSize = cam.orthographicSize;

                if (currentSize > baseSize)
                {
                    float ratio = currentSize / baseSize;
                    // Clamp to minimum 45.0m to guarantee character feet and boss shadows never clip at camera Z = -20m
                    float targetDist = Mathf.Max(45.0f, baseShadowDist / (1.0f + (ratio - 1.0f) * 1.5f));
                    urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, targetDist, Time.unscaledDeltaTime * 4.0f);

                    // Cap additional lights to 4 on wide views to avoid draw-call explosion
                    if (ratio > 1.25f && urp.maxAdditionalLightsCount > 4)
                    {
                        urp.maxAdditionalLightsCount = 4;
                    }
                }
                else
                {
                    // Restore smoothly on zoom-in
                    if (Mathf.Abs(urp.shadowDistance - baseShadowDist) > 0.1f)
                    {
                        urp.shadowDistance = Mathf.Lerp(urp.shadowDistance, baseShadowDist, Time.unscaledDeltaTime * 4.0f);
                    }
                    if (urp.maxAdditionalLightsCount != baseLights)
                    {
                        urp.maxAdditionalLightsCount = baseLights;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[ZoomOptimizer] Update error: " + ex.Message);
            }
        }
    }
}
