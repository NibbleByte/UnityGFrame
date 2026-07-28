using DevLocker.GFrame.Input;
using DevLocker.GFrame.Input.Contexts;
using DevLocker.GFrame.SampleGame.Game;
using System.Collections.Generic;
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
		public IReadOnlyCollection<PlayerContext> PlayerContexts => m_PlayerContexts;
		private readonly List<PlayerContext> m_PlayerContexts = new List<PlayerContext>();

		public async Task LoadAsync()
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
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(SampleLevelsManager.GetEditorSampleScenePath("Sample-PlayScene.unity"), sceneParam);
				while (!loadOp.isDone) await Task.Yield();
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-PlayScene") {
				var loadOp = SceneManager.LoadSceneAsync("Sample-PlayScene", LoadSceneMode.Single);
				while(!loadOp.isDone) await Task.Yield();
			}
#endif

			var playerController = GameObject.FindAnyObjectByType<SamplePlayerController>();

			var uiController = GameObject.FindAnyObjectByType<SamplePlayUIController>(FindObjectsInactive.Include);

			var playerContext = new PlayerContext();
			playerContext.CreatePlayerStack(
				gameContext.PlayerControls,
				playerController,
				uiController,
				InputUIRootObject.GlobalUIRoot
				);


			var behaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			foreach (var listener in behaviours.OfType<ILevelLoadingListener>()) {
				await listener.OnLevelLoadingAsync(playerContext.StatesStack.Context);
			}

			foreach (var listener in behaviours.OfType<ILevelLoadedListener>()) {
				listener.OnLevelLoaded(playerContext.StatesStack.Context);
			}

			playerContext.StatesStack.SetState(new SamplePlayJumperState());
			m_PlayerContexts.Add(playerContext);
		}


		public Task UnloadAsync()
		{
			var levelListeners = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<ILevelLoadedListener>();
			foreach (var listener in levelListeners) {
				listener.OnLevelUnloading();
			}

			return Task.CompletedTask;
		}
	}
}