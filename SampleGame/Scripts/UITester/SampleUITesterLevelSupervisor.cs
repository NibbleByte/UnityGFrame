using DevLocker.GFrame.Input;
using DevLocker.GFrame.Input.Contexts;
using DevLocker.GFrame.SampleGame.Game;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace DevLocker.GFrame.SampleGame.UITester
{
	/// <summary>
	/// Supervisor to load the UITestScene used for testing out the UI + Input features of the GFrame.
	/// </summary>
	public class SampleUITesterLevelSupervisor : ILevelSupervisor
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
			if (SceneManager.GetActiveScene().name != "Sample-UITestScene") {
				// To bypass build settings list.
				var sceneParam = new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };
				var loadOp = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(SampleLevelsManager.GetEditorSampleScenePath("Sample-InputTestScene.unity"), sceneParam);
				while (!loadOp.isDone) await Task.Yield();
			}
#else
			// Can pass it on as a parameter to the supervisor, instead of hard-coding it here.
			if (SceneManager.GetActiveScene().name != "Sample-UITestScene") {
				var loadOp = SceneManager.LoadSceneAsync("Sample-UITestScene", LoadSceneMode.Single);
				while (!loadOp.isDone) await Task.Yield();
			}
#endif

			// StateStack not needed for now.
			//var levelController = GameObject.FindObjectOfType<SampleMainMenuController>();

			var playerContext = new PlayerContext();
			playerContext.CreatePlayerStack(
				gameContext.PlayerControls,
				InputUIRootObject.GlobalUIRoot
				);

			// The whole level is UI, so enable it for the whole level.
			gameContext.PlayerControls.Enable(this, gameContext.PlayerControls.Sample_UI);
			m_PlayerContexts.Add(playerContext);
		}

		public Task UnloadAsync()
		{
			SampleLevelsManager.Instance.GameContext.PlayerControls.DisableAll(this);

			return Task.CompletedTask;
		}
	}
}