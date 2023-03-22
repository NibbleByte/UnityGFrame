using DevLocker.GFrame.Input;
using DevLocker.GFrame.SampleGame.Game;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DevLocker.GFrame.SampleGame.MainMenu
{
	/// <summary>
	/// Supervisor to load the main menu and pass on the control.
	/// </summary>
	public class SampleMainMenuLevelSupervisor : ILevelSupervisor
	{
		public LevelStateStack StatesStack { get; private set; }

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
			if (SceneManager.GetActiveScene().name != "Sample-MainMenuScene") {
				// To bypass build settings list.
				var sceneParam = new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };
#if GFRAME_ASYNC
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Packages/devlocker.gframe/SampleGame/Scenes/Sample-MainMenuScene.unity", sceneParam);
				while(!loadOp.isDone) await Task.Yield();
#else
				yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Packages/devlocker.gframe/SampleGame/Scenes/Sample-MainMenuScene.unity", sceneParam);
#endif
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-MainMenuScene") {
#if GFRAME_ASYNC
				var loadOp = SceneManager.LoadSceneAsync("Sample-MainMenuScene", LoadSceneMode.Single);
				while (!loadOp.isDone) await Task.Yield();
#else
				yield return SceneManager.LoadSceneAsync("Sample-MainMenuScene", LoadSceneMode.Single);
#endif
			}
#endif

			// StateStack not needed for now.
			//var levelController = GameObject.FindObjectOfType<SampleMainMenuController>();
			//

			StatesStack = PlayerContextUtils.GlobalPlayerContext.CreatePlayerStack(SampleLevelsManager.Instance,
				gameContext.PlayerControls
				);

			// The whole level is UI, so enable it for the whole level.
			gameContext.PlayerControls.InputStack.PushActionsState(this);
			gameContext.PlayerControls.UI.Enable();
		}

#if GFRAME_ASYNC
		public Task UnloadAsync()
#else
		public IEnumerator Unload()
#endif
		{
			SampleLevelsManager.Instance.GameContext.PlayerControls.InputStack.PopActionsState(this);

			PlayerContextUtils.GlobalPlayerContext.ClearContextReferences();

#if GFRAME_ASYNC
			return Task.CompletedTask;
#else
			yield break;
#endif
		}
	}
}