using DevLocker.GFrame.SampleGame.Game;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DevLocker.GFrame.SampleGame.Play
{
	/// <summary>
	/// Player is in chopper state - can move freely in all directions with gravity turned off.
	/// This state also controls what is displayed on the UI via the UIController.
	/// </summary>
	public class SamplePlayChopperState : ILevelState, SamplePlayerControls.IPlayChopperActions
	{
		private SamplePlayerControls m_PlayerControls;
		private SamplePlayerController m_PlayerController;
		private SamplePlayUIController m_UIController;

#if GFRAME_ASYNC
		public Task EnterStateAsync(LevelStateContextReferences contextReferences)
#else
		public IEnumerator EnterState(LevelStateContextReferences contextReferences)
#endif
		{
			contextReferences.SetByType(out m_PlayerControls);
			contextReferences.SetByType(out m_PlayerController);
			contextReferences.SetByType(out m_UIController);

			m_PlayerControls.InputStack.PushActionsState(this);
			m_PlayerControls.UI.Enable();
			m_PlayerControls.PlayChopper.SetCallbacks(this);
			m_PlayerControls.PlayChopper.Enable();

			// You don't want "Return" key to trigger selected buttons.
			m_PlayerControls.UI.Submit.Disable();
			m_PlayerControls.UI.Navigate.Disable();

			m_UIController.SwitchState(PlayUIState.Play, false);

#if GFRAME_ASYNC
			return Task.CompletedTask;
#else
			yield break;
#endif
		}

#if GFRAME_ASYNC
		public Task ExitStateAsync()
#else
		public IEnumerator ExitState()
#endif
		{
			m_PlayerControls.PlayChopper.SetCallbacks(null);
			m_PlayerControls.InputStack.PopActionsState(this);

#if GFRAME_ASYNC
			return Task.CompletedTask;
#else
			yield break;
#endif
		}

		public void OnChopperMovement(InputAction.CallbackContext context)
		{
			m_PlayerController.ChopperMovement(context.ReadValue<Vector2>());
		}

		public void OnSwitchToJumper(InputAction.CallbackContext context)
		{
			m_PlayerController.SwitchToJumper();
		}
	}
}