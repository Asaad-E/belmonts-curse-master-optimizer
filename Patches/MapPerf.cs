using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace BelmontsCurseMasterOptimizer.Patches
{
    /// <summary>
    /// Measurement only (changes nothing): records frame times for a few seconds after the map opens or closes,
    /// together with what the game and the mod did in that window, and logs one summary line.
    /// </summary>
    public static class MapPerf
    {
        private const int WindowFrames = 150;

        private static string _what;
        private static int _startFrame = -1;
        private static int _endFrame = -1;
        private static float _startTime;
        private static float _worst;
        private static int _worstAt;
        private static int _over33;
        private static int _over50;
        private static float _total;
        private static int _frames;
        private static readonly List<string> Notes = new List<string>();
        private static double _cpuTotal;
        private static double _gpuTotal;
        private static int _timed;
        private static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<FrameTiming> Timings = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<FrameTiming>(1);

        // CPU time each camera spends in URP during the window (render submission, not GPU time).
        private class CamStat
        {
            public string Name;
            public string Target;
            public int Count;
            public double TotalMs;
        }
        private static readonly Dictionary<IntPtr, CamStat> Cams = new Dictionary<IntPtr, CamStat>();
        private static readonly Stopwatch CamWatch = new Stopwatch();
        private static IntPtr _camInProgress;
        private static Il2CppSystem.Action<ScriptableRenderContext, Camera> _beginDelegate;
        private static Il2CppSystem.Action<ScriptableRenderContext, Camera> _endDelegate;

        public static void Init()
        {
            _beginDelegate = (Il2CppSystem.Action<ScriptableRenderContext, Camera>)new Action<ScriptableRenderContext, Camera>(OnBeginCamera);
            _endDelegate = (Il2CppSystem.Action<ScriptableRenderContext, Camera>)new Action<ScriptableRenderContext, Camera>(OnEndCamera);
            RenderPipelineManager.add_beginCameraRendering(_beginDelegate);
            RenderPipelineManager.add_endCameraRendering(_endDelegate);
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Active)
            {
                return;
            }
            _camInProgress = camera.Pointer;
            CamWatch.Restart();
        }

        private static void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Active || camera.Pointer != _camInProgress)
            {
                return;
            }
            double ms = CamWatch.Elapsed.TotalMilliseconds;
            if (!Cams.TryGetValue(_camInProgress, out CamStat stat))
            {
                RenderTexture rt = camera.targetTexture;
                stat = new CamStat { Name = camera.name, Target = rt != null ? $"{rt.width}x{rt.height}" : "screen" };
                Cams[_camInProgress] = stat;
            }
            stat.Count++;
            stat.TotalMs += ms;
        }

        private static bool Active => _endFrame >= 0;

        public static void Begin(string what)
        {
            if (Active)
            {
                Note(what);
                _endFrame = Time.frameCount + WindowFrames;
                return;
            }
            _what = what;
            _startFrame = Time.frameCount;
            _endFrame = _startFrame + WindowFrames;
            _startTime = Time.realtimeSinceStartup;
            _worst = 0f;
            _worstAt = 0;
            _over33 = 0;
            _over50 = 0;
            _total = 0f;
            _frames = 0;
            Notes.Clear();
            Cams.Clear();
            _cpuTotal = 0;
            _gpuTotal = 0;
            _timed = 0;
        }

        public static void Note(string what)
        {
            if (!Active)
            {
                return;
            }
            Notes.Add($"+{Time.frameCount - _startFrame}f/{(Time.realtimeSinceStartup - _startTime) * 1000f:0}ms {what}");
        }

        /// <summary>Called every frame from VideoOptionsDriver.Update.</summary>
        public static void Tick()
        {
            if (!Active || Time.frameCount == _startFrame)
            {
                return;
            }
            float ms = Time.unscaledDeltaTime * 1000f;
            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, Timings) > 0 && Timings[0].gpuFrameTime > 0)
                {
                    _cpuTotal += Timings[0].cpuFrameTime;
                    _gpuTotal += Timings[0].gpuFrameTime;
                    _timed++;
                }
            }
            catch {}
            _frames++;
            _total += ms;
            if (ms > _worst)
            {
                _worst = ms;
                _worstAt = Time.frameCount - _startFrame;
            }
            if (ms > 33.4f)
            {
                _over33++;
            }
            if (ms > 50f)
            {
                _over50++;
            }
            if (Time.frameCount >= _endFrame)
            {
                var sb = new StringBuilder();
                sb.Append($"[MapPerf] {_what}: {_frames} frames, avg {(_frames > 0 ? _total / _frames : 0f):0.0} ms, worst {_worst:0.0} ms at +{_worstAt}f, >33ms: {_over33}, >50ms: {_over50}");
                sb.Append(_timed > 0 ? $", engine cpu avg {_cpuTotal / _timed:0.0} ms, gpu avg {_gpuTotal / _timed:0.0} ms" : ", gpu timing unavailable");
                sb.Append($", mapRefresh={MapPatches.MapRefreshMode}");
                if (Notes.Count > 0)
                {
                    sb.Append(" | ").Append(string.Join(" | ", Notes));
                }
                foreach (CamStat c in Cams.Values)
                {
                    sb.Append($" | cam '{c.Name}' ({c.Target}) rendered {c.Count}x, cpu avg {c.TotalMs / Math.Max(1, c.Count):0.00} ms");
                }
                Plugin.L?.LogInfo(sb.ToString());
                _endFrame = -1;
            }
        }
    }
}
