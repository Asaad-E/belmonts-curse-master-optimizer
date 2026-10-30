using System;
using HarmonyLib;
using Il2CppSystem;
using RoomManager;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class StutterAndGCPatches
    {
        public static bool StutterFixEnabled = true;

        private const ulong IncrementalKickNs = 3000000uL; // 3 ms
        private static int _roomsSinceUnload = 0;
        private static long _memoryAtUnload = -1L;
        private static long _lastMemoryAtGC = -1L;
        private static bool _inRoomChange = false;
        private static float _savedMaxDeltaTime = -1f;

        public static void ApplyEngineTweaks()
        {
            try
            {
                // Tune async upload buffer for streaming textures/meshes without spiking frame time
                if (QualitySettings.asyncUploadBufferSize != 64)
                {
                    QualitySettings.asyncUploadBufferSize = 64;
                }
                if (QualitySettings.asyncUploadTimeSlice != 2)
                {
                    QualitySettings.asyncUploadTimeSlice = 2;
                }

                // Prevent physics spiral-of-death catch-up frames
                if (_savedMaxDeltaTime < 0f)
                {
                    _savedMaxDeltaTime = Time.maximumDeltaTime;
                }
                Time.maximumDeltaTime = Time.fixedDeltaTime * 2.0f;

                // Disable stack trace allocation on normal logs & warnings
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);

                // Disable TextMeshPro warnings to prevent main thread logging overhead
                if (TMPro.TMP_Settings.instance != null && !TMPro.TMP_Settings.instance.m_warningsDisabled)
                {
                    TMPro.TMP_Settings.instance.m_warningsDisabled = true;
                }
            }
            catch {}
        }

        // Active paced incremental GC heartbeat - disabled during active gameplay to eliminate frame-time spikes
        public static void IncrementalHeartbeat()
        {
            // Disabled during active frame loop: incremental collection is restricted to doorway/room transitions
        }

        [HarmonyPatch(typeof(RoomManagerSystem), "RoomChangeTask")]
        [HarmonyPrefix]
        public static void BeforeRoomChange()
        {
            try
            {
                _inRoomChange = true;
            }
            catch {}
        }

        [HarmonyPatch(typeof(RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterEnterRoom()
        {
            try
            {
                _inRoomChange = false;
            }
            catch {}
        }

        private static bool IsInTrueLoadingScreen()
        {
            try
            {
                var gsm = GameStateManager.GetInstance();
                if ((UnityEngine.Object)(object)gsm != (UnityEngine.Object)null)
                {
                    if (gsm.IsLoadingState())
                    {
                        return true;
                    }
                    // Also allow full cleanup during death/checkpoint fade-to-black recovery state
                    if ((UnityEngine.Object)(object)gsm.activeState != (UnityEngine.Object)null &&
                        (UnityEngine.Object)(object)gsm.activeState == (UnityEngine.Object)(object)gsm.recoveryState)
                    {
                        return true;
                    }
                }
            }
            catch {}
            return false;
        }

        [HarmonyPatch(typeof(Il2CppSystem.GC), "InternalCollect")]
        [HarmonyPrefix]
        public static bool BeforeGCCollect()
        {
            MapPerf.Note("GC.Collect requested");
            if (!StutterFixEnabled) return true;

            try
            {
                // CRITICAL RED-TEAM AUDIT FIX: Only allow full blocking GC during actual loading screens or death recovery!
                // Do NOT allow full GC when _inRoomChange is true, because <RoomChangeTask>b__37_15 calls GC.Collect() mid-doorway!
                if (IsInTrueLoadingScreen())
                {
                    _lastMemoryAtGC = Profiler.GetMonoUsedSizeLong();
                    return true;
                }

                long currentMem = Profiler.GetMonoUsedSizeLong();
                if (_lastMemoryAtGC < 0) _lastMemoryAtGC = currentMem;

                // Fix arithmetic inversion: Ratchet watermark downward when memory drops (e.g. after level purge)
                if (currentMem < _lastMemoryAtGC)
                {
                    _lastMemoryAtGC = currentMem;
                }

                // Safety valve: allow full GC only if MANAGED heap grew by > 256 MB from current watermark
                if (currentMem - _lastMemoryAtGC > (256L << 20))
                {
                    _lastMemoryAtGC = currentMem;
                    return true;
                }

                // Doorway and gameplay collection: convert to incremental slice
                if (GarbageCollector.isIncremental)
                {
                    GarbageCollector.CollectIncremental(IncrementalKickNs);
                }
                return false; // Skip blocking collection!
            }
            catch (System.Exception ex)
            {
                Plugin.L.LogWarning("[StutterFix] CollectIncremental exception: " + ex.Message);
                return true;
            }
        }

        [HarmonyPatch(typeof(LevelStateLogic), "RunGC")]
        [HarmonyPrefix]
        public static bool BeforeLevelStateLogicRunGC()
        {
            MapPerf.Note("LevelStateLogic.RunGC");
            if (!StutterFixEnabled) return true;

            try
            {
                if (IsInTrueLoadingScreen())
                {
                    _lastMemoryAtGC = Profiler.GetMonoUsedSizeLong();
                    return true;
                }

                long currentMem = Profiler.GetMonoUsedSizeLong();
                if (_lastMemoryAtGC < 0) _lastMemoryAtGC = currentMem;
                if (currentMem < _lastMemoryAtGC) _lastMemoryAtGC = currentMem;

                // Safety valve parity: allow full GC if managed heap grew by > 256 MB
                if (currentMem - _lastMemoryAtGC > (256L << 20))
                {
                    _lastMemoryAtGC = currentMem;
                    return true;
                }

                if (GarbageCollector.isIncremental)
                {
                    GarbageCollector.CollectIncremental(IncrementalKickNs);
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(Resources), "UnloadUnusedAssets")]
        [HarmonyPrefix]
        public static bool BeforeUnload(ref AsyncOperation __result, out bool __state)
        {
            MapPerf.Note("Resources.UnloadUnusedAssets");
            __state = false;
            if (!StutterFixEnabled)
            {
                return true;
            }

            try
            {
                long totalMem = Profiler.GetTotalAllocatedMemoryLong();
                if (_memoryAtUnload < 0)
                {
                    _memoryAtUnload = totalMem;
                }

                // Ratchet watermark downward when memory drops after biome unloads
                if (totalMem < _memoryAtUnload)
                {
                    _memoryAtUnload = totalMem;
                }

                // Always allow unload during true loading screens or death recovery
                if (IsInTrueLoadingScreen())
                {
                    _memoryAtUnload = totalMem;
                    return true;
                }

                if (!_inRoomChange)
                {
                    return true;
                }

                long growthMB = (totalMem - _memoryAtUnload) >> 20;

                // Only run if memory grew by > 512 MB or every 4th room
                if (growthMB < 512 && ++_roomsSinceUnload < 4)
                {
                    __state = true;
                    __result = (AsyncOperation)(object)Resources.LoadAsync("stutterfix/skipped_unload");
                    return false; // Skip the 300 ms asset unload hitch
                }

                _roomsSinceUnload = 0;
                _memoryAtUnload = totalMem;
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.L.LogWarning("[StutterFix] BeforeUnload exception: " + ex.Message);
                __state = false;
                return true; // Fail-safe: allow engine unload to proceed
            }
        }

        [HarmonyPatch(typeof(Resources), "UnloadUnusedAssets")]
        [HarmonyPostfix]
        public static void AfterUnload(bool __state)
        {
            try
            {
                if (!__state)
                {
                    _roomsSinceUnload = 0;
                }
            }
            catch {}
        }
    }
}
