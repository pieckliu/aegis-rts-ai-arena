# Aegis RTS AI Arena



Aegis RTS AI Arena is an AI-native real-time strategy game prototype.



The project explores a tactical RTS environment where human players can fight against AI agents, and researchers can train, evaluate, and submit AI agents to compete on global leaderboards.



## Project Structure



- `AegisRts/` - Unity game project

- `docs/` - design notes and the in-process Arena API contract

## Current Stage



Playable Unity vertical slice:

- build a factory, spend and regenerate resources, and train infantry;
- select, box-select, move, and command groups to attack;
- fight an automatically spawning enemy army;
- win, lose, pause, restart, and return to the menu;
- query structured match observations and submit agent actions through the Arena API.

See [`docs/arena-api.md`](docs/arena-api.md) for the agent-facing contract.

