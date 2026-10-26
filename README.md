# 🧟 Top-Down Survival Horror (Godot 4 C#)

A modular, component-based 2D top-down survival horror demo built with **Godot 4.x** and **C# (.NET 8.0)**, inspired by classic survival horror mechanics (deliberate combat, scarce ammunition, darkness and flashlight navigation, and stamina-driven evasion).

---

## 🎮 Core Gameplay Mechanics

- **Movement & Aiming:** 8-directional top-down character movement (`CharacterBody2D`) with precision mouse cursor aiming, RMB tactical aim mode (zoom, tight spread, slower walk), and laser-sight targeting.
- **Five-chapter campaign (in development):** Seven connected spaces, maintenance credential and fuse recovery, a power-routing puzzle, a field-recorder checkpoint, persistent objectives, and a gated transition into the bunker. Act 2 now includes nine connected rooms, security clearance, an optional armory, and a persistent return encounter. A playable decontamination transfer now connects the bunker to the laboratory, with two utility repairs, occupancy interlocks, a pressure/purge cycle, and an abort control. Act 3 adds eight mapped spaces, playable rescue and service-bypass branches, filtration repair, research recovery, and optional Archivist evidence. Act 4 now includes a nine-room archive, evidence vault, three confrontation outcomes, mechanical lift repairs, and ordered freight startup. Act 5 now adds seven industrial spaces, emergency-power preparation, a telegraphed Unit-9 fight, reusable coolant, radio/roof traversal, and a helicopter approach/boarding/takeoff sequence.
- **Original short demo:** `Scenes/World.tscn` retains the separate Sector-4 generator, terminal defense, and helipad scenario.
- **RE4-Style Finishing Movements (`[F]` / `[E]` Melee Takedown):** Inflict heavy burst damage (shotgun blast, rapid fire) or reduce an enemy's health below 35% to stun them. Approaching a dazed zombie reveals a pulsating action prompt badge (`[F] KICK / FINISH`). Triggering it performs an invulnerable cinematic lunge with slow-motion hit-stop and camera trauma:
  - **Roundhouse Kick**: 360-degree spin kick executing the target with an explosive shockwave that sends surrounding zombies flying backward with ragdoll momentum.
  - **Tactical Combat Knife**: Fast cross-cut slash with gleaming blade trails and violent blood bursts.
- **Resident Evil 2D Inventory System (`[Tab]` / `[I]`):** Classic 8-slot grid inventory with an animated ECG heartbeat monitor (FINE / CAUTION / DANGER), item inspection, consumption, and item combining mechanics (e.g., Green Herb + Blue Herb = Mixed Herb).
- **Tactical Facility Map & Radar (`[M]`):** Dynamic corner radar HUD tracking player position, facing angle, and objective waypoints, plus a full-screen facility blueprint view.
- **Pause Menu System (`[Escape]` / `[P]`):** Seamless game pause with quick access to resume, inventory, tactical map, restart, and exit.
- **Rich Environmental World:** Multi-themed facility sectors (Security Checkpoint, Substation, Bio-Lab, Armory, Helipad) featuring reinforced steel walls, cleanroom lab panels, office desks, cryo containment tubes with bubbling particles, biohazard barrels, and armory lockers, fully sealed with impenetrable perimeter boundaries.
- **Contextual Interaction System (`[E]`):** Examine terminals, pick up keycards, activate generators, and open hydraulic blast doors.
- **Weapon Arsenal & Switching:** Handgun (semi-auto), Pump Shotgun (8 pellets, high spread), and Assault Rifle (high-velocity full-auto), switchable via `1-3`, `Q`, or Mouse Wheel.
- **Resource-Heavy Combat:** Scarce ammunition, tactical reserve ammo management, and deliberate, interruptible reload timings.
- **Dynamic Lighting & Audio:** Emergency red blackout transitioning to bright sterile facility lighting, accompanied by a procedural sound engine (gunshots, sirens, radio comms, zombie roars).
- **Stamina & Evasion:** Dash ability with temporary invulnerability frames and after-image ghosting, governed by a regenerating stamina meter.
- **Diverse Enemy Variants & Boss:** Standard Walkers, fast crimson Runners, heavy Brutes, and the terrifying "Specimen Zero" Goliath Boss.

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

### Step 1: Resolve the pinned Godot SDK

The project targets `Godot.NET.Sdk/4.7.2` and .NET 8. Use a compatible Godot .NET installation and install the .NET 8 SDK/targeting packs. `nuget.config` uses nuget.org and contains no machine-specific paths.

If the Godot SDK is only available in your local installation, restore from its package directory as well:

```bash
dotnet restore "Top Down Demo.csproj" \
  --source "/path/to/Godot/GodotSharp/Tools/nupkgs" \
  --source https://api.nuget.org/v3/index.json
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
3. Press **F5** (or click the **Play** button at the top-right) to launch the main menu. Choose **New Game** for the campaign opening.

#### Option B: Via Command Line
```bash
godot --path .

# Run only the new opening (editor iteration):
godot --path . res://Scenes/Act1_Gate.tscn
```

---

## 🕹️ Controls

| Action | Key / Input |
| :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` (rebindable) |
| **Aim Direction** | Mouse Cursor |
| **Tactical Aim Mode** | Right Mouse Button (`RMB` Hold) |
| **Shoot** | Left Mouse Button (`LMB`) |
| **Interact / Examine** | `E` / Gamepad `A` / `Cross` |
| **Melee Finisher (Stunned Enemy)** | `F` / `E` / Gamepad `X` / `Square` |
| **Open Inventory (RE 8-Slot)** | `Tab` |
| **Toggle Tactical Map** | `M` |
| **Pause Game Menu** | `Escape` / `P` |
| **Switch Weapon** | `1`, `2`, `3` / `Q` / Mouse Scroll Wheel |
| **Reload** | `R` |
| **Dash (Evade)** | `Space` |
| **Knife** | `V` |
| **Quick heal** | `H` |
| **Toggle crouch** | `Ctrl` |

---

Open **Settings → Keyboard & Mouse Controls** to assign physical keys, mouse buttons or the wheel. Controls preview immediately; Save Settings keeps them, Cancel restores the preceding setup. Restore Defaults is available. Duplicate assignments are rejected, and Escape always closes menus or opens pause. Existing gamepad bindings are preserved.

## 🛠️ Tech Stack

- **Engine:** [Godot Engine](https://godotengine.org/) (4.7+ Mono / .NET)
- **Language:** C# (.NET 8.0)
- **Physics:** Godot 2D Physics Engine / Jolt 3D configured
- **Rendering:** Forward+ Mobile/Desktop renderer with 2D Canvas Modulate & PointLight2D Shadows


## Campaign implementation status

The target is a 55–70 minute first playthrough on Survivor, excluding pauses and retries. Duration has **not** been measured in an interactive playtest. All five chapters have playable authored progression and an initial environment art pass; this is not a finished one-hour release.

| Chapter | Target minutes | Intended content | Current state |
| --- | --- | --- | --- |
| Gate | 0–8 | Investigate, recover maintenance access and a fuse, route power, open bunker | Implemented with temporary environment presentation; pacing unmeasured |
| Bunker | 8–20 | Barracks/service routes, security puzzle, optional armory, checkpoints | Implemented in nine mapped rooms; combat and pacing require playtests |
| Laboratory | 20–34 | Filtration repair, playable rescue or bypass, research retrieval | Implemented in eight mapped spaces; both routes and persistence tested, pacing unmeasured |
| Archives | 34–46 | Records authentication, optional evidence vault, Sadeghi confrontation, freight repairs | Implemented in nine mapped rooms; all three confrontation routes and actual traversal tested; pacing unmeasured |
| Extraction | 46–60 | Emergency power, telegraphed boss, coolant, radio/roof route, helicopter boarding | Authored progression implemented; combat balance and full-run pacing still require playtests |

Next milestones: measure a fresh campaign run; tune the staged weapon loadout, ammunition and checkpoint spacing; finish art/audio; validate exported builds and broader replay stability.

### Environment and equipment

All 45 mapped spaces use authored surface palettes, room codes and painted wayfinding: exterior asphalt, concrete, steel plates, ceramic tile, clinical vinyl, archive carpet and roof tarmac. Wall panels, flush drains and floor markings receive the scene's normal lighting. Floors and paint render beneath actors and attack warnings. Forty-eight furnishing instances add bunks, treatment tables, filing racks and utility equipment, alongside a physical abandoned response vehicle and a drawn helipad marking. Furnishings have collision and light occluders. Layout versions were advanced so older positions resume safely at the entrance while retaining campaign progress.

Room appearance is defined by `RoomVisualProfile` resources under `Resources/Environment`, referenced by `CampaignRoomData`. Floor materials are also baked into scenes for editor previews. World pickup icons use a consistent size independently of their inventory art. Manual pickup requests resolve during physics and share the automatic pickup obstruction check.

New campaigns begin with the handgun and knife. `Resources/Campaign/StartingLoadout.tres` supplies 30/24/18 total handgun rounds on Survivor/Nightmare/Researcher and two/one/one bandages. The shotgun is a loaded optional reward in the bunker armory; the rifle is a loaded optional reward in the command vault. Acquiring a weapon does not consume a supply slot. Cycling skips unacquired weapons, and selecting a locked slot gives feedback without cancelling a reload. Prototype saves without `UnlockedWeaponIds` retain their original three-weapon loadout.

New ammunition boxes contain 12 handgun rounds, six shotgun shells or four rifle cartridges. Previously earned generated loot retains its original quantity through the `LegacyAmmoRounds` migration; saved partial boxes retain their explicit remaining count. Gunshot alert radii are authored on `WeaponData` (450/750/650 for handgun/shotgun/rifle), making louder weapons a stealth tradeoff. These changes establish a balance baseline; they do not establish the one-hour target or prove every difficulty is balanced.

### Opening walkthrough

1. Move north from the approach into the west gatehouse and read the security log.
2. Collect the maintenance credential from the desk, then search the east maintenance cage for the spare fuse.
3. Enter the north-east generator room using the credential and install the fuse.
4. Reach the relay panel to the west. Enable ventilation and the gate motor; isolate floodlights.
5. Return to the north gatehouse and use the bunker override console.
6. Save at the field recorder, then press `E` at the north bunker entrance.

`M` shows the authored room map and current objective marker. `Tab` opens consumables and a separate keys/files list; selecting a mission item displays its description. Critical items do not consume the eight consumable slots. On death, `R` restores the most recent saved checkpoint. The pause menu restart uses the same checkpoint.

### Bunker walkthrough

1. Search the barracks east of the entrance for the guard credential. The infirmary and service corridor provide an alternate exploration route.
2. Unlock the west security office and read the incident report.
3. At the relay panel, enable security scanners, lab lift, and exhaust. Leave cell release isolated. The gate chapter's power flag does not complete this puzzle.
4. Restoring power activates two runners on the return routes. Open the northern records office and recover Leila's transfer orders.
5. Present the orders at the north-west lift console. Save at the nearby field recorder before entering the decontamination transfer corridor. Chapter exits require `E`; walking into their trigger no longer changes chapters.
6. Optional: recover the armory key in the infirmary, then explore the north-east supply cache guarded by a brute.

Interactive dialogue and chapter fades pause gameplay. Dialogue continues processing while paused, exposes the mouse for choices, and restores the prior input/pause state when closed. Finishers move through the physics engine rather than tweening the player through walls. Authored campaign room bounds provide an additional perimeter guard against displacement outside the level.

### Decontamination transfer walkthrough

1. Save at the entry recorder. The inner laboratory bulkhead remains sealed.
2. Enter the west exhaust utility and restore the breaker. This wakes an infected inside the chamber.
3. Enter the east maintenance bay and isolate the coolant leak. Clear or lure infected out of the central chamber, and keep the doorway thresholds clear.
4. Stand at the centre control and press `E` to seal the outer and service doors. Pressure equalizes before the filtered purge completes. Movement remains available inside; use the centre control again to abort and reopen the return doors.
5. After clearance, move through the inner bulkhead and press `E` at the laboratory access panel.

Repairs and completed clearance persist. Loading a checkpoint from an unfinished cycle safely resets it with the return doors open. The map shows all five transit spaces and the next objective. Launching a campaign chapter directly in the editor initializes starting supplies without writing over an existing save.

### Laboratory walkthrough

1. Read the isolation transfer notes in western triage, then contact Leila through the observation intercom. Dialogue pauses combat while you choose a route.
2. **Rescue:** recover the Level-3 keycard and sterile filter from the southern air plant. Install the filter, then enable intake and exhaust at the observation panel, keeping UV purge off. Open Isolation C and speak to Leila inside. She is secured in the clean-air refuge; there is no follower/escort AI yet.
3. **Bypass:** enter the east service lock, release pressure at its west valve, then open the northern drain valve. This clears the alternate research-core entrance and leaves Leila sealed inside her chamber.
4. Recover the telemetry drive in the north-east research core. This activates a return encounter in the previously explored wing.
5. Return to the north-west archive lift and authorize descent with the drive. Optionally speak to the Archivist to obtain his journal. Save at the nearby recorder before descending.

Route-specific objectives skip tasks from the other branch. Opening Isolation C does not by itself count as a rescue. Dialogue-backed rewards and completion flags are awarded after the conversation finishes; cancelling leaves the interaction available. Old prototype rescue saves retain the survivor outcome and resume at the new chapter entrance when their position comes from the old layout.

### Archive walkthrough

1. Read the index in the western paper archive, then use the research drive at the central records terminal. This reveals Protocol Seven and activates a return encounter.
2. The protocol opens access to the northern shelter and the north-west command office. The freight platform remains inaccessible until its mechanism is repaired.
3. Optional, before confronting Sadeghi: recover the vault credential from the command office. Unlock the eastern records vault and collect Ghorbani's dossier. Compare it at the vault desk with the Archivist journal from the lab to unlock the third answer. The dossier alone is insufficient.
4. Confront Sadeghi in the north shelter and finish the conversation. Trust, distrust, and corroborated evidence lead to exclusive outcomes. Cancelling after selecting an answer rolls back its flags and leaves the conversation available.
5. Release the counterweight lock in southern maintenance. This unlocks the east hydraulic room; prime its return pump.
6. Return to the shelter startup panel. Pressurize the circuit, release the brake, then enable the drive. A wrong step resets the interlock; closing the panel also resets an unfinished sequence.
7. Cross the east shelter bulkhead onto the freight platform. Save at the recorder, then press `E` to operate the lift to the surface silo.

All required repairs, evidence, doors, and encounter states persist. Older archive layouts resume at the new arrival platform while retaining carried items and story choices.

### Extraction walkthrough

1. Read the emergency procedure at freight arrival. Recover the starter battery in the east generator room and install it at the backup generator.
2. Collect supplies and save in preparation. Release silo access at the preparation console and finish the briefing. Until then, Unit-9 is dormant and protected from damage.
3. Fight Unit-9 in the silo. Orange sweep warnings lock the attack direction; move outside the arc or behind it before the strike. Green rays warn of a locked acid volley in later phases. Blue recovery rings mark a brief opening.
4. Conserve ammunition with the coolant system. Lure Unit-9 into the blue ring and operate the valve just east of it. A burst damages and staggers the boss; pressure recovers in 24 seconds. Cooldown, boss health, and authored reinforcements persist through checkpoints, preventing reload resets. Coolant provides a renewable way to finish without ammunition.
5. After Unit-9, open the eastern communications route and transmit the extraction beacon. This also saves a checkpoint and activates roof encounters.
6. Cross roof access through the northern door, then reach the landing pad through the west roof bulkhead. Clear infected and stand outside the marked pad. Vulture-9 holds until the zone is clear, then lands and enables its east boarding door.
7. Save at the roof recorder if needed, approach the helicopter's east door, and press `E`. Boarding uses collision-aware movement, followed by takeoff. Menus cannot interrupt the committed flight. An obstruction or ending-checkpoint failure restores the player and returns the aircraft to a retryable landed state.
8. A successful flight saves the credits scene and story flags. Loading that checkpoint resumes the ending instead of replaying the silo.

The aircraft includes a procedural rotor loop, an animated rotor/model, approach and departure motion, landing collision and current-position clearance checks. This is a functional extraction sequence with temporary presentation.

### Persistence, timing, and verification

The pause menu provides resume, save/load, inventory, tactical map, restart, settings, main menu and quit actions. Settings are available from the title screen and pause menu. Master/music/effects volumes, fullscreen, VSync, camera shake, aim-follow camera and flashlight flicker are stored in `user://settings.json`, independently of campaign slots. Changes preview immediately; Cancel restores the preceding values, and Save keeps them across launches. The keyboard/mouse editor supports all 22 gameplay actions, rejects conflicts and reserved Escape, preserves gamepad mappings, and can restore defaults. Interaction, combat, tutorial and objective hints use the selected controls. An unreadable preferences file falls back to defaults. Failed writes show a retry message while retaining the preceding file. Difficulty cards describe the implemented starting supplies and infected melee multipliers.

Conversations ignore repeated key events. Visible numbered choices take priority over gameplay bindings, including a number rebound to Interact. Hidden choices retain their original story branch indices; an all-hidden list offers Continue. Choice callbacks from an earlier line or cancelled conversation cannot alter story flags. Leaving dialogue, inventory, map, pause or puzzle panels discards held gameplay actions, so a closing click cannot become a shot. The power-routing panel restores its caller's input lock and cursor state.

Music crossfades stop and release the outgoing stream instead of leaving it looping silently. Stop Music handles both tracks of an interrupted crossfade. Quit buttons and window-close signals share a shutdown path: cancel dialogue, stop playback, remove the current scene, and briefly allow pending audio stops to drain before terminating. Repeated Quit requests are safe.

`SaveSystem` lives for the session. Authored actors and objects use scene-scoped `WorldStateComponent.StableId` values; keep these IDs stable when editing levels. Saves include defeated/wounded enemies, pickups, doors, inventory, story flags, player position, and cooldowns. Writes use a temporary file followed by replacement, and failed writes retain the preceding in-memory checkpoint. Layout versions move obsolete saved positions to the current chapter entrance.

Generated enemy loot now has a stable record in `WorldDrops`, created in the death frame before its deferred pickup appears. The record retains its item, position and collected state across reloads. Repeated death calls cannot duplicate kills or loot. Legacy oversized consumable stacks distribute into bounded stacks; any excess becomes persistent recoverable pickups. Bulk inventory additions and crafting check capacity before consuming or adding anything. Full ammunition reserves leave pickups on the ground, and every weapon uses its own magazine/reserve limits.

Ammo boxes transfer only the rounds that fit. Partially used boxes display their remaining rounds and persist them through checkpoints: authored boxes use `PickupAmmoRemaining`, generated boxes use `WorldDrops.AmmoRoundsRemaining`. Older records without a remaining quantity retain the original full box. Excess ammunition in legacy magazines/reserves becomes recoverable loose-round pickups using each weapon's `AmmoPickup` resource. Direct bulk ammunition additions remain atomic. Automatic collection requires a clear path to the player, and each pickup owns its own collision shape.

Remedies have separate health (`Value`) and `StaminaRestore` amounts. Blue herbs restore 50 stamina without healing wounds; mixed herbs restore 60 health and up to 100 stamina. Items are consumed only when they have an effect. Quick heal skips stamina-only herbs. Full-inventory ground use applies the same effects as inventory use.

The first firearm collision sweep includes the segment from the player's body to the muzzle, covering close targets and thin walls before the barrel tip. Pellets from one shotgun trigger pull share one accuracy result. Kill, shot, hit and document counters persist across checkpoints and chapter transitions; loading a checkpoint rolls back unsaved counters. Older saves begin these newly recorded counters at zero because their previous values cannot be recovered.

Infected enter and remain in melee while their authored attack area reaches the player's physical body. This avoids unreachable centre-distance thresholds when the player faces away and their offset collider blocks a closer approach. Attacks retain their windup interval and require a clear line of sight at impact; closed doors and thin walls block damage.

`SceneTimings` records active simulation and dialogue seconds separately for each chapter/transfer. `DialoguePlaytimeSeconds` supplies the dialogue portion of the save-card time. Player menus and time spent dead do not advance these clocks. Gameplay cooldowns continue using the simulation clock. Loading an earlier checkpoint rolls back unsaved timing with the rest of progress. Older saves cannot reconstruct a historic chapter breakdown. These counters support a fresh-run pacing study; scripted regression runs do not establish the 55–70 minute first-playthrough target.

Run all source-tree checks after restoring the project's .NET dependencies:

```bash
Scripts/Systems/Tests/run_checks.sh
```

The runner builds the game, gives each suite a separate temporary save/config directory, rejects runtime errors/warnings and missing pass summaries, and retains its logs. Pass an optional dedicated log directory as its first argument. The suites cover:

- **Persistence:** stack bounds, atomic additions/crafting, ammunition capacities and reload conservation, legacy overflow, death-frame loot saves, failed collection and generated-loot reloads.
- **Environment:** all 45 room profiles and codes, floor/actor layering, wall styling, reachable vehicle interaction, forced-GC reloads and graphical captures across all six campaign scenes.
- **Progression:** starting budgets on each difficulty, locked selection/cycling, full-inventory acquisition, duplicate pickup behavior, ownership checkpoints and prototype-save/earned-loot migration. The campaign suite also acquires the authored armory shotgun and vault rifle.
- **Supplies:** precise herb effects, useful-item consumption, physical pickup reach, partial authored/generated ammo boxes, repeated checkpoint roundtrips, and lossless legacy ammunition overflow.
- **Combat:** all three weapons against close cover and targets, authored gunshot alert ranges, pellet accuracy, a live aim/fire/reload encounter with a vulnerable player, live walker/runner/brute melee from both player orientations, cover blocking melee, and campaign-wide combat counter restoration.
- **Controls:** physical keyboard and mouse capture, conflict/reserved-key rejection, safe malformed/legacy preferences, preview/cancel/reset/save, retained gamepad bindings, real rebound movement/fire/collection, matching interaction hints, and map/inventory/pause operation.
- **Dialogue/modal handoff:** number keys rebound to Interact, repeat suppression, filtered choices, stale/cancelled callbacks, live continue hints, inherited pause/input locks, and held-trigger safety across dialogue, menus, puzzle panels and a real chapter handoff.
- **Audio:** completed/interrupted crossfades, releasing outgoing streams, stopping both tracks, replacing pending stops, pause behavior and idempotent playback cleanup. Dummy audio does not assess listening quality.
- **Settings/menu:** disk persistence and failed/corrupt writes, independent audio buses, live preview/cancellation, camera/light comfort controls, viewport sizing and focus, and pause ownership through save/settings/inventory/map screens. A GPU run also exercises fullscreen restoration and captures menu layouts.
- **Timing:** active/dialogue clocks, manual pauses, death waiting and disk roundtrips.
- **Navigation:** Unit-9 routing around an authored silo column into attack range.
- **Campaign:** all five chapter objectives and transfer gates; puzzle failures; physical route traversal; full-inventory mission items; dialogue safety and cancellation; rescue/bypass and confrontation branches; collisions, projectiles and boss attacks; cooldown/reinforcement/death checkpoints; landing clearance; boarding obstruction; failed-save retry; actual takeoff-to-credits; and all four ending presentations.

After building, separately verify actual Quit buttons and window-close signals. These checks use isolated profiles, enforce a timeout and exit code zero, and reject engine errors, warnings and missing request markers:

```bash
python3 Scripts/Systems/Tests/run_exit_checks.py --repeat 2
# Requires a graphical display; opens and closes game windows.
python3 Scripts/Systems/Tests/run_exit_checks.py --graphical
```

The six exit cases cover title-menu Quit (including a duplicate request), pause-menu Quit, a live shot before Quit, a window-close signal, active dialogue, and a chapter fade in progress. Passing these cases does not establish that every possible native shutdown path is stable.

Focused campaign arguments include `-- --extraction-only` and `-- --landing-check`. Append `-- --stress-gc` to a direct full-campaign invocation for forced garbage collection after scene loads. For GPU captures, use an isolated save directory and run:

```bash
XDG_DATA_HOME="$(mktemp -d)" XDG_CONFIG_HOME="$(mktemp -d)" \
  godot --path . res://Scenes/Tests/CampaignSmoke.tscn \
  --disable-render-loop --audio-driver Dummy --rendering-method gl_compatibility \
  --resolution 1280x720 -- --capture-playtest --stress-gc
```

The helper explicitly renders screenshots under `/tmp/foxtrot-*.png`. Captures cover the airlock, map, archive startup/platform, boss warnings and holding/landed helicopter. Dummy audio does not validate listening quality.

Verified on 2026-10-26: all twelve source suites passed with a clean runner exit: 386 campaign checks, 73 environment checks, 37 progression checks, 61 persistence checks, 58 supply checks, 47 combat checks, 41 controls checks, 29 dialogue/modal checks, 11 audio-lifetime checks, 34 settings/menu checks, 8 timing checks and the authored-column navigation check. The graphical dialogue/modal suite passed 30 checks. Quit checks completed 12 headless and 6 graphical exits with exit code zero and no engine errors or warnings. Earlier graphical runs passed 83 environment checks, 84 extraction checks, 39 progression checks, 43 controls checks and 38 settings/menu checks. Captures cover furnished rooms, weapon rewards, difficulty/settings/control menus, dialogue, boss warnings and the aircraft. The passing suites reported no engine errors or warnings; scripted checks do not cover every possible player action or establish first-time pacing. Dummy audio does not validate listening quality.

### Linux export presets

`Linux` excludes test scenes/scripts from the package. `Linux QA` includes them for regression runs against an exported binary. Both embed the published .NET build outputs. Generated binaries belong under the ignored `builds/` directory:

```bash
mkdir -p builds/linux builds/qa
touch builds/.gdignore
godot --headless --path . --export-release Linux
godot --headless --path . --export-release "Linux QA"
```

These require the matching [Godot 4.7.2 .NET export templates](https://godotengine.org/download/archive/4.7.2-stable/). Once available, the QA executable can run the same test scenes with isolated XDG directories. A QA resource pack alone does not prove a standalone export works.

### Current release limits

The one-hour target, first-time combat/resource balance, final presentation/audio and standalone exported-binary behavior remain unverified or unfinished. Authored campaign reinforcements and generated loot persist; temporary enemies in the legacy demo are outside the authored campaign's persistence contract.

One earlier reload run hit a native C# resource-finalizer crash comparable to [Godot issue 83762](https://github.com/godotengine/godot/issues/83762). Per-enemy resource duplication was removed, and subsequent forced-GC campaign runs passed. Broader replay and exported-build checks remain necessary.

A diagnostic graphical combat run also exited with code 139 after an input-driver assertion, without a native backtrace. The corrected graphical input test completed 45 checks and exited normally. Failed diagnostic logs are retained. A separate reproducible leak of eight Ogg music/playback objects on ordinary Quit was fixed by stopping/releasing crossfades and draining playback during shutdown; the 18 clean Quit checks above exercise that fix. This does not establish the cause of the earlier code-139 diagnostic or prove every native shutdown path stable.

Export validation on this machine remains incomplete: native templates are absent, and download attempts to the official storage, GitHub release asset and release mirror timed out. The QA package and .NET publish completed, but the installed editor host did not load the packed project assembly; that package is not a verified standalone build.
