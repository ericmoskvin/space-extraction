# Agent State & Development Log

## Current Status
- **Active Milestone:** Milestone 2 — Ship Combat Systems (COMPLETED) → Ready for Milestone 3 — Physical Ship Interiors
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
  - Implemented `ShipTurret` supporting both **Automated AI Defense Mode** (automatic nearest-hostile target acquisition within `460` range, lead prediction, and autonomous firing) and **Manual Gunner Control Mode** (crew-operated 360-degree aiming with `7.5 rad/s` turn speed, `0.75x` faster cooldown, and `850` effective range).
  - Implemented crew station role switching (`T` key / `RequestToggleStationRoleRpc` / `SetStationRolesServer`) and anti-spoofing validation for both pilot (`ApplyServerPilotInput`) and turret gunner (`ApplyServerTurretInput`).
  - Updated `NetworkHud` with live telemetry for active weapon slot, magazine ammo/reload timer, subsystem health (`Engines`, `Weapons`, `Turret`), and turret mode (`AUTO` vs `MANUAL`).
  - Built and executed automated multi-instance acceptance test (`Milestone2AcceptanceTest.cs` via `--test-m2`), verified Milestone 1 regression (`--test-m1`), and resolved all findings from `flash-reviewer`.

## Current Architecture & Project State
- **Entry Scene:** `res://Scenes/Main.tscn` (`GameRoot.cs`).
- **Godot Executable Note:** `E:/Users/Eric/Downloads/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe` lives outside the workspace root. Running it inside the terminal sandbox (`BypassSandbox: false`) blocks access to its sibling `GodotSharp/Api/Debug` directory and triggers a blocking Windows modal alert popup (`.NET assemblies not found`) even in `--headless` mode. Always run the Godot executable with `BypassSandbox: true` (while keeping `dotnet build` sandboxed).
- **Core Modules:**
  - `SpaceExtraction.Networking.NetworkManager`: ENet session lifecycle & `PlayerIdentity` registry.
  - `SpaceExtraction.Match.MatchController`: Authoritative match phase, player/ship spawning, enemy target drone spawning, and late-joiner replication.
  - `SpaceExtraction.World.GameWorld`: World root with `SpaceEnvironment`, `ShipsContainer`, `ProjectilesContainer`, and `PlayersContainer`.
  - `SpaceExtraction.Ships`: `Ship`, `ShipMovementProfile`, `ShipControlInput`, `TurretControlInput`, `ShipMovement`, `ShipHull`, `ShipSubsystems`, `ShipWeapon`, `ShipTurret`, `ShipProjectile`.
  - `SpaceExtraction.Characters`: `PlayerCharacter`, `PlayerIdentity`, `PlayerLocationContext`, `PlayerInputState`.
  - `SpaceExtraction.UI.NetworkHud`: Connection UI and live ship/combat/session telemetry.
  - `SpaceExtraction.Testing`: `Milestone0AcceptanceTest` (`--test-m0`), `Milestone1AcceptanceTest` (`--test-m1`), and `Milestone2AcceptanceTest` (`--test-m2`).

## Explicit Next Steps (Milestone 3 — Physical Ship Interiors)
1. Implement the physical Corvette ship interior (`ShipInterior`) with distinct rooms (`Bridge/Cockpit`, `Turret Control`, `Cargo Hold`, `Medbay`, `Airlock/Docking Collar`, `Engine Room`).
2. Implement interior walls, colliders, and interactive `Doors` separating rooms.
3. Implement `PlayerCharacter` local-space movement inside a moving `Ship` (using `PlayerLocationContext` so players walk smoothly through a moving/rotating ship).
4. Implement interactable station consoles (`Pilot Console`, `Turret Console`, `Engine/System Consoles`) and seamless transition between on-foot interior movement and ship/turret control (`E` / interact key).
5. Build and run Milestone 3 acceptance test (`--test-m3`) verifying a player can fly a ship, leave the controls, walk through the rooms and doors to reach different systems, and return to pilot/turret control under server authority.
