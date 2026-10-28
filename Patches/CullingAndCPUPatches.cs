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

                    // STRICT RULE: Never cull bones on combat actors (enemies, bosses, players)
                    // Only cull non-combat environment scenery props (chains, banners, torches, chandeliers)
                    if (IsCombatActor(anim)) continue;

                    anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    culled++;
                }

                if (culled > 0)
                {
                    Plugin.L.LogDebug($"[Culling] Set CullUpdateTransforms on {culled} off-screen environment props.");
                }
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning("[Culling] AfterEnterRoom sweep error: " + ex.Message);
            }
        }

        private static bool IsCombatActor(Animator anim)
        {
            try
            {
                Transform curr = anim.transform;
                while ((UnityEngine.Object)(object)curr != (UnityEngine.Object)null)
                {
                    GameObject go = curr.gameObject;
                    string name = ((UnityEngine.Object)go).name;

                    if (name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Mob", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Monster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    // Check for combat hurtbox, damage caster, character controller, or boss components
                    if (go.GetComponent<ColliderHurtboxHolder>() != null ||
                        go.GetComponent<DamageCaster>() != null ||
                        go.GetComponent<CharacterController>() != null ||
                        go.GetComponent<BossComponent>() != null)
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
