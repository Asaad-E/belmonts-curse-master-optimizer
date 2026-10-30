using System;
using System.Collections.Generic;
using HarmonyLib;
using RoomManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// Optional particle density limit.
    ///
    /// - Scales emission with the rate multipliers, so each effect keeps its own emission curve.
    /// - Remembers every value it changes and puts it back exactly when the limit returns to 100%.
    /// - At 100% (the default) nothing is touched.
    /// - Only active particle systems in the loaded scenes are visited, a short while after entering a room
    ///   and a few per frame, so doorways get no extra work.
    /// </summary>
    [HarmonyPatch]
    public static class ParticlePatches
    {
        private const int ScanDelayFrames = 20;   // after the room transition has settled
        private const int SystemsPerFrame = 24;

        private class Original
        {
            public ParticleSystem System;
            public int MaxParticles;
            public float RateOverTime;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
        }

        private static readonly Dictionary<int, Original> Changed = new Dictionary<int, Original>();
        private static readonly Queue<ParticleSystem> Pending = new Queue<ParticleSystem>();
        private static int _scanAtFrame = -1;

        // 0 = 100% (game default), 1 = 60%, 2 = 30%
        public static float Factor
        {
            get
            {
                int setting = Plugin.ParticleDensityConfig != null ? Plugin.ParticleDensityConfig.Value : 0;
                return setting == 1 ? 0.60f : setting == 2 ? 0.30f : 1.00f;
            }
        }

        private static bool Limiting => Factor < 0.99f;

        /// <summary>Called when the option changes.</summary>
        public static void OnSettingChanged()
        {
            Pending.Clear();
            RestoreAll();
            if (Limiting)
            {
                _scanAtFrame = Time.frameCount + 1;
            }
        }

        [HarmonyPatch(typeof(RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterEnterRoom()
        {
            if (Limiting)
            {
                _scanAtFrame = Time.frameCount + ScanDelayFrames;
            }
        }

        /// <summary>Called every frame from VideoOptionsDriver.Update.</summary>
        public static void Tick()
        {
            if (_scanAtFrame >= 0 && Time.frameCount >= _scanAtFrame)
            {
                _scanAtFrame = -1;
                Scan();
            }
            for (int i = 0; i < SystemsPerFrame && Pending.Count > 0; i++)
            {
                Limit(Pending.Dequeue());
            }
        }

        private static void Scan()
        {
            try
            {
                PruneDestroyed();
                foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
                {
                    if ((UnityEngine.Object)(object)ps != (UnityEngine.Object)null)
                    {
                        Pending.Enqueue(ps);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.L?.LogWarning("[Particles] scan: " + ex.Message);
            }
        }

        private static void Limit(ParticleSystem ps)
        {
            if (!Limiting || (UnityEngine.Object)(object)ps == (UnityEngine.Object)null)
            {
                return;
            }
            try
            {
                int id = ps.GetInstanceID();
                if (Changed.ContainsKey(id))
                {
                    return; // already limited at the current factor
                }
                var main = ps.main;
                var emission = ps.emission;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                var orig = new Original
                {
                    System = ps,
                    MaxParticles = main.maxParticles,
                    RateOverTime = emission.rateOverTimeMultiplier,
                    Shadows = (UnityEngine.Object)(object)renderer != (UnityEngine.Object)null ? renderer.shadowCastingMode : ShadowCastingMode.Off,
                    ReceiveShadows = (UnityEngine.Object)(object)renderer != (UnityEngine.Object)null && renderer.receiveShadows
                };
                Changed[id] = orig;

                float f = Factor;
                main.maxParticles = Math.Max(1, Mathf.RoundToInt(orig.MaxParticles * f));
                emission.rateOverTimeMultiplier = orig.RateOverTime * f;
                if ((UnityEngine.Object)(object)renderer != (UnityEngine.Object)null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            catch (Exception ex)
            {
                Plugin.L?.LogWarning("[Particles] limit: " + ex.Message);
            }
        }

        private static void RestoreAll()
        {
            foreach (Original o in Changed.Values)
            {
                if ((UnityEngine.Object)(object)o.System == (UnityEngine.Object)null)
                {
                    continue;
                }
                try
                {
                    var main = o.System.main;
                    var emission = o.System.emission;
                    main.maxParticles = o.MaxParticles;
                    emission.rateOverTimeMultiplier = o.RateOverTime;
                    var renderer = o.System.GetComponent<ParticleSystemRenderer>();
                    if ((UnityEngine.Object)(object)renderer != (UnityEngine.Object)null)
                    {
                        renderer.shadowCastingMode = o.Shadows;
                        renderer.receiveShadows = o.ReceiveShadows;
                    }
                }
                catch {}
            }
            Changed.Clear();
        }

        private static void PruneDestroyed()
        {
            var dead = new List<int>();
            foreach (KeyValuePair<int, Original> kv in Changed)
            {
                if ((UnityEngine.Object)(object)kv.Value.System == (UnityEngine.Object)null)
                {
                    dead.Add(kv.Key);
                }
            }
            for (int i = 0; i < dead.Count; i++)
            {
                Changed.Remove(dead[i]);
            }
        }
    }
}
