# Agent State & Development Log

## Current Status
- **Active Milestone:** Milestone 4 — Damage Control (COMPLETED) → Ready for Milestone 5 — Boarding
- **Last Updated:** 2026-11-06

## Completed Tasks
- [x] Reviewed `docs/GAME_DESIGN.md` (Project Charter & MVP Specification) and `AGENTS.md`.
- [x] Initialized Git repository, configured `SpaceExtraction.csproj` (`Godot.NET.Sdk/4.7.2`, `net8.0`) & `SpaceExtraction.sln`, and linked remote `origin` (`https://github.com/ericmoskvin/space-extraction.git`).
- [x] **Milestone 0 — Project Foundation:**
  - Established modular project structure, ENet server/client session management (`NetworkManager`), match lifecycle (`MatchController`), decoupled `PlayerIdentity`/`PlayerLocationContext`, server-authoritative `PlayerCharacter` movement, `GameWorld`, `NetworkHud`, and `--test-m0` acceptance test.
- [x] **Milestone 1 — Basic Ship Gameplay:**
  - Implemented `ShipMovementProfile` (`Scout`, `Corvette` MVP reference ship, `Freighter`), `ShipControlInput`, `ShipMovement` (2D rotation, thrust, braking, drag, boost, immobilization), `ShipHull` (disabled transition at 0 hull), `ShipWeapon`, `ShipProjectile` (swept segment collision), and server-authoritative `Ship` entity (`--test-m1`).
- [x] **Milestone 2 — Ship Combat Systems:**
  - Implemented 3 distinct weapon types (`Autocannon`, `Railgun`, `EmpCannon`) plus independent `Dorsal Turret` weapon preset in `ShipWeapon`, with magazine ammunition, fire cooldowns, and reload cycles.
  - Implemented `ShipSubsystems` (`Engines`, `Weapons`, `Turret`) with tactile positional hull hit detection (`ResolveSubsystemAtLocalPoint`: Bow `X > +52` -> `Weapons`, Midships `-52 <= X <= +52` -> `Turret`, Stern `X < -52` -> `Engines`), engine efficiency scaling (`EngineEfficiency` reducing thrust/top speed and completely immobilizing ships when engine health reaches `0` even while hull remains intact), and weapon/turret disabling at `0` subsystem health.
  - Implemented `ShipTurret` supporting both **Automated AI Defense Mode** and **Manual Gunner Control Mode** (`--test-m2`).
- [x] **Milestone 3 — Physical Ship Interiors (+ User Refinements):**
  - Implemented modular interior architecture under `Scripts/Interiors/` (`InteriorRoom`, `InteriorDoor`, `InteriorConsole`, `ShipInterior`).
  - Authored the spacious `2x`-scaled **Corvette Interior Layout** (`CreateCorvetteInterior`, `356x240` hull, `104x96` rooms) with 5 rooms (`Cockpit`, `CentralHub`, `EngineRoom`, `Cargo`, `Medical`), 4 openable/closable bulkhead doors (`Bridge Door`, `Engine Door`, `Cargo Door`, `Medbay Door`), and 5 interactable station consoles (`Pilot Console`, `Turret Console`, `Engineering Console`, `Cargo Console`, `Medical Console`).
  - Implemented ship-local pedestrian movement and sub-stepped wall/doorway sliding collision resolution (`ShipInterior.ResolveLocalMovement`).
  - Implemented smooth **Exterior Roof Cover** (`ExteriorRoofAlpha`) that covers the ship interior when piloting/gunning and smoothly fades out (`alpha -> 0`) when walking on foot inside the ship.
  - Aligned weapon and dorsal turret projectile spawns to the exact visual barrel tips (`(198, 0)` and `(28, 0)`), added mouse-wheel camera zoom (`0.35x`–`2.75x`), and added a colored swatch legend in `NetworkHud` (`--test-m3`).
- [x] **Milestone 4 — Damage Control:**
  - Implemented `ShipDamageControl` (`Scripts/Ships/ShipDamageControl.cs`) with server-authoritative compartment fire intensity (`0`–`100%`), progressive fire growth, ongoing fire attrition damage to burning compartments' subsystems (`EngineRoom` -> `Engines`, `Cockpit` -> `Weapons`, `CentralHub` -> `Turret`), and controlled fire spread across open bulkhead doors (strictly contained by closed bulkhead doors).
  - Implemented combat hit fire ignition (`TryIgniteFromCombatHit`) in `Ship.ApplyCombatHitServer`.
  - Implemented server-authoritative firefighting (`ExtinguishRoom` via `E`/`R`) and subsystem repair (`TryRepairRoomSubsystem` via `R` or Engineering/Turret Console `E`), enforcing that active fire in a compartment blocks repairing that compartment's subsystem until extinguished, consuming finite ship repair supplies (`100` max, `10` per repair action), and restoring disabled (`0` HP) subsystems.
  - Added cutaway interior fire visuals (pulsing orange/red room glow, flame tongues, smoke rings, `[FIRE XX%]` labels) and exterior roof emergency fire venting indicators in `Ship._Draw()`, plus repair supply and emergency alerts in `NetworkHud`.
  - Built and executed `Milestone4AcceptanceTest` (`--test-m4`) and resolved all `flash-reviewer` findings.
  - **Combat Balance & Server Configuration Menu:**
    - Tripled default Ship Hull HP (`720 HP` Corvette, `480 HP` Scout, `1440 HP` Freighter) so ships survive long enough for subsystem damage, firefighting, and repairs to matter.
    - Increased default automated turret targeting range (`DefaultAutoAcquireRange = 1100.0f` units, up from `460.0f`, with `1050 u/s` velocity and `1.75s` lifetime) to match the `2x` ship scale.
    - Added authoritative `ServerConfig` (`Scripts/Match/ServerConfig.cs`) replicated via `MatchController.SyncServerConfigRpc` and a pre-host **Server Balance & Rules Settings** menu in `NetworkHud.cs` allowing hosts to configure Corvette Hull HP, Subsystem HP Multiplier, Auto-Turret Range, Weapon Damage Multiplier, Ship Speed Multiplier, Starting Repair Supplies, Fire Hazard Multiplier, Target Drone Turret Active, and Spawn Target Drone.

## User Feel & Tuning Backlog (Deferred to Combat/Flight Polish Pass)
1. **Tactical On-Foot Movement Speed:** Slow down on-foot interior movement speed when tuning boarding combat (Milestone 5+) so ship-interior gunfights and breach maneuvers feel deliberate and tactical.
2. **Gradual Ship Turning / Rotational Inertia:** Make ship turning less snappy and more gradual (introduce angular acceleration/damping similar to linear thrust acceleration) during future flight-feel tuning.
3. **Hit Registration, Impact Visuals & Damage Tuning:** Refine weapon impact feedback, hit visuals, and combat damage numbers in a dedicated feel/polish pass.

## Current Architecture & Project State
- **Entry Scene:** `res://Scenes/Main.tscn` (`GameRoot.cs`).
- **Godot Executable Note:** `E:/Users/Eric/Downloads/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe` lives outside the workspace root. Running it inside the terminal sandbox (`BypassSandbox: false`) blocks access to its sibling `GodotSharp/Api/Debug` directory and triggers a blocking Windows modal alert popup (`.NET assemblies not found`) even in `--headless` mode. Always run the Godot executable with `BypassSandbox: true` (while keeping `dotnet build` sandboxed).
- **Core Modules:**
  - `SpaceExtraction.Networking.NetworkManager`: ENet session lifecycle & `PlayerIdentity` registry.
  - `SpaceExtraction.Match.MatchController`: Authoritative match phase, player/ship spawning, enemy target drone spawning, and late-joiner replication.
  - `SpaceExtraction.World.GameWorld`: World root with `SpaceEnvironment`, `ShipsContainer`, `ProjectilesContainer`, and `PlayersContainer`.
  - `SpaceExtraction.Ships`: `Ship`, `ShipMovementProfile`, `ShipControlInput`, `TurretControlInput`, `ShipMovement`, `ShipHull`, `ShipSubsystems`, `ShipDamageControl`, `ShipWeapon`, `ShipTurret`, `ShipProjectile`.
  - `SpaceExtraction.Interiors`: `ShipInterior`, `InteriorRoom`, `InteriorDoor`, `InteriorConsole`.
  - `SpaceExtraction.Characters`: `PlayerCharacter`, `PlayerIdentity`, `PlayerLocationContext`, `PlayerInputState`.
  - `SpaceExtraction.UI.NetworkHud`: Connection UI, live ship/combat/interior/damage-control telemetry, and room/system color swatch legend.
  - `SpaceExtraction.Testing`: `Milestone0AcceptanceTest` (`--test-m0`), `Milestone1AcceptanceTest` (`--test-m1`), `Milestone2AcceptanceTest` (`--test-m2`), `Milestone3AcceptanceTest` (`--test-m3`), and `Milestone4AcceptanceTest` (`--test-m4`).

## Explicit Next Steps (Milestone 5 — Boarding)
1. Implement EVA / ship entry-exit transitions (airlock/boarding hatch entry and exit between `OnFootInShip` and `SpaceEva`, plus docking/boarding entry onto disabled or proximate enemy ships).
2. Implement personal on-foot combat weapons, aiming/firing inside ships, and player health/damage.
3. Implement defender vs attacker boarding interactions (bulkhead doors as tactical choke points, interior line-of-sight/cover, crew kill/downed states, and Medbay revival/respawn rules).
4. Implement ship capture / control takeover or sabotage interactions inside boarded ships.
5. Build and run Milestone 5 acceptance test (`--test-m5`) verifying players can exit/board another ship, fight inside its interior, and contest ship control under server authority.
