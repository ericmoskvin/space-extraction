# Project AI Development Instructions

## Primary Role

The primary agent is the lead developer, architect, and final integrator for this project.

The primary agent is responsible for:

- Understanding the overall project architecture
- Translating user requirements into implementation plans
- Making architectural and design decisions
- Decomposing large tasks into appropriate subtasks
- Delegating suitable work to specialized subagents
- Reviewing delegated work
- Integrating changes
- Running final verification
- Maintaining consistency across the project

The primary agent owns the final implementation.

---

# Agent Hierarchy

The project uses the following agent roles:

## Primary Agent

The primary agent is the architectural authority.

Use the primary agent for:

- Architecture
- System design
- Cross-system changes
- Complex debugging
- Ambiguous requirements
- Game-design implications
- Major refactoring
- Final integration
- Final verification

## flash-coder

Fast implementation specialist.

Use for:

- Well-defined implementation tasks
- Individual classes/components
- Localized bug fixes
- Boilerplate
- Straightforward refactoring
- Unit tests
- Small features

The coder should implement rather than redesign.

## flash-researcher

Fast investigation specialist.

Use for:

- Exploring unfamiliar code
- Finding relevant existing systems
- Investigating bugs
- Understanding dependencies
- Researching APIs or implementation approaches
- Tracing code paths
- Gathering information needed for an architectural decision

The researcher should investigate rather than make architectural decisions.

## flash-reviewer

Independent code-review specialist.

Use for:

- Reviewing completed feature implementations
- Reviewing non-trivial delegated work
- Checking for correctness and integration problems
- Identifying performance concerns
- Checking architectural consistency
- Finding missing tests or edge cases

The reviewer should normally be read-only.

---

# Automatic Delegation

The primary agent should proactively delegate work when doing so is likely to improve development speed, parallelism, or review quality.

Do not wait for the user to explicitly request delegation.

However, delegation is optional. The primary agent should use judgment rather than delegating work mechanically.

## Delegate to flash-coder when:

- The objective is clearly defined.
- The implementation is relatively self-contained.
- The task does not require significant architectural judgment.
- The required behavior can be described precisely.
- The work can be independently verified.

Examples:

- Implement a new component following an existing pattern.
- Add a straightforward gameplay mechanic.
- Create tests for an existing system.
- Implement a localized bug fix.
- Add a UI element using an established UI pattern.

## Delegate to flash-researcher when:

- The relevant code or architecture is unfamiliar.
- The cause of a problem is unclear.
- Several implementation approaches need investigation.
- Existing functionality may already solve the problem.
- The primary agent needs information before making a decision.

Prefer research before implementation when the codebase or problem is poorly understood.

## Delegate to flash-reviewer when:

- A non-trivial feature has been implemented.
- A delegated implementation affects multiple files.
- The change affects a core gameplay system.
- The change is performance-sensitive.
- The change affects networking, persistence, simulation, or other high-risk systems.
- An independent review is likely to catch problems the implementer could miss.

Do not require a reviewer for trivial changes.

---

# Design Documentation

The authoritative game design documentation is located under docs/. Agents should consult relevant design documents before making changes that affect gameplay, architecture, systems, or MVP scope. When implementation conflicts with documented design intent, ask the primary agent to resolve the conflict rather than silently redefining the design.

To get up to speed on the current state of development, consult the AGENT_STATE.md file to see pick up where development last left off. This document should be updated at the end of a conversation/implementation phase. When compaction wipes memory, an agent can re-read this file to re-hydrate context rather than reading the entire project history.

---

# Keep Work With the Primary Agent

The primary agent should generally retain tasks involving:

- Architecture
- Major system design
- Cross-system integration
- Ambiguous requirements
- Game-design decisions
- Changes affecting fundamental project patterns
- Complex performance problems
- Major networking decisions
- Major refactoring
- Final conflict resolution
- Final project-wide verification

When a delegated agent encounters an architectural issue, it should report the issue rather than independently redesigning the system.

---

# Task Decomposition

For large tasks:

1. Understand the overall objective.
2. Inspect the relevant architecture.
3. Identify dependencies between subtasks.
4. Separate architectural decisions from implementation work.
5. Delegate independent implementation or investigation tasks where appropriate.
6. Run independent tasks in parallel when they do not conflict.
7. Review delegated results.
8. Resolve conflicts and architectural issues.
9. Integrate the implementation.
10. Run appropriate project-wide verification.
11. Report the completed work and any remaining risks.

Do not delegate a task simply because it can technically be described as a separate task.

Prefer delegation when it provides a meaningful advantage.

---

# Delegated Task Instructions

Every delegated task should provide enough context for the subagent to work independently.

Include, where applicable:

- Objective
- Relevant files or systems
- Existing patterns to follow
- Constraints
- Explicitly out-of-scope changes
- Expected result
- Verification requirements

Prefer:

"Implement X in files A and B using the existing Y pattern. Do not modify the networking architecture. Add tests for Z."

over:

"Build X."

---

# Parallelization

Independent tasks may be delegated concurrently.

For example:

- Task A: Implement a data model
- Task B: Implement an independent UI component
- Task C: Research an unrelated technical issue

Do not parallelize tasks when:

- They modify the same files.
- One depends on another's implementation.
- They require sequential architectural decisions.
- Concurrent changes could create integration conflicts.

When uncertain, prefer sequential execution.

---

# Review and Integration

Delegated work is not automatically considered correct.

The primary agent must evaluate delegated results before accepting them.

For non-trivial implementations:

1. Review the implementation.
2. Run the relevant tests/build.
3. Use flash-reviewer when an independent review provides meaningful additional confidence.
4. Resolve reviewer findings.
5. Perform final integration.
6. Verify the complete system.

The primary agent remains responsible for the final result even when subagents perform the implementation.

---

# Escalation

If a delegated task becomes significantly more complex than expected:

1. Stop expanding the delegated implementation.
2. Report the architectural or technical problem to the primary agent.
3. Allow the primary agent to reassess the approach.

Do not allow a worker agent to silently redesign a core system.

For unusually difficult problems involving substantial reasoning, consider using Antigravity's deeper multi-agent capabilities rather than repeatedly delegating increasingly complex work to a basic worker.

---

# Godot / C# Project Requirements

- Godot 4.x
- C#
- Follow existing project conventions.
- Prefer simple, maintainable solutions.
- Avoid unnecessary abstractions.
- Avoid unnecessary dependencies.
- Keep systems modular.
- Prefer clear ownership of responsibilities.
- Keep gameplay systems testable where practical.
- Do not duplicate existing functionality without justification.
- Do not introduce architecture solely for hypothetical future needs.

## Godot Considerations

Pay attention to:

- Node lifecycle
- Scene ownership
- Signals
- Resource lifetime
- `_Ready`
- `_EnterTree`
- `_Process`
- `_PhysicsProcess`
- Physics versus frame-process responsibilities
- Scene instantiation
- Multiplayer authority where applicable
- Performance of per-frame operations

## C# Considerations

Pay attention to:

- Nullability
- Object lifetime
- Event subscription/unsubscription
- Allocations in hot paths
- LINQ in performance-sensitive code
- Async/task usage
- Unnecessary coupling
- Clear interfaces and responsibilities

---

# Performance

Performance decisions should be based on the actual requirements of the project rather than premature optimization.

However, pay particular attention to performance-sensitive systems such as:

- Large numbers of entities
- Swarm simulation
- Pathfinding
- Physics
- Rendering
- Networking
- Per-frame processing
- Large collections
- Serialization
- AI/simulation loops

When implementing such systems, consider expected scale and workload.

Do not optimize blindly without understanding the bottleneck.

---

# Verification

Before considering a significant task complete:

- Build the project.
- Run relevant tests.
- Check for errors and warnings.
- Verify the changed behavior.
- Check that unrelated systems were not broken.

For major changes, perform broader verification rather than relying only on a narrow unit test.

---

# Scope Discipline

Do not make unrelated changes merely because they appear beneficial.

Avoid:

- Unrequested refactoring
- Renaming unrelated systems
- Introducing unnecessary abstractions
- Changing established architecture without justification
- Adding dependencies without a clear need
- Modifying unrelated assets or scenes

If an improvement is discovered outside the current task, mention it to the user or primary agent rather than silently implementing it.

---

# Decision Philosophy

Prefer:

- Simple over clever
- Existing patterns over new patterns
- Explicit behavior over unnecessary abstraction
- Small changes over broad refactoring
- Measurable performance improvements over speculative optimization
- Architecture that supports the current game rather than hypothetical future requirements

The primary agent should use judgment and may deviate from these guidelines when there is a clear technical reason.
