using DevLocker.GFrame.Input;
using TMPro;
using UnityEngine;


namespace DevLocker.GFrame.UIUtils
{
	/// <summary>
	/// Print the current supervisor and state in the UI.
	/// </summary>
	public class UIDebugShowSupervisorAndState : MonoBehaviour
	{
		public string SupervisorPrefix = "Supervisor: ";
		public TextMeshProUGUI SupervisorText;

		public string StatePrefix = "State: ";
		public TextMeshProUGUI StateText;

		private ILevelSupervisor m_CurrentLevelSupervisor;
		private IPlayerState m_CurrentPlayerState;

		protected PlayerContext m_PlayerContext;

		void Awake()
		{
			if (SupervisorText) SupervisorText.text = string.Empty;
			if (StateText) StateText.text = string.Empty;
		}

		void Update()
		{
			if (LevelsManager.Instance.LevelSupervisor == null)
				return;

			if (m_PlayerContext == null) {
				m_PlayerContext = LevelsManager.Instance.GetPlayerContextFor(gameObject);
			}

			ILevelSupervisor nextSupervisor = LevelsManager.Instance.LevelSupervisor;
			IPlayerState nextState = m_PlayerContext.StatesStack?.CurrentState;

			if (SupervisorText && nextSupervisor != m_CurrentLevelSupervisor) {
				m_CurrentLevelSupervisor = nextSupervisor;
				SupervisorText.text = SupervisorPrefix + (m_CurrentLevelSupervisor?.GetType().Name ?? string.Empty);
			}

			if (StateText && nextState != m_CurrentPlayerState) {
				m_CurrentPlayerState = nextState;
				StateText.text = StatePrefix + (m_CurrentPlayerState?.GetType().Name ?? string.Empty);
			}
		}
	}
}