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

        [HarmonyPatch(typeof(RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterEnterRoom()
        {
            // CRITICAL ENGINE STABILITY FIX:
            // Never force CullUpdateTransforms on room animators!
            // In Unity 2022.3 URP, forcing CullUpdateTransforms on moving hazards
            // (e.g. Circular Saws, Moving Rails, Platforms, and Traps) causes their transform
            // matrix writebacks to be suspended when crossing culling boundaries or changing direction.
            // This corrupts the vertex transformation matrices, resulting in massive polygon explosions
            // and screen-tearing glitches across specific zones or the entire screen.
            // Leaving animators to the game's native configuration guarantees 100% smooth visual stability.
        }

        public static void EnsureSRPBatcher()
        {
            try
            {
                if (!GraphicsSettings.useScriptableRenderPipelineBatching)
                {
                    GraphicsSettings.useScriptableRenderPipelineBatching = true;
                }

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
