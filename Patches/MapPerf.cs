using System.Collections.Generic;
using System.Text;
using UnityEngine;

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
                if (Notes.Count > 0)
                {
                    sb.Append(" | ").Append(string.Join(" | ", Notes));
                }
                Plugin.L?.LogInfo(sb.ToString());
                _endFrame = -1;
            }
        }
    }
}
