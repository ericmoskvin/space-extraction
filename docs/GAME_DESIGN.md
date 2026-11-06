2D Space Extraction
Project Charter & MVP Development Specification
Document Purpose: Human design reference + AI coding-agent reference
Technology Target: Godot 4.x / C#
Initial Multiplayer Target: 4–8 players
Long-Term Multiplayer Target: 8–32 players
Genre: 2D multiplayer space combat / boarding / extraction game

1. Executive Summary
2D Space Extraction is a multiplayer top-down space game centered around crews operating physical ships in a dangerous, persistent-feeling space environment.
Players enter a match aboard a persistent "home" ship, explore a space region, salvage cargo, fight NPCs and other players, board and capture ships, repair damaged vessels, and attempt to extract with their current ship and accumulated rewards.
The defining feature is that ship gameplay and character gameplay exist within the same simulation.
A player may:
Pilot a ship.
Operate a turret.
Repair an engine.
Fight a fire.
Walk through the ship interior.
Board another vessel.
Fight enemy crew.
Attempt to capture the bridge.
Abandon their original ship.
Take control of a captured ship.
Return to their crew's ship.
Recover a dead teammate.
Defend their ship from boarders.
The game should produce emergent situations in which players are forced to make decisions about risk, mobility, crew allocation, ship integrity, cargo, and whether to fight or escape.
The game is inspired by the systemic qualities of:
Heat Signature — seamless relationship between space, ships, interiors, and boarding.
Wildgate — ship combat, movement, boarding, and stealing.
Barotrauma — cooperative operation of a physical vessel and responding to ship problems.
Star Citizen — ships as physical spaces and meaningful vehicles rather than abstract loadouts.
The game is not intended to be a hardcore extraction game in which individual death means sitting out the match.
The intended experience is:
Meaningful stakes without excessive punishment.
Players can lose ships, cargo, and the immediate progress of a run, but individual players should generally have opportunities to return to the action. A full team wipe is the primary condition for losing a match.

2. Design North Star
The game should make players say:
"We weren't supposed to be able to do that."
The most memorable moments should emerge from interactions between simple systems.
Examples:
Capturing an enemy ship while its crew is repairing yours.
Fighting a fire while an enemy boards the engine room.
Abandoning a damaged ship and escaping aboard a vessel you just captured.
Recovering a dead teammate aboard a stolen enemy ship.
Loading valuable cargo onto a ship while another crew attempts to intercept you.
Choosing to remain in dangerous space because the cargo is worth the risk.
Successfully charging the jump drive while being pursued by a faster ship.
The game should prioritize emergent stories over scripted scenarios.

3. Core Design Pillars
3.1 The Ship Is a Major Character
The ship is not a menu, loadout, or simple vehicle.
It is a physical object with:
A recognizable exterior.
An interior.
Systems.
Weapons.
Cargo.
Crew.
Damage.
Repairs.
Medical facilities.
Tactical strengths and weaknesses.
Players should develop an attachment to the ship they are operating.

3.2 Ship Combat and On-Foot Combat Are Equal Pillars
Boarding is important, but it must not reduce space combat to a delivery mechanism for boarding.
The game should support encounters resolved through:
Pure ship combat.
Evasion.
Long-range engagements.
Ship disabling.
Boarding.
Counter-boarding.
Mixed ship/crew combat.
Capturing rather than destroying.
The ship should be at least as important as the individual character.

3.3 Crews Operate Ships Together
Players do not have rigid classes.
Instead, they dynamically assume responsibilities:
Pilot
Gunner
Turret operator
Engineer
Damage-control crew
Boarder
Defender
Cargo operator
Roles can change from moment to moment.
A player operating a turret may leave it to fight boarders.
A pilot may leave the bridge to repair an engine.
A boarder may be ordered back because the home ship has caught fire.
This dynamic allocation of player attention is a core cooperative mechanic.

3.4 Ships Can Be Taken
Ships are valuable because they can change hands.
Players can:
Damage ships.
Disable ships.
Board ships.
Defend ships.
Capture ships.
Steal ships.
Recover ships.
Destroy ships.
Capturing an intact ship should generally be more valuable than destroying it, but substantially more difficult.

3.5 Meaningful Stakes Without Hardcore Frustration
The game should create tension through:
Ship loss.
Cargo loss.
Damage.
Risky extraction.
PvP.
Dangerous POIs.
Time pressure.
Limited recovery resources.
It should avoid excessive punishment through:
Individual permanent death.
Long spectator periods.
Losing all persistent progression.
Unrecoverable character progression.
Team wipe is the primary match-ending failure state.

4. Persistent Ship and Active Ship
This is a foundational long-term system.
4.1 Persistent Ship
Every player/team has a persistent ship that acts as their reliable baseline.
It is always available when entering a match.
The persistent ship represents the team's durable progression.

4.2 Active Ship
The ship currently being used by the team.
Normally:
Persistent Ship → Active Ship
However, a team can capture another ship.
If they successfully extract with it:
Captured Ship → Active Ship
The captured vessel can then be used in subsequent matches.

4.3 Ship Loss
If the active captured ship is:
Destroyed
Permanently lost
Successfully stolen by another team
the player/team reverts to their persistent ship for the next match.
Conceptually:
Persistent Ship
      │
      ▼
  Enter Match
      │
      ▼
 Active Ship
      │
      │ capture + extract
      ▼
Captured Active Ship
      │
      ├── survives ──→ next match
      │
      └── lost ──────→ Persistent Ship
This creates meaningful attachment to captured ships without making ship loss equivalent to permanent account progression loss.

5. Match Structure
A match should initially target approximately:
15–30 minutes
with significant variation caused by player behavior.
Basic structure:
Enter Match
     ↓
Explore
     ↓
Find opportunities
     ↓
Salvage / fight / board / capture
     ↓
Decide whether to continue deeper
     ↓
Travel toward extraction
     ↓
Reach viable jump region
     ↓
Charge jump drive
     ↓
Extract
     ↓
Persist ship + rewards
There should be no requirement that players follow a fixed mission sequence.

6. Space Environment
The play area is a 2D space region with a deliberate risk/reward gradient.
6.1 Outer Region
Characteristics:
Lower loot density.
Lower encounter density.
Easier extraction.
More room for maneuver.
Safer for damaged ships.
Purpose:
Cash-out / escape space.

6.2 Mid Region
Characteristics:
Moderate loot.
Moderate NPC activity.
Increased PvP opportunities.
More points of interest.
Purpose:
Primary operating area.

6.3 Inner / Deep Region
Characteristics:
Highest-value loot.
Dangerous NPCs.
Greater likelihood of player encounters.
More valuable ships.
Greater environmental danger.
Purpose:
High-risk / high-reward gameplay.
The game should encourage players to ask:
"How much longer do we stay?"
rather than forcing players toward objectives.

7. Extraction
Extraction should not primarily use a conventional "stand in this circle for 30 seconds" mechanic.
Instead, ships eventually need to escape the active combat region and initiate a jump.
Jump Extraction
A ship must:
Reach an appropriate outer-space region.
Satisfy jump-drive requirements.
Initiate a jump.
Survive the charging period.
Complete the jump.
The jump process is interruptible.
Possible interruptions include:
Leaving the required area.
Ship becoming disabled.
Critical system damage.
Enemy intervention.

7.1 Extraction Philosophy
Extraction should create tension through pursuit, not arbitrary waiting.
A pursuing enemy should be able to catch a fleeing ship under the right circumstances.
However, escape should be possible through:
Speed.
Maneuvering.
Terrain.
Temporary boosts.
Combat.
Disabling pursuers.
Clever routing.
The objective is:
Pursuit should be possible but not inevitable.

8. Ship Movement and Pursuit
The MVP should use a relatively simple ship movement model.
Ships have:
Acceleration
Maximum velocity
Rotation/turning
Braking/deceleration
Temporary boost
Different ship classes should have meaningfully different movement profiles.
For example:
Scout
Fast acceleration, high maneuverability, weak durability.
Corvette
Balanced.
Freighter
Slow acceleration, poor maneuverability, high durability/cargo.
The exact values should be tuned through playtesting rather than predetermined.

9. Ship Classes
The MVP should include approximately three ship archetypes.
Scout
Small, fast, low-capacity.
Useful for:
Scouting.
Solo play.
Evasion.
Opportunistic boarding.
Rapid extraction.

Corvette
Medium general-purpose ship.
Useful for:
Small crews.
Combat.
Boarding.
General exploration.
This should be the primary reference ship for MVP development.

Freighter
Large, slow, cargo-oriented.
Useful for:
Carrying large amounts of loot.
Sustained operations.
Acting as a valuable capture target.
The Freighter should demonstrate the game's "steal something much better than you brought in" fantasy.

10. Ship Interior Architecture
The game should use a semi-modular architecture.
Ships should not feel like collections of arbitrary rectangular rooms.
Instead:
Layer 1 — Authored Ship Design
Each ship has a deliberately designed:
Exterior silhouette.
Overall internal layout.
Bridge location.
Engine arrangement.
Cargo configuration.
Turret positions.
Major room relationships.
Layer 2 — Reusable Interior Modules
Individual rooms and components can use reusable modules:
Corridors.
Doors.
Engineering rooms.
Cargo rooms.
Medical rooms.
Crew rooms.
Utility rooms.
Bridge components.
Consoles.
This creates development efficiency without requiring ships to visually resemble connected blocks.

MVP Rule
Do not build a procedural ship generator initially.
Create a small number of hand-designed ships using reusable room/component technology.
The modular system exists primarily to accelerate development and maintain gameplay consistency.

11. Ship Systems
Ships should contain a limited set of meaningful systems.
Hull
Overall structural integrity.

Engines
Determine movement performance.
Damage may cause:
Reduced acceleration.
Reduced top speed.
Complete immobilization.

Weapons
Ship weapons inflict damage or special effects.

Turrets
Turrets are independent ship systems.
They can be:
Automated.
Manually operated.
Damaged.
Disabled.
Repaired.
Manual operation should provide a significant tactical advantage over automation.
This allows small crews to operate larger ships without requiring every turret to have a dedicated player.

Power
A simplified resource used by important ship systems.
Power should create meaningful tradeoffs without becoming a detailed engineering simulator.

Medical
Supports player recovery/revival.

Cargo
Stores physicalized cargo.

Command/Bridge
Controls ownership/capture.

12. Ship Combat
Ship combat should be:
Simple to understand, but tactically varied.
The MVP should avoid a huge weapon catalog.
Instead, implement a small set of weapons with distinct roles.
Potential categories:
Weapon
Role
Autocannon
Reliable general damage
Railgun
Long-range precision / subsystem pressure
Missile
Burst damage / tracking
Flak
Anti-small target / boarding defense
Beam
Sustained damage
EMP
System disabling

The final MVP selection should be approximately 3–5 weapons, not all of the above.
The important design goal is:
Different weapons should create different decisions rather than simply different damage numbers.

13. Damage and Ship Emergencies
Damage should create problems, not merely reduce a health bar.
Examples:
Engine damage
Reduces mobility.
Weapon damage
Disables a weapon or turret.
Power damage
Reduces system availability.
Hull damage
Reduces structural integrity.
Fire
Creates an active onboard emergency.

14. Firefighting and Damage Control
Fire is a deliberate source of friction.
A fire can:
Damage a room/system.
Obscure visibility.
Force players to divert attention.
Spread if not addressed.
Make movement through a ship more dangerous.
Players can respond by:
Extinguishing the fire.
Repairing the damaged system.
Isolating the affected area.
Abandoning the room temporarily.
The system should remain readable and arcade-like rather than simulating detailed fire physics.

15. Boarding
Boarding is a major gameplay system, but not the entire game.
Three long-term boarding methods are envisioned:
1. Docking Collar
Reliable, deliberate boarding method.
Primary MVP method.
2. EVA
Players leave through an airlock and physically traverse space.
High risk / high flexibility.
3. Boarding Pod
A dedicated small craft used to transport boarders.
Potential future system.

MVP Boarding
Implement:
Docking-based boarding.
A ship can board another vessel when appropriate conditions are met, especially after disabling it.
EVA and boarding pods should be architecturally possible but can be deferred.

16. On-Foot Combat
On-foot combat is deliberately simple.
Players have:
Health.
Movement.
Primary weapon.
Optional secondary/melee capability.
Interaction.
The MVP does not require:
Classes.
Skill trees.
Complex armor.
Large weapon arsenals.
Advanced character progression.
The purpose is to make boarding and defending ships fun, not to create a standalone FPS.

17. Ship Capture
Capturing a ship requires more than reaching the bridge.
A player must initiate a Command Override.
Command Override
At the bridge/command console:
Player activates command override.
Capture timer begins.
Player must remain within the command area.
Player can still move and fight.
Leaving the area interrupts the process.
Completing the channel transfers control.
The player is therefore vulnerable while capturing.

Example
COMMAND OVERRIDE
████████████░░░░ 72%

Remaining: 4.2 sec

Player can:
- Move
- Shoot
- Defend themselves
- Fight nearby enemies

Leaving command radius:
CANCEL OVERRIDE
This creates a natural climax to boarding.

18. Ship Defense
Capturing a ship should require more than killing the crew.
Defenders can:
Retake the bridge.
Interrupt command override.
Attack boarders.
Disable critical ship systems.
Force attackers to retreat.
Recover their ship.
This creates an internal battle over the vessel rather than an instant ownership flip.

19. Medical and Player Recovery
Individual player death should not normally remove the player from the match.
Downed / Dead State
A player can become incapacitated.
A surviving teammate can recover them.

Medical Bay
Ships can contain a medical facility.
The medical facility functions as a recovery/respawn point for the crew.
Potential long-term limitations include:
Finite medical resources.
Recovery cooldown.
Medical bay damage.
Medical bay power requirements.
The MVP should use a simple finite or cooldown-based recovery system, with exact tuning deferred to playtesting.

Cross-Ship Recovery
A team's players should not necessarily become permanently stranded if their original ship is lost.
If the team captures another vessel:
The captured ship's medical facility can become the team's new recovery point.
This reinforces the principle:
The crew persists; the ship is replaceable.

20. Team Wipe
The match should only be considered definitively lost when the entire team is eliminated or otherwise unable to recover.
This is intentionally more forgiving than hardcore extraction games.
The goal is:
Tension without excessive spectator time.

21. Cargo
Cargo should be partially physicalized.
Cargo is not merely a number in an inventory.
Cargo may exist as physical containers aboard a ship.
Players can:
Load cargo.
Unload cargo.
Move cargo.
Steal cargo.
Potentially manipulate cargo with tractor systems.

22. Cargo Weight
Cargo should have a physical/economic consequence.
Carrying large amounts of cargo can slightly affect:
Acceleration.
Maximum speed.
Maneuverability.
Possibly fuel/jump efficiency.
The effect should be noticeable but not so strong that full cargo makes a ship unusable.
This creates an important decision:
"Is this cargo worth being slower?"

23. Internal vs. External Cargo
Long-term ship design can support different cargo philosophies.
Internal Cargo
Advantages:
Protected.
Difficult for enemies to access.
Less exposed to weapons.
Disadvantages:
Takes interior volume.
Requires boarding/entry to access.
Slower to load.
External Cargo
Advantages:
Easy to access.
Fast loading/unloading.
Does not consume as much interior volume.
Disadvantages:
Exposed.
Can be damaged.
Can potentially be detached or stolen.
This should become a meaningful ship-design tradeoff.

24. Inventory
The player inventory should remain simple.
Avoid a complex grid inventory during MVP.
Use:
Personal slots.
Simple item counts.
Ship cargo.
Physical cargo objects where appropriate.
The game should emphasize physical ship interaction over inventory management.

25. NPCs
The MVP needs only a small number of NPC archetypes.
NPC Crew
Capabilities:
Patrol.
Investigate.
Attack.
Defend.
Retreat.
Protect important ship systems.
NPC Ships
Capabilities:
Travel.
Patrol.
Attack.
Flee.
Become disabled.
Be boarded/captured.

26. Derelict Ships
Derelicts are an important source of ships and loot.
A derelict may contain:
Cargo.
Damaged systems.
NPCs.
Valuable equipment.
A potentially capturable ship.
This gives players a way to acquire new ships without necessarily winning a PvP fight.

27. PvP Ships
Player ships should be capturable under appropriate circumstances.
A successful capture requires:
Neutralizing/overwhelming the ship.
Boarding.
Fighting the crew.
Reaching the command center.
Completing Command Override.
Successfully defending the capture.
The captured ship remains in the world as a physical object.

28. Persistent Progression
The long-term progression system should build around the player's persistent ship and accumulated resources.
Potential progression categories:
Persistent ship upgrades.
New persistent ship types.
Ship modules.
Weapons.
Defensive systems.
Medical improvements.
Cargo improvements.
Cosmetic customization.
However:
The persistent ship should not make captured ships irrelevant.
A captured vessel should frequently provide capabilities the player cannot normally access with their baseline ship.

29. Match Economy
The MVP only requires a placeholder economy.
Persistent rewards can include:
Currency.
Salvage.
Captured ship value.
Extracted cargo.
The complete economy should be designed only after the core gameplay loop is validated.

30. Multiplayer Architecture
The game should use an authoritative server model.
The server owns authoritative state for:
Players.
Ships.
Ship movement.
Damage.
Weapons.
Turrets.
Doors.
Fire.
Cargo.
Loot.
NPCs.
Ship ownership.
Capture progress.
Extraction.
Clients provide input and receive replicated state.
Important actions should be server validated.

31. Networking Design Principle
The game should be designed around the assumption that:
A ship is a networked container for multiple networked players and systems.
This should influence architecture from the beginning.
Avoid tightly coupling:
Player → Ship
Instead use:
World
 ├── Ship
 │    ├── Ship Systems
 │    ├── Interior
 │    ├── Cargo
 │    └── Occupants
 │
 └── Player
      └── Current Location / Occupancy
A player can therefore move between ships without fundamentally changing their identity or requiring a new player object.

32. Suggested Godot Conceptual Architecture
Exact implementation should be determined by the coding agent after inspecting the current project, but the conceptual separation should resemble:
Game
├── Match
├── World
│   ├── Space
│   ├── POIs
│   ├── Asteroids
│   └── Extraction Region
│
├── Ships
│   ├── Ship
│   ├── ShipMovement
│   ├── ShipSystems
│   ├── ShipWeapons
│   ├── Turrets
│   ├── DamageControl
│   ├── Cargo
│   └── Boarding
│
├── Interiors
│   ├── Rooms
│   ├── Doors
│   ├── Consoles
│   └── ShipStations
│
├── Characters
│   ├── Player
│   ├── NPC
│   └── Combat
│
├── Progression
│   ├── PersistentProfile
│   ├── PersistentShip
│   └── Rewards
│
└── Networking
    ├── Server
    ├── Replication
    └── Authority
This is a conceptual architecture, not a requirement to reproduce these exact class names.

33. Agent-Assisted Development Principles
AI coding agents should treat this document as a design authority, but not blindly implement every described long-term feature.
The agent should distinguish between:
MVP Requirements
Must exist for the current milestone.
Architectural Requirements
Should influence implementation even if the full feature is deferred.
Future Systems
Should not be implemented unless explicitly promoted into the current milestone.

33.1 Avoid Premature Systems
Do not build:
Full economy.
Full procedural ship generation.
Complex insurance.
Large weapon catalogs.
Advanced NPC factions.
32-player optimization.
before the core loop is playable.

33.2 Preserve Future Extensibility
When implementing an MVP system, avoid architectural decisions that make the following difficult:
Multiple ship classes.
Ship capture.
Players changing ships.
Turrets.
EVA.
Boarding pods.
Physical cargo.
Persistent captured ships.
Larger multiplayer matches.
The goal is simple now, extensible later, not speculative overengineering.

34. Development Milestones
Milestone 0 — Project Foundation
Goal
Establish a clean multiplayer Godot C# project.
Implement:
Project structure.
Server/client architecture.
Basic match lifecycle.
Player identity.
Networked player movement.
Basic world scene.
Acceptance Criteria
Multiple clients connect.
Players see each other.
Server owns authoritative player state.
Players can move reliably.
No major architectural dependency on a single-player simulation.

Milestone 1 — Basic Ship Gameplay
Goal
Make a ship fun to control.
Implement:
Ship movement.
Rotation.
Acceleration.
Braking.
Boost.
Basic hull.
Basic ship weapon.
Basic enemy target.
Acceptance Criteria
Players can:
Fly ships.
Maneuver around one another.
Shoot.
Damage another ship.
Disable another ship.
No interiors yet.

Milestone 2 — Ship Combat Systems
Goal
Establish meaningful ship combat.
Implement:
2–3 weapon types.
Weapon cooldown/ammunition.
Subsystem damage.
Engine damage.
Basic turret.
Automated turret behavior.
Manual turret control.
Acceptance Criteria
Players can make meaningful choices between:
Different weapon types.
Piloting.
Manual turret operation.
Automated defenses.
A small crew should be viable.

Milestone 3 — Physical Ship Interiors
Goal
Make ships actual spaces players inhabit.
Implement:
Ship interior.
Character movement.
Cockpit.
Engine room.
Cargo room.
Medical room.
Doors.
Interactable consoles.
Transition between ship control and interior movement.
Acceptance Criteria
A player can:
Fly a ship.
Leave the controls.
Walk through the ship.
Reach different systems.
Return to control.
The ship should already begin feeling like a place rather than a vehicle.

Milestone 4 — Damage Control
Goal
Make ship damage create gameplay.
Implement:
System damage.
Disabled components.
Fire.
Fire spread in a controlled/simple manner.
Firefighting.
Repair interactions.
Basic repair limitations.
Acceptance Criteria
A combat encounter can produce:
"Our engine is damaged and on fire; someone needs to fix it."
The problem must require player attention rather than being an automatic health regeneration event.

Milestone 5 — Boarding
Goal
Establish the signature ship/interior transition.
Implement:
Docking.
Docked ships.
Interior connection.
Player movement between ships.
Boarding.
Basic on-foot weapons.
Basic enemy/player combat.
Acceptance Criteria
Players can:
Disable another ship.
Dock with it.
Leave their ship.
Enter the enemy ship.
Fight inside it.
Return to their ship.

Milestone 6 — Ship Capture
Goal
Prove the central "steal the ship" fantasy.
Implement:
Bridge/command console.
Command Override.
Capture timer.
Capture interruption.
Ship ownership transfer.
New crew control.
Former owner control consequences.
Acceptance Criteria
A player can:
Disable a ship → board it → fight its crew → reach the bridge → initiate override → survive the channel → take control.
The capture should feel dangerous and earned.

Milestone 7 — Medical / Crew Recovery
Goal
Prevent individual deaths from unnecessarily ending participation.
Implement:
Player down/death state.
Teammate recovery.
Medical bay.
Recovery timer/cooldown.
Ship-based recovery.
Team wipe detection.
Acceptance Criteria
A player can die during a boarding action and later return to the match if their team survives.

Milestone 8 — Cargo and Salvage
Goal
Introduce the economic reason to explore.
Implement:
Loot.
Cargo.
Physical cargo containers.
Simple cargo storage.
Basic cargo weight effect.
Basic cargo loading/unloading.
Simple inventory.
Tractor-beam interaction may be introduced here if technically appropriate.
Acceptance Criteria
Players can:
Find cargo.
Load it.
Carry it.
Feel its effect on ship performance.
Extract with it.

Milestone 9 — Risk-Gradient Space
Goal
Establish the actual match structure.
Implement:
Outer region.
Mid region.
Inner region.
Increasing loot value.
Increasing danger.
POIs.
Derelicts.
NPC ships.
Extraction/jump region.
Acceptance Criteria
Players understand:
Deeper = better rewards + greater danger.
There must be a genuine strategic decision about when to leave.

Milestone 10 — Jump Extraction
Goal
Create the final tension of the match.
Implement:
Jump drive.
Minimum extraction region.
Charge-up sequence.
Interruptions.
Successful extraction.
Failed extraction.
Acceptance Criteria
Players can:
Enter the dangerous region.
Acquire loot/a ship.
Travel outward.
Reach jump range.
Begin charging.
Defend themselves.
Successfully escape.

Milestone 11 — Persistent Active Ship
Goal
Introduce the distinctive meta-loop.
Implement:
Persistent ship.
Active ship.
Captured ship persistence.
Extracted ship storage.
Ship loss.
Reversion to persistent ship.
Acceptance Criteria
A player can:
Enter using their persistent ship.
Capture another ship.
Extract with it.
Start the next match using the captured ship.
Lose the captured ship.
Revert to their persistent ship.
This is a major milestone for the game's long-term identity.

Milestone 12 — Vertical Slice
Goal
Produce the first genuinely playable version of the game.
Target:
4 players
Content:
3 ship archetypes.
Multiple ship interiors.
PvP.
NPC ships.
Derelicts.
Ship combat.
Turrets.
Fires/repair.
Boarding.
Ship capture.
Medical recovery.
Cargo.
Risk-gradient map.
Jump extraction.
Persistent active ship.
The vertical slice should be playable repeatedly without developer intervention.

35. MVP Explicitly Out of Scope
Do not implement these as MVP requirements:
32-player matches.
Full procedural ship generation.
Large galaxy simulation.
Complex faction systems.
Player stations.
Crafting.
Complex character progression.
Large character weapon arsenal.
Dozens of ship classes.
Capital ships.
EVA boarding as a fully developed system.
Boarding pods.
Complex insurance systems.
Sophisticated economy.
Trading.
Detailed power-grid simulation.
Fully physically simulated fires.
Full atmospheric flight.
Planet surfaces.
Voice chat.
Extensive cosmetics.
Competitive ranked matchmaking.
These may become future features.

36. Technical Scope Philosophy
The MVP should favor:
Systemic interactions over content quantity.
Three ships with strong interactions are preferable to twenty ships with shallow mechanics.
Three weapon types with meaningful tactical differences are preferable to twenty interchangeable weapons.
Five interesting rooms are preferable to fifty decorative rooms.
A small number of systems should interact in surprising ways.

37. Prototype Quality Standard
Early milestones should use placeholder art where appropriate.
The following are acceptable during prototyping:
Geometric ships.
Primitive room geometry.
Placeholder characters.
Simple particle effects.
Debug UI.
Temporary sounds.
Do not spend significant development time polishing art before the corresponding gameplay system has been validated.
However, the spatial relationships and readability of the final game should be considered from the beginning.

38. First Playable Test
The earliest meaningful end-to-end prototype should eventually support:
Four players
Each enters aboard a Corvette.
The map contains:
Asteroids.
One derelict.
One NPC ship.
One enemy player crew.
One outer extraction region.
Players can:
Fly.
Fight.
Damage systems.
Board.
Fight on foot.
Capture a ship.
Recover teammates.
Load cargo.
Escape.
Extract.
The prototype does not need progression or polished art.
If this scenario is fun, the project has validated its fundamental premise.

39. Agent Acceptance-Test Philosophy
Every milestone should end with explicit gameplay acceptance tests.
For example:
Ship Capture Test
Given two ships, when Ship A disables Ship B, boards it, eliminates/evades its defenders, initiates Command Override, and maintains control until completion, Ship A's crew should gain control of Ship B.
Ship Loss Test
Given a captured active ship, if the ship is destroyed before extraction, the team should enter the next match using its persistent ship.
Recovery Test
Given a surviving teammate and an incapacitated player, the surviving teammate should be able to recover the player using the ship's medical system.
Extraction Test
Given a ship in the outer extraction region, the crew should be able to initiate and complete a jump unless the process is interrupted.
Tests should be behavioral and observable, not merely code-level.

40. AI Agent Operating Rules
When an AI coding agent is assigned work on this project:
Before implementation
The agent should:
Inspect the existing project structure.
Identify relevant existing systems.
Identify current architectural assumptions.
Check whether the requested feature already partially exists.
Determine the smallest implementation that satisfies the milestone.
Avoid rewriting unrelated systems.
During implementation
The agent should:
Preserve existing functionality.
Keep server authority explicit.
Avoid unnecessary dependencies.
Keep systems modular.
Prefer composition over deep inheritance.
Avoid hard-coding ship-specific behavior into global systems.
Add debug tooling where useful.
Keep multiplayer behavior in mind even when implementing a local feature.
After implementation
The agent should:
Compile/build.
Run available tests.
Check for errors/warnings.
Verify the requested behavior.
Report architectural changes.
Report known limitations.
Identify any technical debt intentionally introduced.

41. Definition of Done
A milestone is not complete merely because the relevant classes exist.
A feature is complete when:
It works in the running game.
It works under multiplayer authority.
It integrates with existing systems.
It can be demonstrated.
It has basic error handling.
It does not break previously completed milestones.
Its behavior matches the design intent.
Any known limitations are documented.

42. Long-Term Vision
The MVP should eventually grow toward a much larger systemic game.
Potential future systems include:
8–32 player matches.
More ship classes.
Larger ships.
Capital ships.
More complex interiors.
Procedural ship generation.
EVA boarding.
Boarding pods.
Advanced ship modules.
More sophisticated cargo.
Factions.
NPC economies.
Missions.
Contracts.
Bounties.
Ship customization.
Insurance/recovery.
Persistent crew progression.
More elaborate medical systems.
More complex environmental hazards.
These systems should be evaluated against the core design pillars rather than assumed to be mandatory.

43. Final Design Statement
The game is fundamentally about crews and ships operating in a dangerous shared space.
The player should care about their ship.
They should know where the engine room is.
They should recognize when a turret is damaged.
They should hear the alarm when someone boards.
They should have to decide whether to leave the cockpit to fight a fire.
They should be excited when they discover a valuable derelict.
They should hesitate before engaging a larger ship.
They should be tempted to capture rather than destroy it.
They should feel vulnerable while overriding its command systems.
They should be able to lose their ship without losing their entire progression.
They should be able to recover teammates.
They should be able to take a stolen ship home.
And, critically, they should have to decide:
"Is what we have now worth risking by going deeper?"
The ultimate game loop is therefore:
              PERSISTENT SHIP
                     │
                     ▼
                 ENTER SPACE
                     │
                     ▼
                 EXPLORE
                     │
          ┌──────────┼──────────┐
          │          │          │
        SALVAGE    FIGHT      BOARD
          │          │          │
          │          │       CAPTURE
          │          │          │
          └──────────┴──────────┘
                     │
              TAKE GREATER RISKS
                     │
                     ▼
               ACQUIRE VALUE
                     │
          ┌──────────┴──────────┐
          │                     │
      EXTRACT NOW          GO DEEPER
          │                     │
          │                     └──────┐
          │                            │
          ▼                            ▼
     CHARGE JUMP                 GREATER REWARD
          │                            │
          └──────────────┬─────────────┘
                         │
                         ▼
                     EXTRACT
                         │
                         ▼
               KEEP SHIP / REWARDS
                         │
                         ▼
                   NEXT MATCH
The game's defining characteristic should be that all of these steps are performed through the same underlying simulation.
A ship isn't a menu.
A boarding action isn't a separate game mode.
An extraction isn't a teleport trigger.
A crew member isn't disconnected from the ship.
Everything is part of the same world.


