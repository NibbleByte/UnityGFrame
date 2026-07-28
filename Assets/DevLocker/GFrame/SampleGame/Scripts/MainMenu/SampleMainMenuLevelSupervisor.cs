using DevLocker.GFrame.Input;
using DevLocker.GFrame.Input.Contexts;
using DevLocker.GFrame.SampleGame.Game;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DevLocker.GFrame.SampleGame.MainMenu
{
	/// <summary>
	/// Supervisor to load the main menu and pass on the control.
	/// </summary>
	public class SampleMainMenuLevelSupervisor : ILevelSupervisor
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
			if (SceneManager.GetActiveScene().name != "Sample-MainMenuScene") {
				// To bypass build settings list.
				var sceneParam = new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(SampleLevelsManager.GetEditorSampleScenePath("Sample-MainMenuScene.unity"), sceneParam);
				while(!loadOp.isDone) await Task.Yield();
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-MainMenuScene") {
				var loadOp = SceneManager.LoadSceneAsync("Sample-MainMenuScene", LoadSceneMode.Single);
				while (!loadOp.isDone) await Task.Yield();
			}
#endif

			// StateStack not needed for now.
			//var levelController = GameObject.FindObjectOfType<SampleMainMenuController>();
			//

			var playerContext = new PlayerContext();
			playerContext.CreatePlayerStack(
				gameContext.PlayerControls,
				InputUIRootObject.GlobalUIRoot
				);

			// The whole level is UI, so enable it for the whole level.
			playerContext.InputContext.Enable(this, gameContext.PlayerControls.Sample_UI);

			m_PlayerContexts.Add(playerContext);
		}

		public Task UnloadAsync()
		{
			InputUIRootObject.GlobalUIRoot.InputContext.DisableAll(this);

			return Task.CompletedTask;
		}
	}
}