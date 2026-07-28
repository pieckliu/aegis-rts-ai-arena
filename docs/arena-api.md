# Aegis RTS Arena API

`GameBootstrap` exposes a small in-process API for agents and future Python bridges.

## Observation

Call `GetArenaObservation()` for a typed snapshot or `GetArenaObservationJson()` for JSON.

The snapshot contains:

- match time, player resources, terminal state, and result;
- every building and unit with a stable match-local ID;
- entity kind, team, world position, grid cell, and health;
- each building's exact occupied grid cells;
- factory queue count, current production kind, and production progress.
- artillery deployment state.

## Actions

Pass an `ArenaAction` to `ExecuteArenaAction`.

Supported action types:

- `Move`: set `UnitIds`, `CellX`, and `CellY`;
- `Attack`: set `UnitIds` and `TargetId`;
- `BuildFactory`: set `CellX` and `CellY`;
- `TrainInfantry`: no additional fields are required;
- `TrainArtillery`: no additional fields are required;
- `DeployArtillery`: set the artillery `UnitIds`;
- `UndeployArtillery`: set the artillery `UnitIds`.

Every call returns an `ArenaActionResult` with `Accepted` and `Message`.

Actions are rejected while the match is paused, terminal, or outside the playing state. The API uses the same validation and command paths as human input so agent matches follow normal game rules.

Human-controlled units use grid pathfinding for movement orders. Every cell in a building's
3×3 footprint is blocked, while each member of a selected group receives its own destination.
Undeployed artillery can move but cannot fire. Deployed artillery cannot move, can attack from
long range, and deals bonus damage to buildings.

## Next bridge

A transport layer can call this API from Unity ML-Agents, a local HTTP/WebSocket server, or a native Python bridge. Keep transport concerns outside `GameBootstrap`; this contract is intended to remain deterministic and serializable.
