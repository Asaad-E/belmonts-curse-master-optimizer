# Belmont's Curse Master Optimizer

**Belmont's Curse Master Optimizer** is a comprehensive performance, smoothness, and quality-of-life mod for *Castlevania: Belmont's Curse*. It delivers a rock-solid, fluid 60 FPS gameplay experience on Steam Deck and PC by eliminating the game's most frustrating traversal stutters, map loading freezes, and rendering bottlenecks.

Every optimization is fully configurable live in-game through the native **Settings > Video** menu with complete controller and Steam Deck gamepad support.

---

## Why Use This Mod?

While *Castlevania: Belmont's Curse* is an incredible retro-modern experience, the vanilla game suffers from several technical hitches that break immersion:
- Opening the map causes frame drops, and closing it triggers an annoying **~2-second freeze**.
- Moving between rooms frequently causes micro-stutters due to synchronous memory cleanups.
- Wide-angle camera zoom-outs in scenic vistas and boss arenas tank the framerate.
- Planar water reflections can look jittery or choppy during movement.

**Belmont's Curse Master Optimizer** resolves all of these issues while maintaining 100% native image sharpness and visual fidelity.

---

## Key Features

### 🗺️ Instant, Zero-Lag Map Navigation
- **The Problem:** Opening the map renders the entire 3D world behind it (dropping FPS), and closing the map freezes the game for ~2 seconds while unloading scene data.
- **The Fix:** Freezes 3D scene rendering behind the map for a fluid 60 FPS map browsing experience, and caches the map in memory so closing it is instantaneous (<1 ms) with zero recovery lag.

### 💧 Smooth 60 FPS Water Reflections
- **The Problem:** Frame-skipping water reflections cause distracting jitter and sliding artifacts as the camera moves.
- **The Fix:** Renders water reflections smoothly every single frame at 60 FPS, while optimizing internal reflection resolution and stripping unnecessary shadow passes to keep GPU and CPU usage minimal.

### 🎥 Wide-Angle & Boss Arena View Optimizer
- **The Problem:** When the camera pulls back to show huge scenic rooms or boss arenas, framerates plunge because the game suddenly tries to render hundreds of distant lights and shadow casters.
- **The Fix:** Dynamically and smoothly scales shadow distance and limits secondary dynamic lights when the camera zooms out, keeping large encounters smooth and responsive without noticeable visual popping.

### ⚡ Stutter-Free Traversal & Exploration
- **The Problem:** Running between rooms and hitting checkpoints triggers harsh garbage collection freezes (40–120 ms pauses).
- **The Fix:** Converts blocking garbage collections into smooth background incremental slices (3 ms), expands asset upload streaming buffers, and clamps physics catch-up loops to eliminate exploration hitching.

### 🔋 Global CPU Optimization & Battery Savings
- **The Problem:** Unseen enemies and animated props running complex skeletal calculations off-screen waste CPU cycles and drain handheld battery.
- **The Fix:** Intelligently pauses bone transform calculations for off-screen enemies and enables the SRP Batcher engine-wide, significantly reducing CPU draw overhead and extending battery life on the Steam Deck.

### 🔍 Razor-Sharp Text & UI (Zero Blurriness Guarantee)
- **The Problem:** Aggressive resolution downscaling can make dialogue text, damage numbers, and menu fonts blurry and soft.
- **The Fix:** Strictly locks resolution scale to 1.0 (100% native pixel-sharp rendering) on Medium and High quality presets. Dialogue, UI, and TextMeshPro font elements remain crisp and clean at all times.

### 🎮 Native In-Game Settings Menu
- **The Problem:** Having to edit configuration files or restart the game to tweak settings.
- **The Fix:** Adds clean, native options directly inside the game's **Settings > Video** menu with full gamepad D-pad and analog stick navigation. Any setting changed in-game is saved automatically.

### 🖥️ 16:10 Aspect Ratio Fix for Steam Deck
- **The Problem:** On 16:10 displays like the Steam Deck, the pause menu camera gets cropped or zoomed in awkwardly.
- **The Fix:** Automatically scales the pause menu camera to fit 16:10 screens perfectly so no art or text is cut off.

---

## In-Game Settings

Once installed, open **Settings > Video** to configure:

| Setting | Options | Description |
| :--- | :--- | :--- |
| **Graphics Quality** | Low / Medium / High | Overall graphics quality. Medium & High strictly preserve native 1:1 text sharpness. |
| **Water Reflections** | Optimized (60 FPS) / Frame-Skip / Off | Silky smooth 60 FPS reflection, legacy frame-skip, or completely off for battery savings. |
| **Fast Map Screen** | Enabled / Disabled | Eliminates the 2-second map close freeze and freezes 3D background rendering. |
| **Wide View Optimizer** | Enabled / Disabled | Automatically scales shadow distance during wide camera zoom-outs. |
| **Off-Screen CPU Culling** | Enabled / Disabled | Culls off-screen enemy bone transform updates to reduce CPU usage. |
| **Stutter & Traversal Fix** | Enabled / Disabled | Incremental background GC and expanded streaming buffers for hitch-free exploration. |

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
