# 🧟 Top-Down Survival Horror (Godot 4 C#)

A modular, component-based 2D top-down survival horror demo built with **Godot 4.x** and **C# (.NET 8.0)**, inspired by classic survival horror mechanics (deliberate combat, scarce ammunition, darkness and flashlight navigation, and stamina-driven evasion).

---

## 🎮 Core Gameplay Mechanics

- **Movement & Aiming:** 8-directional top-down character movement (`CharacterBody2D`) with precision mouse cursor aiming and laser-sight targeting.
- **Resource-Heavy Combat:** Scarce ammunition, tactical reserve ammo management, and deliberate, interruptible reload timings.
- **Flashlight & Lighting:** Pitch-black ambient environment relying on a directional flashlight cone (`PointLight2D` attached to player rotation) with realistic voltage flickers.
- **Stamina & Evasion:** Dash ability with temporary invulnerability frames and after-image ghosting, governed by a regenerating stamina meter.
- **Dynamic Pickups:** Data-driven item drops (First Aid kits, 9mm ammo boxes) dropped on enemy defeat.

---

## 📐 Architecture & Clean Code Standards

The codebase adheres strictly to clean architecture principles:

```
├── res://Assets/               # Textures, Shaders, Sprites
├── res://Prefabs/              # Reusable entity scenes (Player, Zombie, Bullet, Pickup, Box)
├── res://Resources/            # Data-driven Godot Resources (.tres)
│   ├── Enemies/                # ZombieStats.tres
│   └── Items/                  # Medkit.tres, AmmoPack.tres
├── res://Scenes/               # Level and world scenes (World.tscn)
└── res://Scripts/              # C# (.NET) source code separated by domains
    ├── Core/                   # Interfaces (IDamageable, IState), FSM, Resources
    ├── Entities/               # Entity controllers (Player, Zombie, Bullet, Pickup)
    │   ├── Components/         # Modular Node components (Health, Movement, Stamina, Inventory, Flashlight)
    │   └── States/             # FSM States (Idle, Walk, Dash, Chase, Attack, Dead)
    ├── Systems/                # WorldManager, ProjectilePool (zero-GC object pooling)
    └── UI/                     # Data-driven UI controllers (HUDUI, CrosshairUI)
```

- **Component-Based Design:** Entities are composed of isolated, reusable Node components (`HealthComponent`, `MovementComponent`, `StaminaComponent`, `InventoryComponent`, `FlashlightComponent`). No monolithic God classes.
- **Finite State Machines (FSM):** Player and Enemy AI logic are encapsulated in state classes (`PlayerIdleState`, `PlayerWalkState`, `ZombieChaseState`, `ZombieAttackState`, etc.).
- **Object Pooling:** Bullets and frequent projectile instances are recycled via `ProjectilePool` to eliminate Garbage Collection (GC) pauses during combat.
- **Decoupled UI:** UI listens passively to component events/signals (`HealthChanged`, `StaminaChanged`, `AmmoChanged`). The UI never mutates state directly.
- **Scene Unique Nodes (`%`) & Exports:** No hardcoded node string paths (`GetNode("Path/To/Node")`). All nodes use `[Export]` or Scene Unique Names with strict null safety validation.

---

## 🚀 Prerequisites

1. **Godot Engine 4.x (.NET / Mono version)**
   - Download the **.NET version** of Godot (e.g., Godot 4.3+ or 4.7+ Mono).
   - Standard Godot builds without .NET will not compile C# projects.
2. **.NET SDK**
   - **.NET 8.0 SDK** or later installed on your machine (`dotnet --version`).

---

## 📦 Dependency Installation & Initial Setup

Godot C# projects require the `Godot.NET.Sdk` package, which is bundled inside the Godot installation directory. Follow these steps when setting up the project for the first time:

### Step 1: Configure Godot Local NuGet Package Source

When Godot .NET builds a project, it resolves `Godot.NET.Sdk`, `GodotSharp`, and `GodotSharpEditor` from your local Godot installation.

This project includes a [nuget.config](file:///home/alirezaabd/Main/Game-Development/example-game/nuget.config) file that configures:
1. `nuget.org` (for standard .NET dependencies)
2. `GodotLocal` (pointing to the local Godot package path)

#### Linux Setup:
Make sure your GodotSharp package directory is linked to `~/.local/opt/godot/Godot_v4.7.2-stable_mono_linux_x86_64` (or update `nuget.config` with the exact path to your Godot installation):

```bash
# Example: If Godot is installed at ~/Applications/Godot:
mkdir -p ~/.local/opt/godot
ln -sfn ~/Applications/Godot ~/.local/opt/godot/Godot_v4.7.2-stable_mono_linux_x86_64
```

Alternatively, edit `nuget.config` to point `GodotLocal` directly to your Godot installation's `GodotSharp/Tools/nupkgs` directory:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="GodotLocal" value="/path/to/your/Godot/GodotSharp/Tools/nupkgs" />
  </packageSources>
</configuration>
```

---

### Step 2: Restore Dependencies & Build

Open your terminal in the project root directory and run:

```bash
# Restore NuGet dependencies
dotnet restore "Top Down Demo.csproj"

# Build the C# solution
dotnet build "Top Down Demo.csproj"
```

You should see:
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

### Step 3: Run the Game

#### Option A: Via the Godot Editor
1. Launch **Godot Engine (.NET version)**.
2. Click **Import**, navigate to this folder, select `project.godot`, and open the project.
3. Press **F5** (or click the **Play** button at the top-right) to launch `res://Scenes/World.tscn`.

#### Option B: Via Command Line
```bash
godot --path . res://Scenes/World.tscn
```

---

## 🕹️ Controls

| Action | Key / Input |
| :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` / Arrow Keys |
| **Aim** | Mouse Cursor |
| **Shoot** | Left Mouse Button (`LMB`) |
| **Reload** | `R` |
| **Dash (Evade)** | `Space` |
| **Restart (Game Over)** | `R` |
| **Quit Game** | `Escape` |

---

## 🛠️ Tech Stack

- **Engine:** [Godot Engine](https://godotengine.org/) (4.7+ Mono / .NET)
- **Language:** C# (.NET 8.0)
- **Physics:** Godot 2D Physics Engine / Jolt 3D configured
- **Rendering:** Forward+ Mobile/Desktop renderer with 2D Canvas Modulate & PointLight2D Shadows
