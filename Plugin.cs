using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using BelmontsCurseMasterOptimizer.Patches;
using BelmontsCurseMasterOptimizer.UI;

namespace BelmontsCurseMasterOptimizer
{
    [BepInPlugin("belmontscurse.masteroptimizer", "Belmont's Curse Master Optimizer", "1.3.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "belmontscurse.masteroptimizer";

        internal static Plugin Instance;
        internal static ManualLogSource L;

        internal static ConfigEntry<int> QualityPresetConfig;
        internal static ConfigEntry<int> RenderScaleConfig;
        internal static ConfigEntry<int> FSRSharpnessConfig;
        internal static ConfigEntry<int> ParticleDensityConfig;
        internal static ConfigEntry<int> FoVConfig;
        internal static ConfigEntry<int> ReflectionModeConfig;
        internal static ConfigEntry<bool> FastMapConfig;
        internal static ConfigEntry<bool> ZoomOptimizerConfig;
        internal static ConfigEntry<bool> CpuCullingConfig;
        internal static ConfigEntry<bool> StutterFixConfig;
        internal static ConfigEntry<bool> FixMenuZoomConfig;

        public override void Load()
        {
            Instance = this;
            L = Log;

            QualityPresetConfig = Config.Bind<int>(
                "Graphics",
                "QualityPreset",
                2,
                new ConfigDescription("Graphics quality preset: 0 = Low, 1 = Medium, 2 = High", new AcceptableValueRange<int>(0, 2))
            );

            RenderScaleConfig = Config.Bind<int>(
                "Graphics",
                "RenderScale",
                0,
                new ConfigDescription("3D Resolution Scale: 0 = 100% (Native), 1 = 95%, 2 = 90%, 3 = 85%, 4 = 80%, 5 = 75%, 6 = 70%", new AcceptableValueRange<int>(0, 6))
            );

            FSRSharpnessConfig = Config.Bind<int>(
                "Graphics",
                "FSRSharpness",
                3,
                new ConfigDescription("FSR RCAS Edge Sharpness: 0 = 20% (Soft), 1 = 40% (Light), 2 = 60% (Balanced), 3 = 80% (Sharp), 4 = 100% (Strong)", new AcceptableValueRange<int>(0, 4))
            );

            ParticleDensityConfig = Config.Bind<int>(
                "Graphics",
                "ParticleDensity",
                1,
                new ConfigDescription("Particle density limit: 0 = 100% (Full / Native), 1 = 60% (Optimized), 2 = 30% (Low / Battery Saver)", new AcceptableValueRange<int>(0, 2))
            );

            FoVConfig = Config.Bind<int>(
                "Graphics",
                "FieldOfView",
                3,
                new ConfigDescription("Field of View / Camera Framing: 0 = Muy Cerca (0.80x), 1 = Cerca (0.90x), 2 = Medio (0.95x), 3 = Nativo (1.00x), 4 = Lejos (1.15x)", new AcceptableValueRange<int>(0, 4))
            );

            ReflectionModeConfig = Config.Bind<int>(
                "Graphics",
                "ReflectionMode",
                0,
                new ConfigDescription("Water reflection mode: 0 = Optimized (Smooth 60 FPS), 1 = Frame-Skip (30 FPS), 2 = Off", new AcceptableValueRange<int>(0, 2))
            );

            FastMapConfig = Config.Bind<bool>(
                "Performance",
                "FastMap",
                true,
                "Freezes 3D world camera while map is open and caches map scenes to eliminate the 2-second map close freeze."
            );

            ZoomOptimizerConfig = Config.Bind<bool>(
                "Performance",
                "ZoomOptimizer",
                true,
                "Dynamically scales shadow distance and light culling during wide camera zoom-outs to prevent massive FPS drops."
            );

            CpuCullingConfig = Config.Bind<bool>(
                "Performance",
                "CpuCulling",
                true,
                "Culls bone transforms and animations for off-screen enemies to save CPU time."
            );

            StutterFixConfig = Config.Bind<bool>(
                "Performance",
                "StutterFix",
                true,
                "Replaces blocking stop-the-world GC with incremental GC and increases async asset upload buffers."
            );

            FixMenuZoomConfig = Config.Bind<bool>(
                "Display",
                "FixMenuZoom",
                true,
                "Corrects the pause menu camera on 16:10 aspect ratio screens (e.g. Steam Deck)."
            );

            // Synchronize patch flags with configuration
            WaterReflectionPatches.ReflectionMode = ReflectionModeConfig.Value;
            MapPatches.FastMapEnabled = FastMapConfig.Value;
            CameraZoomPatches.ZoomOptimizerEnabled = ZoomOptimizerConfig.Value;
            CullingAndCPUPatches.CpuCullingEnabled = CpuCullingConfig.Value;
            StutterAndGCPatches.StutterFixEnabled = StutterFixConfig.Value;

            // 1. Register custom MonoBehaviours into IL2CPP runtime
            ClassInjector.RegisterTypeInIl2Cpp<VideoOptionsDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<MenuCamFix>();

            // 2. Attach drivers first so options and camera fixes run reliably
            AddComponent<VideoOptionsDriver>();
            AddComponent<MenuCamFix>();

            // 3. Apply baseline engine optimizations
            StutterAndGCPatches.ApplyEngineTweaks();
            CullingAndCPUPatches.EnsureSRPBatcher();

            // 4. Apply Harmony patches with isolated try/catch per module (Resilient loading)
            Harmony harmony = new Harmony(Guid);

            void SafePatch(Type patchType, string moduleName)
            {
                try
                {
                    harmony.PatchAll(patchType);
                    L.LogInfo($"[Harmony] Successfully hooked {moduleName}");
                }
                catch (Exception ex)
                {
                    L.LogError($"[Harmony] Error hooking {moduleName}: {ex.Message}");
                }
            }

            SafePatch(typeof(MapPatches), "MapPatches");
            SafePatch(typeof(CameraZoomPatches), "CameraZoomPatches");
            SafePatch(typeof(ParticlePatches), "ParticlePatches");
            SafePatch(typeof(WaterReflectionPatches), "WaterReflectionPatches");
            SafePatch(typeof(StutterAndGCPatches), "StutterAndGCPatches");
            SafePatch(typeof(CullingAndCPUPatches), "CullingAndCPUPatches");
            SafePatch(typeof(VideoOptionPatches), "VideoOptionPatches");

            Config.Save();
            L.LogInfo("Belmont's Curse Master Optimizer v1.3.0 successfully loaded!");
        }
    }
}
