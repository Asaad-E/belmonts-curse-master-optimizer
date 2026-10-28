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

                    // STRICT RULE: Never cull bones on combat actors (enemies, bosses, players, traps)
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
                GameObject animGo = ((Component)anim).gameObject;
                if ((UnityEngine.Object)(object)animGo == (UnityEngine.Object)null) return true;

                // 1. Check child bone hierarchy for bone-attached weapon hitboxes, hurtboxes, or colliders
                if ((UnityEngine.Object)(object)animGo.GetComponentInChildren<ColliderHurtboxHolder>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<BaseDamageCaster>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<DamageCaster>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<BossComponent>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<CharacterController>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<Collider2D>(true) != (UnityEngine.Object)null ||
                    (UnityEngine.Object)(object)animGo.GetComponentInChildren<Rigidbody2D>(true) != (UnityEngine.Object)null)
                {
                    return true;
                }

                // 2. Walk upward to parent roots to check actor names and root components
                Transform curr = anim.transform;
                int depth = 0;
                while ((UnityEngine.Object)(object)curr != (UnityEngine.Object)null && depth < 6)
                {
                    GameObject go = curr.gameObject;
                    string name = ((UnityEngine.Object)go).name;

                    if (name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Mob", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Monster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Trap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Hazard", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    if (go.GetComponent<ColliderHurtboxHolder>() != null ||
                        go.GetComponent<BaseDamageCaster>() != null ||
                        go.GetComponent<DamageCaster>() != null ||
                        go.GetComponent<CharacterController>() != null ||
                        go.GetComponent<BossComponent>() != null ||
                        go.GetComponent<Collider2D>() != null ||
                        go.GetComponent<Rigidbody2D>() != null)
                    {
                        return true;
                    }
                    curr = curr.parent;
                    depth++;
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
