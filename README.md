# Aegis RTS AI Arena



Aegis RTS AI Arena is an AI-native real-time strategy game prototype.



The project explores a tactical RTS environment where human players can fight against AI agents, and researchers can train, evaluate, and submit AI agents to compete on global leaderboards.



## Project Structure



- `AegisRts/` - Unity game project

- `docs/` - design notes and the in-process Arena API contract
- `Assets/_Project/Resources/RtsGameConfig.asset` - centralized gameplay balance

## Current Stage



Playable Unity vertical slice:

- build a factory, spend and regenerate resources, and train infantry;
- queue production in factories and wait when an exit is blocked;
- select, box-select, move, and command groups to attack;
- navigate groups around occupied grid cells;
- pan and zoom the bounded battlefield camera;
- explore a three-state fog of war driven by player building and unit sight;
- use a live tactical minimap, click or drag it to navigate, and press `M` to toggle the strategic overview;
- read player bases, factories, infantry, and enemies as distinct symbolic dots;
- use a runtime uGUI menu, command panel, overlays, selection rectangle, and health bars;
- fight an automatically spawning enemy army;
- win, lose, pause, restart, and return to the menu;
- query structured match observations and submit agent actions through the Arena API.

See [`docs/arena-api.md`](docs/arena-api.md) for the agent-facing contract.
See [`docs/architecture.md`](docs/architecture.md) for the runtime component boundaries and extraction order.

The active prototype intentionally uses symbolic runtime graphics and no runtime audio. Authored
presentation assets remain in the repository for possible later use, but gameplay currently prioritizes
map readability, visibility rules, and tactical-map interaction.

