using DevLocker.GFrame.Input;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DevLocker.GFrame.SampleGame.Play
{
	public enum PlayUIState
	{
		None = 0,
		Play = 2,
		Paused = 4,
		Options = 8,
	}

	/// <summary>
	/// Sample UI controller to switch states of the UI and expose methods for the UI buttons to call.
	/// </summary>
	public class SamplePlayUIController : MonoBehaviour
	{
		[Serializable]
		public struct StatePanelBinds
		{
			public PlayUIState State;
			public GameObject Panel;
		}

		public PlayUIState CurrentState = PlayUIState.Play;

		public GameObject JumperModePanel;
		public GameObject ChopperModePanel;

		public TMPro.TextMeshProUGUI ModeLabelTMP;

		public StatePanelBinds[] StatePanels;

		// Used for multiple event systems (e.g. split screen).
		protected IPlayerContext m_PlayerContext;

		void Awake()
		{
			m_PlayerContext = PlayerContextUtils.GetPlayerContextFor(gameObject);

			foreach (var bind in StatePanels) {
				bind.Panel.SetActive(false);
			}

			SwitchState(CurrentState, true);
		}

		public void SwitchState(PlayUIState state, bool? jumperMode = null)
		{
			if (jumperMode.HasValue) {
				JumperModePanel?.SetActive(jumperMode.Value);
				ChopperModePanel?.SetActive(!jumperMode.Value);

				if (ModeLabelTMP) {
					ModeLabelTMP.text = $"Player Mode: {(jumperMode.Value ? "Jumper" : "Chopper")}";
				}
			}

			if (state == CurrentState)
				return;

			if (CurrentState != PlayUIState.None) {
				var prevPanel = GetPanel(CurrentState);
				prevPanel.SetActive(false);
			}

			CurrentState = state;

			var nextPanel = GetPanel(state);
			nextPanel.SetActive(true);
		}

		public GameObject GetPanel(PlayUIState state)
		{
			foreach (var bind in StatePanels) {
				if (state == bind.State)
					return bind.Panel;
			}

			throw new NotImplementedException();
		}

		public void PauseLevel()
		{
			// Will be popped by UI.
			m_PlayerContext.StatesStack.PushState(new SamplePlayPausedState());
			//Game.SampleLevelsManager.Instance.PushGlobalState(new SamplePlayPausedState());
		}

		public void OpenOptions()
		{
			// Will be popped by UI.
			m_PlayerContext.StatesStack.PushState(new SamplePlayOptionsState());
			//Game.SampleLevelsManager.Instance.PushGlobalState(new SamplePlayOptionsState());
		}

		public void ExitToMainMenu()
		{
#if GFRAME_ASYNC
			Game.SampleLevelsManager.Instance.SwitchLevelAsync(new MainMenu.SampleMainMenuLevelSupervisor());
			//Game.SampleLevelsManager.Instance.SwitchLevelAsync(new MainMenu.SampleMainMenuLevelSupervisor());
#else
			Game.SampleLevelsManager.Instance.SwitchLevel(new MainMenu.SampleMainMenuLevelSupervisor());
			//Game.SampleLevelsManager.Instance.SwitchLevel(new MainMenu.SampleMainMenuLevelSupervisor());
#endif
		}
	}
}