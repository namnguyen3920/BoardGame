# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (6000.3.13f1) board game. C# scripts live under `Assets/`; the Unity Editor is the build tool — no CLI build scripts exist.

## Architecture

### Core Patterns

- **MonoSingleton<T>** (`Assets/Core/Scripts/Singleton.cs`): Base for all manager MonoBehaviours. Subclass this for any new manager.
- **Singleton<T>**: Thread-safe non-MonoBehaviour singleton via `Lazy<T>`.
- **Factory + Interface**: Special nodes use `SpecialNodeFactory` → `SpecialNodeCreator` → `ISpecialNode` (Bonus/Fail).
- **ScriptableObject config**: `BoardData` (hex cells) and `TilePalette` (prefab arrays) store designer data outside code.

### Systems and Responsibilities

| Manager | File | Role |
|---|---|---|
| `GameMN` | `Assets/Core/Scripts/GameMN.cs` | Game state machine (Waiting → RollDice → SwitchPlayer), player ranking |
| `RouteMN` | `Assets/Board/Scripts/RouteMN.cs` | Populates node list, randomly assigns 6 special nodes |
| `DiceMN` | `Assets/Dice/Scripts/DiceMN.cs` | Dice rolls (1–6), `isDiceRolled` flag |
| `SpecialNodeMN` | `Assets/Node/Scripts/SpecialNodeMN.cs` | Applies materials to Bonus/Fail nodes |
| `UIMN` | `Assets/UI/Scripts/UIMN.cs` | Roll button, turn display, end-game panel, player stats |
| `MainMenu` | `Assets/UI/Scripts/MainMenu/MainMenu.cs` | Mode selection (PvP/BvB), scene transition |
| `BoardSpawner` | `Assets/Board/Scripts/BoardSpawner.cs` | Procedural hex tile generation from `BoardData`; editor context menu: Generate / Clear |

### Player System

`PlayerController` (abstract MonoSingleton) → `MainPlayer`. Movement is coroutine-driven; landing on a node checks `NodeType` (Normal / Bonus / Fail) and delegates effects to the factory-created special node.

### Game Flow

```
MainMenu (mode select) → GamePlay scene
→ GameMN loops: Roll → Move → Check node → Switch player
→ ReportWinner() when player reaches final node → end-game stats UI
```

## Naming Conventions

Managers are suffixed `MN` (e.g., `GameMN`, `RouteMN`). ScriptableObjects are suffixed `Data` or `Palette`.
