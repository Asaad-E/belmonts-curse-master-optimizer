using System;
using HarmonyLib;
using RoomManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class CullingAndCPUPatches
    {
        public static bool CpuCullingEnabled = true;

        [HarmonyPatch(typeof(RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterEnterRoom()
        {
            if (!CpuCullingEnabled) return;

            try
            {
                var animators = UnityEngine.Object.FindObjectsOfType<Animator>();
                if (animators == null) return;

                int culled = 0;
                for (int i = 0; i < animators.Length; i++)
                {
                    var anim = animators[i];
                    if ((UnityEngine.Object)(object)anim == (UnityEngine.Object)null) continue;
                    if (anim.cullingMode != AnimatorCullingMode.AlwaysAnimate) continue;

                    // Whitelist player and bosses
                    if (IsPlayerOrBoss(anim)) continue;

                    anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    culled++;
                }

                if (culled > 0)
                {
                    Plugin.L.LogDebug($"[Culling] Set CullUpdateTransforms on {culled} off-screen animators.");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Culling] AfterEnterRoom sweep error: " + ex.Message);
            }
        }

        private static bool IsPlayerOrBoss(Animator anim)
        {
            try
            {
                Transform curr = anim.transform;
                while ((UnityEngine.Object)(object)curr != (UnityEngine.Object)null)
                {
                    string name = ((UnityEngine.Object)curr.gameObject).name;
                    if (name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    if (curr.GetComponent<BossComponent>() != null)
                    {
                        return true;
                    }
                    curr = curr.parent;
                }
            }
            catch {}
            return false;
        }

        public static void EnsureSRPBatcher()
        {
            try
            {
                // Set directly on GraphicsSettings for immediate engine-wide activation
                GraphicsSettings.useScriptableRenderPipelineBatching = true;

                var pipe = GraphicsSettings.currentRenderPipeline;
                if (pipe != null)
                {
                    var urp = pipe.TryCast<UniversalRenderPipelineAsset>();
                    if (urp != null && !urp.useSRPBatcher)
                    {
                        urp.useSRPBatcher = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[SRPBatcher] EnsureSRPBatcher error: " + ex.Message);
            }
        }
    }
}
