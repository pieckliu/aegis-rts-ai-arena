# Aegis RTS Arena API

`GameBootstrap` exposes a small in-process API for agents and future Python bridges.

## Observation

Call `GetArenaObservation()` for a typed snapshot or `GetArenaObservationJson()` for JSON.

The snapshot contains:

- match time, player resources, terminal state, and result;
- every building and unit with a stable match-local ID;
- entity kind, team, world position, grid cell, and health;
- factory queue count and current production progress.

## Actions

Pass an `ArenaAction` to `ExecuteArenaAction`.

Supported action types:

- `Move`: set `UnitIds`, `CellX`, and `CellY`;
- `Attack`: set `UnitIds` and `TargetId`;
- `BuildFactory`: set `CellX` and `CellY`;
- `TrainInfantry`: no additional fields are required.

Every call returns an `ArenaActionResult` with `Accepted` and `Message`.

Actions are rejected while the match is paused, terminal, or outside the playing state. The API uses the same validation and command paths as human input so agent matches follow normal game rules.

Human-controlled units use grid pathfinding for movement orders. Occupied building and destination cells are treated as blocked, while each member of a selected group receives its own destination.

## Next bridge

A transport layer can call this API from Unity ML-Agents, a local HTTP/WebSocket server, or a native Python bridge. Keep transport concerns outside `GameBootstrap`; this contract is intended to remain deterministic and serializable.
