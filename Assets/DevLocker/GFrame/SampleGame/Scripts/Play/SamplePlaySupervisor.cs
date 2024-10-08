using DevLocker.GFrame.Input;
using DevLocker.GFrame.Input.Contexts;
using DevLocker.GFrame.SampleGame.Game;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevLocker.GFrame.SampleGame.Play
{
	/// <summary>
	/// Supervisor to load the sample play scene used to demonstrate sample gameplay with the GFrame,
	/// focusing on play states & input hotkeys.
	/// </summary>
	public class SamplePlaySupervisor : ILevelSupervisor
	{
#if GFRAME_ASYNC
		public async Task LoadAsync()
#else
		public IEnumerator Load()
#endif
		{
			SampleGameContext gameContext = SampleLevelsManager.Instance.GameContext;

			if (MessageBox.MessageBox.Instance) {
				MessageBox.MessageBox.Instance.ForceCloseAllMessages();
			}

#if UNITY_EDITOR
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-PlayScene") {
				// To bypass build settings list.
				var sceneParam = new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };
#if GFRAME_ASYNC
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Packages/devlocker.gframe/SampleGame/Scenes/Sample-PlayScene.unity", sceneParam);
				while (!loadOp.isDone) await Task.Yield();
#else
				yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Packages/devlocker.gframe/SampleGame/Scenes/Sample-PlayScene.unity", sceneParam);
#endif
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-PlayScene") {
#if GFRAME_ASYNC
				var loadOp = SceneManager.LoadSceneAsync("Sample-PlayScene", LoadSceneMode.Single);
				while(!loadOp.isDone) await Task.Yield();
#else
				yield return SceneManager.LoadSceneAsync("Sample-PlayScene", LoadSceneMode.Single);
#endif
			}
#endif

			var playerController = GameObject.FindAnyObjectByType<SamplePlayerController>();

			var uiController = GameObject.FindAnyObjectByType<SamplePlayUIController>(FindObjectsInactive.Include);

			PlayerContextUIRootObject.GlobalPlayerContext.CreatePlayerStack(
				gameContext.PlayerControls,
				playerController,
				uiController
				);


			var behaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			foreach (var listener in behaviours.OfType<ILevelLoadingListener>()) {
#if GFRAME_ASYNC
				await listener.OnLevelLoadingAsync(PlayerContextUIRootObject.GlobalPlayerContext.StatesStack.Context);
#else
				yield return listener.OnLevelLoading(PlayerContextUIRootObject.GlobalPlayerContext.StatesStack.Context);
#endif
			}

			foreach (var listener in behaviours.OfType<ILevelLoadedListener>()) {
				listener.OnLevelLoaded(PlayerContextUIRootObject.GlobalPlayerContext.StatesStack.Context);
			}

			PlayerContextUIRootObject.GlobalPlayerContext.StatesStack.SetState(new SamplePlayJumperState());
		}


#if GFRAME_ASYNC
		public Task UnloadAsync()
#else
		public IEnumerator Unload()
#endif
		{
			var levelListeners = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<ILevelLoadedListener>();
			foreach (var listener in levelListeners) {
				listener.OnLevelUnloading();
			}

#if GFRAME_ASYNC
			return Task.CompletedTask;
#else
			yield break;
#endif
		}
	}
}