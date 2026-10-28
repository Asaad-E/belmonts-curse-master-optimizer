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
                // Re-enforce async upload buffer for streaming textures/meshes without stutter
                if (QualitySettings.asyncUploadBufferSize != 128)
                {
                    QualitySettings.asyncUploadBufferSize = 128;
                }
                if (QualitySettings.asyncUploadTimeSlice != 4)
                {
                    QualitySettings.asyncUploadTimeSlice = 4;
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

        // Active per-frame incremental GC heartbeat called from VideoOptionsDriver.Update()
        public static void IncrementalHeartbeat()
        {
            if (!StutterFixEnabled) return;
            if (GarbageCollector.GCMode != GarbageCollector.Mode.Enabled) return;

            // Grant an active 1.0 ms slice per frame during normal gameplay
            // Prevents Gen0/1 memory from out-allocating the collector during intense combat
            GarbageCollector.CollectIncremental(1000000uL);
        }

        [HarmonyPatch(typeof(RoomManagerSystem), "RoomChangeTask")]
        [HarmonyPrefix]
        public static void BeforeRoomChange()
        {
            _inRoomChange = true;
        }

        [HarmonyPatch(typeof(RoomManagerSystem), "OnEnterRoom")]
        [HarmonyPostfix]
        public static void AfterEnterRoom()
        {
            _inRoomChange = false;
        }

        private static bool IsInTrueLoadingScreen()
        {
            try
            {
                var gsm = GameStateManager.GetInstance();
                if ((UnityEngine.Object)(object)gsm != (UnityEngine.Object)null)
                {
                    return gsm.IsLoadingState();
                }
            }
            catch {}
            return false;
        }

        [HarmonyPatch(typeof(Il2CppSystem.GC), "InternalCollect")]
        [HarmonyPrefix]
        public static bool BeforeGCCollect()
        {
            if (!StutterFixEnabled) return true;

            try
            {
                // Safety valve: allow full blocking GC during actual loading screens or room changes
                if (IsInTrueLoadingScreen() || _inRoomChange)
                {
                    return true;
                }

                long currentMem = Profiler.GetTotalAllocatedMemoryLong();
                if (_lastMemoryAtGC < 0) _lastMemoryAtGC = currentMem;

                // Fix arithmetic inversion: Ratchet watermark downward when memory drops (e.g. after level purge)
                if (currentMem < _lastMemoryAtGC)
                {
                    _lastMemoryAtGC = currentMem;
                }

                // Safety valve: allow full GC if heap memory grew by > 512 MB from current watermark
                if (currentMem - _lastMemoryAtGC > (512L << 20))
                {
                    _lastMemoryAtGC = currentMem;
                    return true;
                }

                // Gameplay collection: convert to 3 ms incremental slice
                GarbageCollector.CollectIncremental(IncrementalKickNs);
                return false; // Skip blocking collection
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
            if (!StutterFixEnabled) return true;

            try
            {
                if (IsInTrueLoadingScreen() || _inRoomChange)
                {
                    return true;
                }

                GarbageCollector.CollectIncremental(IncrementalKickNs);
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
            __state = false;
            if (!StutterFixEnabled || !_inRoomChange)
            {
                return true;
            }

            long totalMem = Profiler.GetTotalAllocatedMemoryLong();
            if (_memoryAtUnload < 0)
            {
                _memoryAtUnload = totalMem;
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

        [HarmonyPatch(typeof(Resources), "UnloadUnusedAssets")]
        [HarmonyPostfix]
        public static void AfterUnload(bool __state)
        {
            if (!__state)
            {
                _roomsSinceUnload = 0;
            }
        }
    }
}
