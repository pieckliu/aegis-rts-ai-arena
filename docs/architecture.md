# Runtime architecture

The prototype runtime is being split incrementally so gameplay stays playable during the transition.

## Current components

- `GameBootstrap` coordinates match state, world creation, scene objects, and the extracted runtime systems.
- `GridMapService` owns map bounds, coordinate conversion, occupied cells, and nearby open-cell lookup.
- `BuildingPlacementSystem` validates and atomically reserves paid building placements.
- `UnitMovementSystem` owns formation-cell assignment, path requests, movement updates, and combat pursuit movement.
- `EnemyAISystem` owns enemy spawn timing, spawn-cell selection, and initial attack strategy.
- `EntityPresentationFactory` owns runtime grid lines, circle sprites, world labels, and presentation resource cleanup.
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

## Extraction status

The initial runtime extraction is complete. `GameBootstrap` now coordinates match state, domain registration, and the extracted systems instead of implementing each subsystem internally.

The next presentation step is to replace the factory's generated circles and labels with prefab-backed unit and building views. Enemy strategy can evolve behind `EnemyAISystem` without changing the Arena contract.

Each extraction should retain the existing Arena contract and add Edit Mode or Play Mode coverage before behavior changes are introduced.
