using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace BelmontsCurseMasterOptimizer.Patches
{
    public static class ParticlePatches
    {
        private struct ParticleData
        {
            public int OriginalMaxParticles;
            public float OriginalEmissionRate;
        }

        private static readonly Dictionary<int, ParticleData> _trackedParticles = new Dictionary<int, ParticleData>();

        // 0 = 100% (Full / Native), 1 = 60% (Optimized), 2 = 30% (Low / Battery Saver)
        public static float GetParticleScaleFactor()
        {
            int setting = (Plugin.ParticleDensityConfig != null) ? Plugin.ParticleDensityConfig.Value : 1;
            switch (setting)
            {
                case 1: return 0.60f; // 60% Optimized
                case 2: return 0.30f; // 30% Low
                default: return 1.00f; // 100% Native
            }
        }

        public static void OptimizeParticleSystem(ParticleSystem ps)
        {
            if ((UnityEngine.Object)(object)ps == (UnityEngine.Object)null) return;

            try
            {
                int id = ((UnityEngine.Object)ps).GetInstanceID();
                if (!_trackedParticles.TryGetValue(id, out var data))
                {
                    data = new ParticleData
                    {
                        OriginalMaxParticles = ps.maxParticles,
                        OriginalEmissionRate = ps.emissionRate
                    };
                    _trackedParticles[id] = data;
                }

                float factor = GetParticleScaleFactor();
                if (factor < 0.99f)
                {
                    ps.maxParticles = Mathf.Max(1, Mathf.RoundToInt(data.OriginalMaxParticles * factor));
                    if (data.OriginalEmissionRate > 0f)
                    {
                        ps.emissionRate = data.OriginalEmissionRate * factor;
                    }

                    var rend = ps.GetComponent<ParticleSystemRenderer>();
                    if ((UnityEngine.Object)(object)rend != (UnityEngine.Object)null)
                    {
                        rend.receiveShadows = false;
                        rend.shadowCastingMode = ShadowCastingMode.Off;
                        rend.enableGPUInstancing = true;
                    }
                }
                else
                {
                    ps.maxParticles = data.OriginalMaxParticles;
                    ps.emissionRate = data.OriginalEmissionRate;
                }
            }
            catch {}
        }

        public static void ApplyToAllActiveParticles()
        {
            PruneAndApplyActiveParticles();
        }

        public static void PruneAndApplyActiveParticles()
        {
            try
            {
                var activeIds = new HashSet<int>();
                foreach (UnityEngine.Object item in (Il2CppArrayBase<UnityEngine.Object>)(object)Resources.FindObjectsOfTypeAll(Il2CppType.Of<ParticleSystem>()))
                {
                    var ps = ((Il2CppObjectBase)item).TryCast<ParticleSystem>();
                    if ((UnityEngine.Object)(object)ps != (UnityEngine.Object)null && ((Component)ps).gameObject.scene.IsValid())
                    {
                        OptimizeParticleSystem(ps);
                        activeIds.Add(((UnityEngine.Object)ps).GetInstanceID());
                    }
                }

                var deadIds = new List<int>();
                foreach (var id in _trackedParticles.Keys)
                {
                    if (!activeIds.Contains(id))
                    {
                        deadIds.Add(id);
                    }
                }

                for (int i = 0; i < deadIds.Count; i++)
                {
                    _trackedParticles.Remove(deadIds[i]);
                }
            }
            catch {}
        }

        public static void ClearTrackedParticles()
        {
            _trackedParticles.Clear();
        }
    }
}
