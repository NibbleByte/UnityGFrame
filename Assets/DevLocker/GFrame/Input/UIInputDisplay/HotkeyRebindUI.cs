#if USE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace DevLocker.GFrame.Input.UIInputDisplay
{
	/// <summary>
	/// Exposes API to rebind an InputAction.
	/// </summary>
	public class HotkeyRebindUI : MonoBehaviour
	{
		[Tooltip("The display of the hotkey - it will rebind it's InputAction.")]
		public HotkeyDisplayUI DisplayUI;

		public float WaitSecondsOnMatch = 0.1f;

		[Tooltip("Binding used for cancel. Examples:\n\"<Keyboard>/escape\" - ESC\n\"*/{Cancel}\" - any input considered as cancel (use with caution).")]
		[InputControl]
		public string CancelBinding;

		[InputControl]
		[NonReorderable]
		public List<string> IncludeRebindsTo;

		[InputControl]
		[NonReorderable]
		public List<string> ExcludeRebindsTo;

		public UnityEvent RebindStarted;
		public UnityEvent RebindFinished;
		public UnityEvent RebindReset;

		// Used for multiple event systems (e.g. split screen).
		protected IPlayerContext m_PlayerContext;

		private InputActionRebindingExtensions.RebindingOperation m_RebindOperation;

		void Awake()
		{
			if (DisplayUI == null) {
				DisplayUI = GetComponent<HotkeyDisplayUI>();
			}

			m_PlayerContext = PlayerContextUtils.GetPlayerContextFor(gameObject);
		}


		[ContextMenu("Start Rebind")]
		public void StartRebind()
		{
			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyRebindUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			if (m_RebindOperation != null) {
				m_RebindOperation.Dispose();
				m_RebindOperation = null;
			}

			InputAction action = m_PlayerContext.InputContext.FindActionFor(DisplayUI.InputAction);

			if (DisplayUI.CurrentlyDisplayedData.Binding.id == Guid.Empty)
				return;

			// Actions need to be disabled in order to be re-bind.
			m_PlayerContext.InputContext.PushOrSetActionsMask(this, new InputAction[0]);

			m_RebindOperation = action.PerformInteractiveRebinding();
			m_RebindOperation.WithTargetBinding(DisplayUI.CurrentlyDisplayedData.BindingIndex);

			//m_RebindOperation.WithBindingGroup(DisplayUI.CurrentlyDisplayedData.ControlScheme); // Doesn't filter out groups?!
			//m_RebindOperation.WithExpectedControlType(/* Control type like "Key", "Button"*/);

			if (!string.IsNullOrEmpty(CancelBinding)) {
				m_RebindOperation.WithCancelingThrough(CancelBinding);
			}

			m_RebindOperation.OnMatchWaitForAnother(WaitSecondsOnMatch);

			foreach (var include in IncludeRebindsTo) {
				m_RebindOperation.WithControlsHavingToMatchPath(include);
			}

			foreach (var excluded in ExcludeRebindsTo) {
				m_RebindOperation.WithControlsExcluding(excluded);
			}

			m_RebindOperation.OnCancel(OnRebindCancel);
			m_RebindOperation.OnComplete(OnRebindComplete);

			m_RebindOperation.Start();

			RebindStarted.Invoke();
		}

		[ContextMenu("Cancel Rebind")]
		public void CancelRebind()
		{
			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyRebindUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			if (m_RebindOperation != null) {
				m_RebindOperation.Cancel();
			}
		}

		[ContextMenu("Reset Bind")]
		public void ResetBind()
		{
			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyRebindUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			if (m_RebindOperation != null) {
				m_RebindOperation.Cancel();
				// Will be disposed by the OnCancel hook.
			}

			if (DisplayUI.CurrentlyDisplayedData.Binding.id == Guid.Empty)
				return;

			InputAction action = m_PlayerContext.InputContext.FindActionFor(DisplayUI.InputAction);
			action.RemoveBindingOverride(DisplayUI.CurrentlyDisplayedData.BindingIndex);

			DisplayUI.RefreshDisplay();
			RebindReset.Invoke();
		}

		private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
		{
			Debug.Assert(m_RebindOperation == operation);

			if (m_RebindOperation == null)
				return;

			m_RebindOperation.Dispose();
			m_RebindOperation = null;

			m_PlayerContext?.InputContext?.PopActionsMask(this);

			DisplayUI.RefreshDisplay();
			RebindFinished.Invoke();
		}

		private void OnRebindCancel(InputActionRebindingExtensions.RebindingOperation operation)
		{
			Debug.Assert(m_RebindOperation == operation);

			if (m_RebindOperation == null)
				return;

			m_RebindOperation.Dispose();
			m_RebindOperation = null;

			m_PlayerContext?.InputContext?.PopActionsMask(this);

			DisplayUI.RefreshDisplay();
			RebindFinished.Invoke();
		}

		private void OnDestroy()
		{
			if (m_RebindOperation != null) {
				m_RebindOperation.Dispose();
				m_RebindOperation = null;
			}

			m_PlayerContext = null;
		}
	}

}
#endif