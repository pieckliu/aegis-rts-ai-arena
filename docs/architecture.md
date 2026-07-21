# Runtime architecture

The prototype runtime is being split incrementally so gameplay stays playable during the transition.

## Current components

- `GameBootstrap` coordinates match state, world creation, scene objects, and the extracted runtime systems.
- `GameDomain` owns shared team, entity-type, building, and unit runtime models.
- `RtsCameraController` owns camera setup, movement, zoom, and map bounds.
- `RtsGameConfig` is the ScriptableObject source for map, economy, combat, production, AI, movement, and camera balance values.
- `RtsSelectionInputController` owns click, drag-selection, and command-input state.
- `RtsEconomyProductionSystem` owns player resources, passive income, factory queues, and production timing.
- `RtsCombatSystem` owns target acquisition, pursuit, cooldowns, damage, and combat resolution.
- `RtsEntityLifecycle` owns entity removal, occupancy cleanup, target cleanup, and destruction callbacks.
- `ArenaOrchestrator` owns observation building, action validation, entity lookup, and command routing.
- `RtsGameUIController` builds and updates the runtime uGUI menu, HUD, command panel, overlays, selection rectangle, and health bars.
- `ArenaGameRules` contains deterministic economy and damage rules.
- `GridPathfinder` contains deterministic grid path search.
- `ArenaContracts` defines serializable observations, actions, entities, and results.

The default balance asset is `Assets/_Project/Resources/RtsGameConfig.asset`. `GameBootstrap` loads it at startup and falls back to scene values only if the asset is missing.

## Remaining extractions

The remaining responsibilities should leave `GameBootstrap` in this order:

1. building placement and world-coordinate helpers;
2. unit movement and formation reservation;
3. enemy spawning and strategy;
4. runtime entity presentation and prefab creation.

Each extraction should retain the existing Arena contract and add Edit Mode or Play Mode coverage before behavior changes are introduced.
