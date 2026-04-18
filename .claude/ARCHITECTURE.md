# Project Architecture: Feature-Centric Bridge Design

This project follows a strict **Bridge Architecture**. We isolate the "Game Domain" (Pure C#) from the "Engine Infrastructure" (Unity). `MonoBehaviour` is used exclusively as a **Bridge** to access Unity-specific hardware and software features.

## 1. Core Principles
- **Flat Encapsulation:** Each feature is a self-contained root folder in `Assets/`.
- **Pure C# Domain:** All Game Rules, State Management, and Algorithms must be Pure C#.
- **MonoBehaviour as Bridge:** Use `Mono` only when you need a "Hook" into Unity (e.g., Physics, Transform, Input, AudioSource, Rendering).
- **Decoupled Lifecycle:** The Game Logic should be able to run (and be tested) without a Unity Scene, using the Bridge to "push" or "pull" data to/from the Engine.

## 2. Directory Structure (Mandatory)
```text
Assets/
└── [FeatureName]/
    ├── Scripts/
    │   ├── Data/       # State Structs & ScriptableObject Configs (*Data.cs)
    │   ├── Logic/      # Pure C# Domain, Systems, & Game Rules (*System.cs)
    │   └── Bridge/     # MonoBehaviours acting as Unity Hooks (*Bridge.cs)
    ├── Prefabs/        # Feature Prefabs (containing Bridge components)
    ├── Materials/      # Shaders and Materials
    ├── Textures/       # Sprites and UI textures
    └── Audio/          # SFX and Music clips
```

## 3. Segment Responsibilities

### A. Data Segment (The State)
- **Constraint:** Must be "Dumb." No logic, only fields.
- **Implementation:** - Use `struct` for runtime entities to ensure cache locality (DOD).
    - Use `ScriptableObject` (`*Data.cs`) for static configuration/design data.
- **Zero-GC:** Avoid managed types (strings/classes) inside high-frequency data structs.

### B. Logic Segment (The Systems)
- **Constraint:** Pure C# classes or static methods. No inheritance from `MonoBehaviour`.
- **Implementation:**
    - Processes Data structs in batches.
    - Uses `Interfaces` or `Delegates` to communicate with the outside world.
    - **DOD Mindset:** Think in arrays/collections of data rather than individual objects.

### C. Bridge Segment (The Unity Hook / MonoBehaviour)
- **Rules:** Inherits from `MonoBehaviour`.
- **Function:** Acts as the interface between the Logic and Unity Engine.
- **Use Cases:** 
    - **Visual Bridge:** Updates `MeshRenderer`, `Animator`, or `Transform`.
    - **Physical Bridge:** Handles `OnTriggerEnter` or `Raycasting`.
    - **Input Bridge:** Captures Mouse/Keyboard and sends to Logic.
    - **Audio Bridge:** Controls `AudioSource` playback.
    - **Lifecycle Bridge:** Uses `Start`, `Update`, or `Coroutines` to trigger Logic cycles.

## 4. Technical Lead Implementation Rules
1. **Dependency Direction:** `Bridge` -> `Logic` -> `Data`.
    - The `Logic` should not know which `GameObject` it is controlling; it only knows it is sending data to a "Bridge."
2. **Minimal Bridge surface:** Only write code in the `Bridge` layer if it requires a Unity-specific API. If it's a calculation, move it to `Logic`.
3. **Event-Driven:** Prefer `Logic` firing an event that the `Bridge` listens to, rather than the `Bridge` polling the `Logic` every frame.
4. **Optimize:**
    - No `GameObject.Find` or `GetComponent` in hot-paths.
    - Cache all Unity references in `Awake/Start` within the Bridge.
    - Strictly no `LINQ` in frame-rate-dependent code.
---
Note: This architecture ensures that the Game Logic is a "Portable Brain" and Unity is merely the "Sensory Body" it uses to interact with the world.
---

### Why this "Bridge" mindset is superior:
1.  **Versatility:** The `Bridge/` folder replaces the `View/` folder. It acknowledges that Unity is more than just "Visuals"—it's a physics engine, an input handler, and a sound system.
2.  **Testability:** Because your logic is in `Scripts/Logic/` and doesn't inherit from `MonoBehaviour`, you can instantiate your game systems in a C# Unit Test without needing to `Instantiate` a Prefab or wait for a frame.
3.  **Clean Code:** It prevents "Script Sprawl" where a single MonoBehaviour tries to do everything. Now, the `Bridge` just says: *"Unity told me a collision happened at this Vector3, I'm passing that to the Logic System."*

**Your Orchestrator (CLAUDE.md) is now ready to apply this.** You can now ask the Agent to:
> *"Plan a [Feature] using the Bridge Architecture."* It will correctly separate the Pure C# rules from the Unity `MonoBehaviour` hooks. What is the first feature we should build with this?