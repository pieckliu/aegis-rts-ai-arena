# Runtime architecture

The prototype runtime is being split incrementally so gameplay stays playable during the transition.

## Current components

- `GameBootstrap` coordinates match state, world creation, scene objects, and the extracted runtime systems.
- `GridMapService` owns map bounds, coordinate conversion, occupied cells, and nearby open-cell lookup.
- `BuildingPlacementSystem` validates and atomically reserves paid building placements.
- `UnitMovementSystem` owns exact-position direct movement, formation-cell assignment, obstacle-triggered grid path requests, movement updates, combat pursuit movement, and deterministic unit-volume separation.
- `EnemyAISystem` owns enemy spawn timing, spawn-cell selection, and initial attack strategy.
- `EntityPresentationFactory` creates symbolic circle views, grid lines, and overlays. Authored prefabs remain optional and are disabled in the active prototype.
- `PresentationPrefabCatalog` is the Resources-loaded source of player/enemy building, infantry, overlay, and grid-line prefabs.
- `RtsEntityViewAnimator` adds presentation-only idle, attack-kick, and hit-shake animation to entity prefabs.
- `RtsAudioFeedbackSystem` plays catalog-backed attack, impact, and production-complete clips without affecting simulation state.
- `GameDomain` owns shared team, entity-type, building, and unit runtime models.
- `RtsCameraController` owns camera setup, movement, zoom, map bounds, minimap navigation, and strategic-overview switching.
- `RtsVisibilitySystem` owns current visibility, persistent exploration, the world fog texture, enemy presentation visibility, and non-leaking last-known enemy contact snapshots.
- `RtsGameConfig` is the ScriptableObject source for map, economy, combat, production, AI, movement, and camera balance values.
- `RtsSelectionInputController` owns click, empty-ground box selection, direct unit-drag movement, and command-input state.
- `RtsEconomyProductionSystem` owns player resources, passive income, factory queues, and production timing.
- `RtsCombatSystem` owns target acquisition, pursuit, cooldowns, damage, and combat resolution.
- `CombatFeedbackEvent` is the one-way boundary from deterministic combat resolution to presentation.
- `RtsWorldFeedbackSystem` owns transient attack projectiles, hit flashes, and death pulses.
- `RtsEntityLifecycle` owns entity removal, occupancy cleanup, target cleanup, and destruction callbacks.
- `ArenaOrchestrator` owns observation building, action validation, entity lookup, and command routing.
- `RtsGameUIController` builds and updates the runtime uGUI menu, minimal gameplay controls, tactical minimap, camera viewport, selection rectangle, health bars, production progress, and transient notifications.
- `MinimapPointerHandler` converts minimap pointer and drag input into normalized camera-navigation requests.
- `ArenaGameRules` contains deterministic economy and damage rules.
- `GridPathfinder` contains deterministic grid path search.
- `ArenaContracts` defines serializable observations, actions, entities, and results.

The default balance asset is `Assets/_Project/Resources/RtsGameConfig.asset`. It defines the
expanded 48×48 exploration map, camera limits, sight ranges, and unit collision padding.
`GameBootstrap` loads it at startup and falls back to scene values only if the asset is missing.

## Extraction status

The initial runtime extraction is complete. `GameBootstrap` now coordinates match state, domain registration, and the extracted systems instead of implementing each subsystem internally.

Authored presentation assets remain under `Assets/_Project/Art`, `Assets/_Project/Audio`, and
`Assets/_Project/Prefabs/Presentation` for possible later use. The active prototype deliberately
renders gameplay entities as colored circles and does not create the runtime audio system. The
minimap and world map share visibility state, preventing hidden enemy entities and health bars from
leaking information. Friendly minimap contacts update from live positions; hidden mobile enemy
contacts remain frozen at their last observed position and expire after the configured memory
duration, while discovered static enemy buildings remain as dim strategic intelligence.

Combat feedback remains presentation-only: `RtsCombatSystem` publishes immutable hit data and never depends on visual state. Production progress is derived from the existing factory queue, so the Arena observation and action contract stays unchanged.

Authored art and audio provenance is documented in `docs/art-audio-assets.md`. Enemy strategy can
evolve behind `EnemyAISystem` without changing the Arena contract.

Each extraction should retain the existing Arena contract and add Edit Mode or Play Mode coverage before behavior changes are introduced.
