# Belmont's Curse Master Optimizer

**Belmont's Curse Master Optimizer** is a comprehensive performance optimization and quality-of-life mod for *Castlevania: Belmont's Curse* (Unity 2022.3.62f2, 64-bit IL2CPP, Universal Render Pipeline / URP), running with BepInEx 6 Bleeding Edge on Steam Deck (SteamOS / Proton) and PC.

This mod consolidates, enhances, and replaces legacy mods (`BelmontsCurseDeckFix`, `StutterFix`, and `MorePerformanceSettings`) into a single, high-performance, crash-resilient plugin.

---

## Features

1. **Fast Map Screen (Zero Lag & 60 FPS):**
   - Freezes 3D world geometry, lighting, shadows, and reflection passes behind the fullscreen map UI (`Camera.main.cullingMask = 0`).
   - Caches `LevelMap` scenes in memory upon first load (`ActivatePreLoaded`), converting map closing into an instantaneous `<1ms` GameObject deactivation and eliminating the 2-second Addressables scene unload stall.

2. **Smart Water Reflections (Smooth 60 FPS):**
   - Renders planar water reflections every single frame ($N=1$), completely eliminating the projection lag, visual shearing, and jitter caused by frame-skipping.
   - Strips real-time shadow passes and post-processing from the reflection camera, and optimizes resolution to 0.35x.
   - Supports fallback modes: Frame-Skip and Off.

3. **Wide View Optimizer (Dynamic Frustum / Shadow Scaler):**
   - Continuously monitors camera orthographic size during wide camera zoom-outs.
   - Dynamically scales down URP `shadowDistance` along a smooth interpolation curve ($O(1/R)$) with play-plane clamping (minimum 25m) and caps secondary dynamic lights (`maxAdditionalLightsCount = 4`) to prevent draw call and shadow-caster explosions in massive rooms.

4. **Traversal & Incremental Garbage Collection:**
   - Intercepts blocking `GC.Collect()` calls (including `LevelStateLogic.RunGC`) and converts them to background incremental collections in 3ms slices (`GarbageCollector.CollectIncremental(3000000uL)`).
   - Features safety valves allowing full collections during genuine loading screens and when heap growth exceeds 512 MB.
   - Sets texture/mesh streaming upload buffer to 128 MB and 4ms time-slice.
   - Clamps `Time.maximumDeltaTime = Time.fixedDeltaTime * 2.0f` to prevent physics spiral-of-death catch-up hitches.
   - Disables non-error stack trace generation and suppresses TextMeshPro missing-character warnings.

5. **Off-Screen Skeletal Animator Culling:**
   - Sweeps room animators on room entry, setting `AnimatorCullingMode.CullUpdateTransforms` on non-player and non-boss entities to save CPU cycles.
   - Enforces URP SRP Batcher (`GraphicsSettings.useScriptableRenderPipelineBatching = true`) engine-wide.

6. **Strict Native Text Sharpness (Zero Blurriness Guarantee):**
   - Quality presets: Low, Medium, High.
   - **Medium** and **High** strictly lock `renderScale = 1.0f` (100% native pixel crispness). Text and UI are never downscaled or blurred by upscaling filters.
   - A 1-second watchdog timer continuously re-locks `renderScale = 1.0f` against internal engine resets.

7. **Native In-Game Gamepad Menu Injection:**
   - Injects settings directly into `Settings > Video` with native controller / Steam Deck D-pad and analog stick navigation (`VerticalNavigationGroup`).
   - Automatically synchronizes all live in-game choices to `BepInEx/config/belmontscurse.masteroptimizer.cfg`.

8. **16:10 Aspect Ratio Fix for Steam Deck:**
   - Corrects `WorldSpaceUICamera` orthographic size to prevent the pause menu from being cropped or zoomed in on 16:10 screens.

---

## Building from Source

### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or higher.
- `BepInEx/core` and `BepInEx/interop` assemblies from *Castlevania: Belmont's Curse*.

### Compilation
```bash
dotnet build BelmontsCurseMasterOptimizer.csproj -c Release
```
Output assembly: `bin/Release/net6.0/BelmontsCurseMasterOptimizer.dll`

---

## Installation
1. Ensure BepInEx 6 Bleeding Edge (IL2CPP x64) is installed.
2. Remove or disable legacy mods (`BelmontsCurseDeckFix.dll` and `StutterFix.dll`).
3. Place `BelmontsCurseMasterOptimizer.dll` in `BepInEx/plugins/BelmontsCurseMasterOptimizer/`.
4. Launch the game and configure settings via `Settings > Video`.
