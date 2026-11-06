# Agent State & Development Log

## Current Status
- **Active Milestone:** Milestone 3 — Physical Ship Interiors (COMPLETED) → Ready for Milestone 4 — Damage Control
- **Last Updated:** 2026-11-06

## Completed Tasks
- [x] Reviewed `docs/GAME_DESIGN.md` (Project Charter & MVP Specification) and `agents.md`.
- [x] Initialized Git repository, configured `SpaceExtraction.csproj` (`Godot.NET.Sdk/4.7.2`, `net8.0`) & `SpaceExtraction.sln`, and linked remote `origin` (`https://github.com/ericmoskvin/space-extraction.git`).
- [x] **Milestone 0 — Project Foundation:**
  - Established modular project structure, ENet server/client session management (`NetworkManager`), match lifecycle (`MatchController`), decoupled `PlayerIdentity`/`PlayerLocationContext`, server-authoritative `PlayerCharacter` movement, `GameWorld`, `NetworkHud`, and `--test-m0` acceptance test.
- [x] **Milestone 1 — Basic Ship Gameplay:**
  - Implemented `ShipMovementProfile` (`Scout`, `Corvette` MVP reference ship, `Freighter`), `ShipControlInput`, `ShipMovement` (2D rotation, thrust, braking, drag, boost, immobilization), `ShipHull` (disabled transition at 0 hull), `ShipWeapon`, `ShipProjectile` (swept segment collision), and server-authoritative `Ship` entity (`--test-m1`).
- [x] **Milestone 2 — Ship Combat Systems:**
  - Implemented 3 distinct weapon types (`Autocannon`, `Railgun`, `EmpCannon`) plus independent `Dorsal Turret` weapon preset in `ShipWeapon`, with magazine ammunition, fire cooldowns, and reload cycles.
  - Implemented `ShipSubsystems` (`Engines`, `Weapons`, `Turret`) with tactile positional hull hit detection (`ResolveSubsystemAtLocalPoint`: Bow `X > +9` -> `Weapons`, Midships `-9 <= X <= +9` -> `Turret`, Stern `X < -9` -> `Engines`), engine efficiency scaling (`EngineEfficiency` reducing thrust/top speed and completely immobilizing ships when engine health reaches `0` even while hull remains intact), and weapon/turret disabling at `0` subsystem health.
  - Implemented `ShipTurret` supporting both **Automated AI Defense Mode** and **Manual Gunner Control Mode** (`--test-m2`).
- [x] **Milestone 3 — Physical Ship Interiors:**
  - Implemented modular interior architecture under `Scripts/Interiors/` (`InteriorRoom`, `InteriorDoor`, `InteriorConsole`, `ShipInterior`).
  - Authored the physical **Corvette Interior Layout** (`CreateCorvetteInterior`) with 5 rooms (`Cockpit`, `CentralHub`, `EngineRoom`, `Cargo`, `Medical`), 4 openable/closable bulkhead doors (`Bridge Door`, `Engine Door`, `Cargo Door`, `Medbay Door`), and 5 interactable station consoles (`Pilot Console`, `Turret Console`, `Engineering Console`, `Cargo Console`, `Medical Console`).
  - Implemented ship-local pedestrian movement and sub-stepped wall/doorway sliding collision resolution (`ShipInterior.ResolveLocalMovement`) so crew members walk smoothly inside moving and rotating ships without physics jitter or door tunneling.
  - Implemented seamless transitions between ship station control (`Pilot` / `Turret Gunner`) and on-foot interior walking via `E` key (`HandlePlayerInteriorInteractServer`), plus bulkhead door toggling via `F` key (`HandleToggleNearestDoorServer`) and dynamic camera zoom (`1.0x` when piloting/gunning, `1.5x` cutaway view when walking inside).
  - Updated `Ship._Draw()` to render the cutaway Corvette interior (floors, bulkheads, open/closed doors, consoles) and `PlayerCharacter._Draw()` (`ZIndex = 2`) to render crew members and station halos inside the ship.
  - Updated `NetworkHud` with live interior status (role, current room, and contextual `[E]`/`[F]` interaction prompts).
  - Built and executed automated multi-instance acceptance test (`Milestone3AcceptanceTest.cs` via `--test-m3`), verified Milestone 2 regression (`--test-m2`), and resolved all findings from `flash-reviewer`.

## Current Architecture & Project State
- **Entry Scene:** `res://Scenes/Main.tscn` (`GameRoot.cs`).
- **Godot Executable Note:** `E:/Users/Eric/Downloads/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe` lives outside the workspace root. Running it inside the terminal sandbox (`BypassSandbox: false`) blocks access to its sibling `GodotSharp/Api/Debug` directory and triggers a blocking Windows modal alert popup (`.NET assemblies not found`) even in `--headless` mode. Always run the Godot executable with `BypassSandbox: true` (while keeping `dotnet build` sandboxed).
- **Core Modules:**
  - `SpaceExtraction.Networking.NetworkManager`: ENet session lifecycle & `PlayerIdentity` registry.
  - `SpaceExtraction.Match.MatchController`: Authoritative match phase, player/ship spawning, enemy target drone spawning, and late-joiner replication.
  - `SpaceExtraction.World.GameWorld`: World root with `SpaceEnvironment`, `ShipsContainer`, `ProjectilesContainer`, and `PlayersContainer`.
  - `SpaceExtraction.Ships`: `Ship`, `ShipMovementProfile`, `ShipControlInput`, `TurretControlInput`, `ShipMovement`, `ShipHull`, `ShipSubsystems`, `ShipWeapon`, `ShipTurret`, `ShipProjectile`.
  - `SpaceExtraction.Interiors`: `ShipInterior`, `InteriorRoom`, `InteriorDoor`, `InteriorConsole`.
  - `SpaceExtraction.Characters`: `PlayerCharacter`, `PlayerIdentity`, `PlayerLocationContext`, `PlayerInputState`.
  - `SpaceExtraction.UI.NetworkHud`: Connection UI and live ship/combat/interior/session telemetry.
  - `SpaceExtraction.Testing`: `Milestone0AcceptanceTest` (`--test-m0`), `Milestone1AcceptanceTest` (`--test-m1`), `Milestone2AcceptanceTest` (`--test-m2`), and `Milestone3AcceptanceTest` (`--test-m3`).

## Explicit Next Steps (Milestone 4 — Damage Control)
1. Implement room-based hazard and fire system (`ShipDamageControl` / room fire states) triggered by combat hits and subsystem damage.
2. Implement controlled, arcade-readable fire behavior (damaging room systems/crew over time, spreading to adjacent rooms if unaddressed, and slowed/contained by closed bulkhead doors).
3. Implement firefighting and repair interactions (extinguishing room fires and repairing damaged/disabled ship subsystems in the corresponding physical rooms with basic repair rate/cooldown constraints).
4. Update interior visuals and HUD alerts so crews can clearly see and respond to room fires and damaged systems (`"Our engine is damaged and on fire; someone needs to fix it"`).
5. Build and run Milestone 4 acceptance test (`--test-m4`) verifying combat damage ignites fire/disables a system, fire spreads or damages over time until extinguished, and crew members physically extinguish the fire and repair the damaged system under server authority.
