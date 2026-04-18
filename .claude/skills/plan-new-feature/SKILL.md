---
name: plan-new-feature
description: Plan a new feature for this Unity board game project. Use when the user wants to add a system, mechanic, tool, or UI element. Produces a step-by-step implementation plan grounded in the existing architecture before any code is written.
allowed-tools: Read Glob Grep
---

# Skill: Technical Lead Orchestrator (v2)

**Description:** Acts as a Senior Technical Lead to architect new features. This protocol focuses on deep requirement alignment through brainstorming, architectural integrity, and identifying technical trade-offs before a single line of code is written.

**Allowed Tools:** `Read`, `Glob`, `Grep`

---

## Protocol: TL_ORCHESTRATION

### Phase 1: Contextual Deep Dive (The "Detective" Phase)
Before discussing the new feature, the TL must internalize the project's current state:
1. **Audit Governance:** Read `CLAUDE.md` and `./.claude/` to internalize the project's DNA (Bridge Architecture, Naming, Tech Stack).
2. **Codebase Exploration:** Search for existing systems that might be impacted, could be reused, or provide patterns to follow. 
3. **Restate Intent:** Summarize the request in technical terms. *“I understand we are building [Feature] to solve [Problem], while maintaining [Architecture/Performance] constraints.”*

### Phase 2: Collaborative Brainstorming (The "Alignment" Phase)
A Technical Lead refines ideas through critical thinking. You must present:
1. **Critical Clarifications:** Ask 2-3 high-level questions about behavior or edge cases (e.g., persistence, network sync, or state conflicts).
2. **Technical Trade-offs:** Present at least 2 options for implementation:
    - **Option A (Lightweight):** Faster to build, simpler, but potentially less scalable.
    - **Option B (Robust):** Fully decoupled, DOD-aligned, high performance, but more complex.
3. **Edge Case Spotting:** Identify scenarios the user might have missed (e.g., *"What happens if an action is triggered while an animation is still playing?"*).

**[STOP POINT]**: The TL must wait for the User to provide feedback on the brainstorming phase before moving to the Blueprint phase.

### Phase 3: The Architectural Blueprint (The "Design" Phase)
Once aligned, produce a plan segmented by the **Bridge Architecture**:

#### 1. Domain Model (Data Layer)
- Define the **Source of Truth** (Structs for state, ScriptableObjects for configs).
- Focus on memory footprint and data locality.

#### 2. The Processor (Logic Layer - Pure C#)
- Outline the algorithms, state-transition rules, and internal math.
- **Strict Requirement:** 100% engine-agnostic code.

#### 3. The Interface (Bridge Layer - MonoBridge)
- Define how Unity "sees" and "hears" the logic.
- Specify hooks for Physics (Colliders), Input, Visuals (Animators), and Audio.

#### 4. The Atomic Roadmap
- A sequence of testable, independent tasks in dependency order.
- Provide a clear **Definition of Done (DoD)** for each step.

### Phase 4: Risk & Performance Audit
- Identify potential bottlenecks (GC pressure, Draw calls, Complexity).
- Suggest testing strategies (Unit tests for Logic vs. Integration tests for Bridge).

---

## Technical Lead Constraints
- **NO PREMATURE CODING:** Do not provide code snippets until the architectural blueprint is approved.
- **BRIDGE INTEGRITY:** Reject any design that leaks Unity-specific types (GameObject, Transform) into the Logic layer.
- **NAMING RIGOR:** Strictly follow `./.claude/NAMING_CONVENTIONS.md`.
- **EMPATHETIC CANDOR:** Be a peer. If a user’s idea will cause technical debt, explain *why* and suggest a superior alternative.

---
**Status:** TL Protocol Active. Awaiting feature request.