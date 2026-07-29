using DevLocker.GFrame.SampleGame.Game;
using DevLocker.WiseInput;
using DevLocker.WiseInput.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DevLocker.GFrame.SampleGame.Play
{
	/// <summary>
	/// Similarly to <see cref="SamplePlaySupervisor"/>, supervisor to load the sample play scene used to demonstrate sample gameplay with the GFrame,
	/// focusing on play states & input hotkeys.
	/// This one supports multiple players.
	///
	/// For more info check this video: https://www.youtube.com/watch?v=g_s0y5yFxYg
	/// </summary>
	public class SampleMultiPlaySupervisor : ILevelSupervisor
	{
		public IReadOnlyCollection<PlayerContext> PlayerContexts => m_PlayerContexts;
		private readonly List<PlayerContext> m_PlayerContexts = new List<PlayerContext>();

		public async Task LoadAsync()
		{
			SampleGameContext gameContext = SampleLevelsManager.Instance.GameContext;

			if (MessageBox.MessageBox.Instance) {
				MessageBox.MessageBox.Instance.ForceCloseAllMessages();
			}

			// Disable the global PlayerInput component as each player will have their own.
			gameContext.PlayerInput.gameObject.SetActive(false);

#if UNITY_EDITOR
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-MultiPlayScene") {
				// To bypass build settings list.
				var sceneParam = new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(SampleLevelsManager.GetEditorSampleScenePath("Sample-MultiPlayScene.unity"), sceneParam);
				while (!loadOp.isDone) await Task.Yield();
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-PlayScene") {
				var loadOp = SceneManager.LoadSceneAsync("Sample-MultiPlayScene", LoadSceneMode.Single);
				while(!loadOp.isDone) await Task.Yield();
			}
#endif

			var eventSystems = GameObject.FindObjectsByType<MultiplayerEventSystem>(FindObjectsSortMode.None);
			Array.Sort(eventSystems, (left, right) => left.name.CompareTo(right.name));

			// Setup all the context and stacks for each player.

			foreach(MultiplayerEventSystem eventSystem in eventSystems) {
				var playerControls = new WiseInput.Sample.SamplePlayerControls();

				var playerInput = eventSystem.GetComponent<PlayerInput>();
				var playerController = playerInput.camera.GetComponentInParent<SamplePlayerController>();
				var uiController = eventSystem.playerRoot.GetComponentInParent<SamplePlayUIController>();


				// HACK: trick the PlayerInput to use the reference to our asset instead of copying the actions. Check the InputComponentContext() constructor for more info.
				// NOTE: the PlayerInput must initially have empty reference set for InputActionAsset in the prefab.
				playerInput.enabled = false;
				playerInput.actions = playerControls.asset;
				playerInput.enabled = true;

				var uiInputModule = eventSystem.GetComponentInChildren<InputSystemUIInputModule>();
				uiInputModule.actionsAsset = playerControls.asset;  // This will refresh the UI Input action references to the new asset.

				playerInput.uiInputModule = uiInputModule;

				// HACK: Starting two PlayerInput components while disabling the global one in the same startup frame doesn't work - no input is called.
				//		 Force them refresh using crude methods. This probably won't be needed if instantiating players dynamically.
				eventSystem.gameObject.SetActive(false);
				eventSystem.gameObject.SetActive(true);

				var inputContext = new InputComponentContext(playerInput, new InputActionsMaskedStack(playerControls), IInputContext.InputBehaviours.Default, GameObject.FindAnyObjectByType<SampleGameStarter>().BindingDisplayAssets);
				playerControls.SetInputContext(inputContext);

				//
				// Now the states stack & UI root...
				//
				var playerContext = new PlayerContext();
				var inputUIRoot = uiController.GetComponent<InputUIRootObject>();
				inputUIRoot.SetupContext(eventSystem, inputContext);

				playerContext.CreatePlayerStack(
					playerControls,
					playerController,
					uiController,
					inputUIRoot
				);


				// Only collect behaviours for this player and notify them with the correct references.
				var behaviours = CollectBehaviours(playerController, uiController, eventSystem);

				foreach (var listener in behaviours.OfType<ILevelLoadingListener>()) {
					await listener.OnLevelLoadingAsync(playerContext.StatesStack.Context);
				}

				foreach (var listener in behaviours.OfType<ILevelLoadedListener>()) {
					listener.OnLevelLoaded(playerContext.StatesStack.Context);
				}


				playerContext.StatesStack.SetState(new SamplePlayJumperState());

				m_PlayerContexts.Add(playerContext);
			}
		}


		public Task UnloadAsync()
		{
			// Stop the manager as it will log errors complaining about the global PlayerInput not having camera.
			GameObject.FindAnyObjectByType<PlayerInputManager>().enabled = false;

			// Same goes for player input & event systems. Note: collection is modified on disable PlayerInput
			foreach(PlayerInput playerInput in PlayerInput.all.ToList()) {
				if (playerInput) {
					playerInput.gameObject.SetActive(false);
				}
			}

			var levelListeners = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<ILevelLoadedListener>();
			foreach (var listener in levelListeners) {
				listener.OnLevelUnloading();
			}

			SampleLevelsManager.Instance.GameContext.PlayerInput.gameObject.SetActive(true);

			// Enabling input components causes for UI InputActions to get enabled, which confuses the MainMenu supervisor, who also enables them.
			SampleLevelsManager.Instance.GameContext.PlayerControls.Sample_UI.Disable();

			return Task.CompletedTask;
		}


		private static IEnumerable<MonoBehaviour> CollectBehaviours(params MonoBehaviour[] behaviours)
		{
			foreach(MonoBehaviour behaviour in behaviours) {
				foreach(var collectedBehaviour in behaviour.GetComponentsInChildren<MonoBehaviour>(true)) {
					yield return collectedBehaviour;
				}
			}
		}
	}
}