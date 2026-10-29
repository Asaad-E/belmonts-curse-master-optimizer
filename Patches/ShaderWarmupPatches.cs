using System;
using System.Diagnostics;
using HarmonyLib;
using RoomManager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BelmontsCurseMasterOptimizer.Patches
{
    [HarmonyPatch]
    public static class ShaderWarmupPatches
    {
        public static bool ShaderWarmupEnabled = true;

        private static float _lastWarmupTime = -30f;
        private const float MinWarmupInterval = 8.0f; // Minimum 8 seconds between room-level warmups

        public static void WarmupNow(string reason, bool force = false)
        {
            try
            {
                if (!ShaderWarmupEnabled) return;

                float now = Time.realtimeSinceStartup;
                if (!force && (now - _lastWarmupTime < MinWarmupInterval))
                {
                    return;
                }
                _lastWarmupTime = now;

                var sw = Stopwatch.StartNew();

                // 1. Trigger native driver pipeline shader warmup for all currently loaded shaders
                Shader.WarmupAllShaders();

                // 2. Discover and warm up any uncompiled ShaderVariantCollections
                int collectionsWarmed = 0;
                int variantsWarmed = 0;

                var collections = Resources.FindObjectsOfTypeAll<ShaderVariantCollection>();
                if (collections != null)
                {
                    for (int i = 0; i < collections.Length; i++)
                    {
                        var col = collections[i];
                        if (col != null)
                        {
                            try
                            {
                                if (!col.isWarmedUp)
                                {
                                    col.WarmUp();
                                    collectionsWarmed++;
                                    variantsWarmed += col.variantCount;
                                }
                            }
                            catch {}
                        }
                    }
                }

                sw.Stop();
                if (sw.ElapsedMilliseconds > 2 || collectionsWarmed > 0)
                {
                    Plugin.L?.LogInfo($"[ShaderWarmup] Pre-warmed shaders ({reason}): {collectionsWarmed} collection(s), {variantsWarmed} variant(s) in {sw.ElapsedMilliseconds} ms.");
                }
            }
            catch (Exception ex)
            {
                Plugin.L?.LogWarning($"[ShaderWarmup] Warmup encountered error ({reason}): {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
        [HarmonyPostfix]
        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                string sceneName = scene.IsValid() ? scene.name : "Scene";
                WarmupNow($"Scene Loaded: {sceneName}", force: true);
            }
            catch {}
        }

        public static void OnRoomTransition()
        {
            try
            {
                WarmupNow("Room Transition", force: false);
            }
            catch {}
        }
    }
}
