# GFrame
Simple frame for a game that has levels, states and input. It depends on [Wise Input](https://github.com/NibbleByte/UnityWiseInput) library which utilizes the Input System package.

## Levels Framework
This lightweight framework introduces the `ILevelSupervisor` that is responsible for loading the level. The idea is for it to initialize the scene, instead of doing it in `Awake()` or `Start()` methods that rely on Unity magic event order. This way, you get a nice initialization sequence written in a single function, where the order is clear.
Loading different level means changing the active `ILevelSupervisor`. Loading and unloading is done in aysnc/await methods.

## Player Context
Player context represents the state of the current player. You can have many player contexts at the same time - useful for split-screen multiplayer gameplay. Each player has its own UI states stack and input context. You can push UI state on top of the stack to change the current UI and controls behaviour (e.g. game state, paused state, world map state). States receive context which can be used to access the player and other references (instead of relying on singletons).