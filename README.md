# Belmont's Curse Master Optimizer

**Belmont's Curse Master Optimizer** is a comprehensive performance, smoothness, and quality-of-life mod for *Castlevania: Belmont's Curse*. It delivers a rock-solid, fluid 60 FPS gameplay experience on Steam Deck and PC by eliminating the game's most frustrating traversal stutters, map loading freezes, and rendering bottlenecks.

Every optimization is fully configurable live in-game through the native **Settings > Video** menu with complete controller and Steam Deck gamepad support.

---

## Why Use This Mod?

While *Castlevania: Belmont's Curse* is an incredible retro-modern experience, the vanilla game suffers from several technical hitches that break immersion:
- The map screen redraws a full-resolution 3D map every frame, dropping the framerate below 30 FPS while it is open.
- Moving between rooms frequently causes micro-stutters due to synchronous memory cleanups.
- Wide-angle camera zoom-outs in scenic vistas and boss arenas tank the framerate.
- Planar water reflections can look jittery or choppy during movement.

**Belmont's Curse Master Optimizer** resolves all of these issues while maintaining 100% native image sharpness and visual fidelity.

---

## Key Features

### Faster Map Screen
- **The Problem:** While the map is open, the game redraws the whole 3D level map into a 1920x1080 texture every single frame, even when nothing on the map is moving. In testing this alone took the map screen from 60 FPS to below 30 FPS.
- **The Fix:** **Map Refresh Rate** redraws the map only as often as needed. **Smart** (default) redraws it whenever the cursor, camera or zoom moves and every 4th frame otherwise; **Eco** redraws it every 4th frame while moving and every 8th at rest. The map keeps showing its last image in between. Map cameras also skip shadows, post-processing and HDR, and map transitions are shortened.

### Smooth 60 FPS Water Reflections
- **The Problem:** Frame-skipping water reflections cause distracting jitter and sliding artifacts as the camera moves.
- **The Fix:** Renders water reflections smoothly every single frame at 60 FPS, while optimizing internal reflection resolution and stripping unnecessary shadow passes to keep GPU and CPU usage minimal.

### Wide-Angle & Boss Arena View Optimizer
- **The Problem:** When the camera pulls back to show huge scenic rooms or boss arenas, framerates plunge because the game suddenly tries to render hundreds of distant lights and shadow casters.
- **The Fix:** Dynamically and smoothly scales shadow distance and limits secondary dynamic lights when the camera zooms out, keeping large encounters smooth and responsive without noticeable visual popping.

### Stutter-Free Traversal & Exploration
- **The Problem:** Running between rooms and hitting checkpoints triggers harsh garbage collection freezes (40–120 ms pauses).
- **The Fix:** Converts blocking garbage collections into smooth background incremental slices (3 ms), expands asset upload streaming buffers, and clamps physics catch-up loops to eliminate exploration hitching.

### Optional Particle Density Limit
- **The Problem:** Effect-heavy rooms can cost a lot on handheld GPUs.
- **The Fix:** **Particle Density** lowers particle counts to 80% or 60%. Each effect keeps its own emission curve, every changed value is restored exactly when you return to 100%, and the work is spread over a few frames after entering a room so doorways stay smooth. At 100% (default) particles are untouched.

### Native Image Sharpness
- The mod never changes the render resolution: the game, HUD, text and menus always render at your display resolution.

### Native In-Game Settings Menu
- **The Problem:** Having to edit configuration files or restart the game to tweak settings.
- **The Fix:** Adds clean, native options directly inside the game's **Settings > Video** menu with full gamepad D-pad and analog stick navigation. A **Settings Page** selector at the top splits the list into the game's own options and the optimizer's Graphics and Performance pages, so the menu always fits on screen. Any setting changed in-game is saved automatically.

### 16:10 Aspect Ratio Fix for Steam Deck
- **The Problem:** On 16:10 displays like the Steam Deck, the pause menu camera gets cropped or zoomed in awkwardly.
- **The Fix:** Automatically scales the pause menu camera to fit 16:10 screens perfectly so no art or text is cut off.

---

## In-Game Settings

Once installed, open **Settings > Video** to configure:

| Setting | Options | Description |
| :--- | :--- | :--- |
| Page | Setting | Options | Description |
| :--- | :--- | :--- | :--- |
| Top | **Settings Page** | Standard Video / Optimizer: Graphics / Optimizer: Performance | Chooses which group of options is shown. |
| Standard Video | **Max FPS** | 30 / 40 / 45 / 60 / 120 / Unlimited | Game framerate limit, with added 40 FPS and 45 FPS modes for Steam Deck. At 40/45 FPS the mod turns Unity vsync off so the Deck paces frames itself; with vsync on a 60 Hz screen, any slow frame drops straight to 30 FPS. |
| Graphics | **Graphics Quality** | Low / Medium / High | Overall graphics quality and asset fidelity. |
| Graphics | **Particle Density** | 100% / 80% / 60% | Optional particle limit; 100% leaves particles untouched. |
| Graphics | **Water Reflections** | Optimized (60 FPS) / Frame-Skip / Off | Smooth reflections, legacy frame-skip, or off for battery savings. |
| Performance | **Fast Map Screen** | Enabled / Disabled | Optimized map cameras and shorter map transitions. |
| Performance | **Map Refresh Rate** | Every frame / Half rate / Smart / Eco | How often the level map is redrawn while the map screen is open. |
| Performance | **Wide View Optimizer** | Enabled / Disabled | Automatically scales shadow distance during wide camera zoom-outs. |
| Performance | **Stutter & Traversal Fix** | Enabled / Disabled | Incremental background GC and expanded streaming buffers for hitch-free exploration. |

---

## Installation

1. Make sure you have **BepInEx 6 Bleeding Edge (IL2CPP x64)** installed in your game directory.
2. If you previously installed `BelmontsCurseDeckFix.dll` or `StutterFix.dll`, remove or rename them (e.g. `.dll.disabled`).
3. Download or build `BelmontsCurseMasterOptimizer.dll`.
4. Place `BelmontsCurseMasterOptimizer.dll` into your `BepInEx/plugins/BelmontsCurseMasterOptimizer/` folder.
5. Launch the game and enjoy smooth 60 FPS gameplay!

---

## Building from Source

### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or higher.
- Game interop assemblies (`BepInEx/core` and `BepInEx/interop`).

### Build Command
```bash
dotnet build BelmontsCurseMasterOptimizer.csproj -c Release
```
Output binary: `bin/Release/net6.0/BelmontsCurseMasterOptimizer.dll`

---

## Credits & Acknowledgments

- Heartfelt thanks to the authors of **BelmontsCurseDeckFix**, **StutterFix**, and **MorePerformanceSettings** for their pioneering research, inspiration, and initial groundwork in the *Castlevania: Belmont's Curse* modding community.
- Built using the [BepInEx](https://github.com/BepInEx/BepInEx) framework and [HarmonyX](https://github.com/BepInEx/HarmonyX).
