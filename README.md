# Aegis RTS AI Arena



Aegis RTS AI Arena is an AI-native real-time strategy game prototype.



The project explores a tactical RTS environment where human players can fight against AI agents, and researchers can train, evaluate, and submit AI agents to compete on global leaderboards.



## Project Structure



- `AegisRts/` - Unity game project

- `docs/` - design notes and the in-process Arena API contract
- `Assets/_Project/Resources/RtsGameConfig.asset` - centralized gameplay balance

## Current Stage



Playable Unity vertical slice:

- explore an expanded 48×48 battlefield that opens around the player base;
- place 3×3-footprint bases and factories that block movement and constrain base layouts;
- spend and regenerate resources, then train infantry or deployable long-range artillery;
- use a shared ordered factory queue and wait when every footprint-adjacent exit is blocked;
- select, box-select, drag friendly units to move, and command groups to attack;
- interrupt infantry combat with a new move order so player-controlled units can retreat;
- move directly to exact clicked positions, keep unit volumes separated, and use grid detours around occupied structures;
- deploy artillery to unlock firing and display its range, then undeploy it before moving again;
- start focused directly on the player-base side of the battlefield, then pan and zoom the bounded camera;
- explore a three-state fog of war driven by player building and unit sight;
- use a live tactical minimap with real-time friendly tracking and fading last-known enemy contacts, click or drag it to navigate, and press `M` to toggle the fog-respecting strategic overview;
- read player bases, factories, infantry, artillery, and enemies as distinct symbolic dots;
- use a runtime uGUI menu, command panel, overlays, selection rectangle, and health bars;
- fight an automatically spawning enemy army;
- win, lose, pause, restart, and return to the menu;
- query structured match observations and submit agent actions through the Arena API.

See [`docs/arena-api.md`](docs/arena-api.md) for the agent-facing contract.
See [`docs/architecture.md`](docs/architecture.md) for the runtime component boundaries and extraction order.

The active prototype intentionally uses symbolic runtime graphics and no runtime audio. Authored
presentation assets remain in the repository for possible later use, but gameplay currently prioritizes
map readability, visibility rules, and tactical-map interaction.

