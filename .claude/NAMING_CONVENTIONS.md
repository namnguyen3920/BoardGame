# Unity Professional Naming Conventions & Style Guide

## 1. Project Structure & Assets
- **Folders:** `PascalCase`. Use plural nouns for categories (e.g., `Prefabs`, `Scripts`, `Textures`).
- **Scripts:** `PascalCase`. File name must match the class name.
- **Assets:** Use prefixes followed by an underscore for fast searching/filtering:
    - **Prefabs:** `PFX_` or `PF_` (e.g., `PF_Player.prefab`)
    - **Materials:** `M_` (e.g., `M_Metal_Rusty.mat`)
    - **Textures:** `T_` or `Tex_` (e.g., `T_Brick_Albedo.png`)
    - **Animations:** `Anim_` (e.g., `Anim_Hero_Idle.anim`)
    - **Shaders:** `SH_` (e.g., `SH_Water_Flow.shader`)
    - **Scenes:** `Scene_` or `S_` (e.g., `S_Main_Menu.unity`)

## 2. C# Coding Standards

### Class & Interface Naming
- **Classes:** `PascalCase` nouns (e.g., `PlayerController`).
- **Interfaces:** `IPascalCase` (e.g., `IDamageable`).
- **Abstract Classes:** `PascalCase` with `Base` suffix or prefix if necessary (e.g., `EnemyBase`).

### Variables & Fields
- **Public Fields / Properties:** `PascalCase` (e.g., `public float Health { get; private set; }`).
- **Private / Protected Fields:** `_camelCase` with leading underscore (e.g., `private int _currentScore;`).
- **Local Variables / Parameters:** `camelCase` without underscore (e.g., `float deltaTime`).
- **Constants / Statics:** `UPPER_CASE` (e.g., `public const int MAX_INVENTORY_SIZE = 99;`).

### Method Naming
- **General Methods:** `PascalCase` verbs (e.g., `CalculatePhysics()`).
- **Event Handlers:** Prefix with `On` (e.g., `OnPlayerDeath()`).
- **Boolean Methods:** Prefix with `Is`, `Has`, `Can`, or `Should` (e.g., `IsDead()`, `HasRequiredKey()`).
- **Async Methods:** Suffix with `Async` if using `Task` or `UniTask`.

## 3. Unity Specific Patterns
- **References:** Suffix with the component type for clarity (e.g., `[SerializeField] private Button _submitButton;`).
- **Singleton:** Use `Instance` for the static accessor.

## 4. Architecture Suffixes
Identify the role of a script at a glance:
- `Manager`: Persistent systems (e.g., `AudioMN`).
- `Data`: ScriptableObject data containers (e.g., `WeaponData`).
- `View`: Handles UI or visual-only logic (e.g., `InventoryView`).
- `Controller`: Logic bridge between Input and Systems.
- `Settings`: Global configuration SOs (e.g., `GraphicsSettings`).

## 5. Performance & Memory Best Practices
- **No Vague Naming:** Avoid `temp`, `val`, `data1`. Use descriptive names like `elapsedTimeSeconds`.
- **Cache Optimization:** Suffix cached components with `Cache` or `Ref` if they are fetched during `Awake/Start`.
- **Collection Naming:** Use plural names for arrays/lists (e.g., `List<Enemy> _activeEnemies;`).

## 6. Forbidden Practices
- ❌ Do NOT use `m_` prefixes for member variables (Standard `_` is preferred).
- ❌ Do NOT use Hungarian notation (e.g., `int iCount`).
- ❌ Do NOT use `GameObject.Find` or `SendMessage` (performance killers).
- ❌ Do NOT use abbreviations that are not industry-standard (e.g., `PlyrCntrl` instead of `PlayerController`).