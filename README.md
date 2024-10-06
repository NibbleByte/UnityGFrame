# GFrame
Simple frame for a game that has levels, states and input. It utilizes the Input System package.

## Levels Framework
This lightweight framework introduces the `ILevelSupervisor` that is responsible for loading the level. The idea is for it to initialize the scene, instead of doing it in `Awake()` or `Start()` methods that rely on Unity magic event order. This way, you get a nice initialization sequence written in a single fucntion, where the order is clear.
Loading different level means changing the active `ILevelSupervisor`. Loading and unloading is done in coroutine OR aysnc/await methods.

## UI Input Scopes
`UIScope` is a component that enables or disables it's child `IScopeElement` components depending or whether it is active or not. You can have hierarchy of scopes and one of them can be focused, making all parents active (other inactive scopes will disable their elements). When enabled `IScopeElement` usually enable specified hotkeys or other objects.

`UIScope` helps you resolve input conflicts and manage hotkeys in the prefabs hierarchy, instead of code.

Best way to explore this framework is trying the `Sample-UITestScene.unity` scene that serves as documentation and test bed.

TODO: write better readme :)