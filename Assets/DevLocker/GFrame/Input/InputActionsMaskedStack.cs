#if USE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace DevLocker.GFrame.Input
{
	/// <summary>
	/// Keeps track of the input actions state (enable flag).
	/// Can push a mask for what actions are allowed to be enabled (others will be automatically disabled until mask is removed).
	/// For the mask to work, enabling and disabling actions must always be done via this class, not directly by InputAction.Enable().
	/// Masks are stacked and only the top one is active.
	/// </summary>
	public class InputActionsMaskedStack
	{
		private class StateSourceBind
		{
			public object Source;
			public HashSet<InputAction> MaskActions;
		}

		public struct InputActionConflictsReport
		{
			/// <summary>
			/// Actions that have 2 or more users that requested it enabled.
			/// </summary>
			public List<KeyValuePair<InputAction, IReadOnlyCollection<object>>> Conflicts => m_Conflicts != null ? m_Conflicts : (m_Conflicts = new ());
			private List<KeyValuePair<InputAction, IReadOnlyCollection<object>>> m_Conflicts;

			/// <summary>
			/// Actions that no one asked the <see cref="IInputContext"/> to enable, but they are enabled anyway. This means that some code is bypassing the <see cref="IInputContext"/>.
			/// </summary>
			public List<InputAction> IllegalActions => m_IllegalActions != null ? m_IllegalActions : (m_IllegalActions = new ());
			private List<InputAction> m_IllegalActions;

			public bool HasIssuesFound => m_Conflicts?.Count > 0 || m_IllegalActions?.Count > 0;

			/// <summary>
			/// Compare if two collections of conflicts are equal.
			/// </summary>
			public bool Equals(InputActionConflictsReport conflictsReport)
			{
				// Avoid creating garbage by invoking getter properties for nothing.
				if (m_Conflicts == null && conflictsReport.m_Conflicts == null && m_IllegalActions == null || conflictsReport.m_IllegalActions == null)
					return true;

				return conflictsReport.Conflicts.SequenceEqual(Conflicts) && conflictsReport.IllegalActions.SequenceEqual(IllegalActions);
			}
		}

		public IReadOnlyCollection<InputAction> Actions => m_Actions.Keys;

		private Dictionary<InputAction, HashSet<object>> m_Actions = new Dictionary<InputAction, HashSet<object>>();

		private List<StateSourceBind> m_MasksStack = new List<StateSourceBind>();

		private HashSet<InputAction> m_CurrentActionsMask => m_MasksStack.LastOrDefault()?.MaskActions;

		private bool m_Disposed = false;

		public InputActionsMaskedStack(IInputActionCollection2 actions)
		{
			foreach(InputAction action in actions) {
				m_Actions.Add(action, new HashSet<object>(2));
			}
		}

		/// <summary>
		/// Called by the InputContext when you're done with it.
		/// Once disposed, it will ignore all enable/disable requests.
		/// This will suppress any input conflict errors for scope elements being destroyed afterwards.
		/// </summary>
		public void Dispose()
		{
			ForceClearAllEnableRequests();
			m_Disposed = true;
		}

		/// <summary>
		/// Enable action if mask stack allows it.
		/// Remembers the enable state in case the mask gets removed later on.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public void Enable(object source, InputAction action)
		{
			if (m_Disposed)
				return;

			if (source == null || action == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Enabling by source of type InputAction is not allowed.");

			if (!m_Actions.TryGetValue(action, out HashSet<object> enableSources))
				throw new ArgumentException($"Input action \"{action}\" is not part of the tracked actions.");

			if (enableSources.Count == 0 && action.enabled) {
				UnityEngine.Debug.LogError($"Trying to enable input action \"{action.name}\" by {source}, but it is already enabled. Some code is enabling input actions without the IInputContext!", source as UnityEngine.Object);
			}

			enableSources.Add(source);

			if (m_CurrentActionsMask?.Contains(action) ?? true) {
				action.Enable();
			}
		}

		/// <summary>
		/// Disable action.
		/// Remembers the disable state for later updates.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public void Disable(object source, InputAction action)
		{
			if (m_Disposed)
				return;

			if (source == null || action == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Disabling by source of type InputAction is not allowed.");

			if (!m_Actions.TryGetValue(action, out HashSet<object> enableSources))
				throw new ArgumentException($"Input action \"{action}\" is not part of the tracked actions.");

			if (enableSources.Count > 0 && !action.enabled && (m_CurrentActionsMask?.Contains(action) ?? true)) {
				UnityEngine.Debug.LogError($"Trying to disable input action \"{action.name}\" by {source}, but it is already disabled. Some code is disabling input actions without the IInputContext!", source as UnityEngine.Object);
			}

			enableSources.Remove(source);

			if (enableSources.Count == 0) {
				action.Disable();
			}
		}

		/// <summary>
		/// Disable all input actions enabled by the provided source object.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public void Disable(object source)
		{
			if (m_Disposed)
				return;

			if (source == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Disabling by of type source InputAction is not allowed.");

			foreach (var pair in m_Actions) {
				if (pair.Value.Remove(source) && pair.Value.Count == 0) {
					pair.Key.Disable();
				}
			}
		}

		/// <summary>
		/// Returns all input actions enabled by specified source.
		/// </summary>
		public IEnumerable<InputAction> GetInputActionsEnabledBy(object source)
		{
			if (source == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Requests with source type InputAction is not allowed.");

			foreach (var pair in m_Actions) {
				if (pair.Value.Contains(source)) {
					yield return pair.Key;
				}
			}
		}

		/// <summary>
		/// Get all sources that enabled specific action.
		/// </summary>
		public IEnumerable<object> GetEnablingSourcesFor(InputAction action)
		{
			if (m_Actions.TryGetValue(action, out HashSet<object> enablingSources))
				return enablingSources;

			return Array.Empty<object>();
		}

		/// <summary>
		/// Is the specified action enabled by the provided source.
		/// </summary>
		public bool IsEnabledBy(object source, InputAction action)
		{
			if (source == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Requests with source type InputAction is not allowed.");

			if (m_Actions.TryGetValue(action, out HashSet<object> enablingSources))
				return enablingSources.Contains(source);

			return false;

		}

		/// <summary>
		/// Push actions mask filtering in actions allowed to be enabled.
		/// If mask is added or set to the top of the stack it will be applied immediately disabling any actions not included.
		/// </summary>
		public void PushOrSetActionsMask(object source, IEnumerable<InputAction> actionsMask, bool setBackToTop = false)
		{
			if (m_Disposed)
				return;

			if (source == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Disabling by of type source InputAction is not allowed.");

			foreach (StateSourceBind pair in m_MasksStack) {
				if (pair.Source == source) {
					pair.MaskActions = actionsMask.ToHashSet();

					if (setBackToTop) {
						// Should work, as we stop iterating right away.
						m_MasksStack.Remove(pair);
						break;
					}

					if (pair == m_MasksStack.Last()) {
						ForceRefreshInputActionStates();
					}
					return;
				}
			}

			m_MasksStack.Add(new StateSourceBind {
				Source = source,
				MaskActions = actionsMask.ToHashSet(),
			});
			ForceRefreshInputActionStates();
		}

		/// <summary>
		/// Remove actions mask by the source it pushed it.
		/// Removing it will restore the actions state according to the next mask in the stack or their original tracked state.
		/// </summary>
		public void PopActionsMask(object source)
		{
			if (m_Disposed)
				return;

			if (source == null)
				throw new ArgumentNullException();

			if (source is InputAction)
				throw new ArgumentException("Disabling by of type source InputAction is not allowed.");

			for (int i = 0; i < m_MasksStack.Count; ++i) {
				StateSourceBind pair = m_MasksStack[i];

				if (pair.Source == source) {
					bool wasActive = i == m_MasksStack.Count - 1;

					m_MasksStack.RemoveAt(i);

					if (wasActive) {
						ForceRefreshInputActionStates();
					}
					return;
				}
			}
		}

		/// <summary>
		/// Should action be enabled according to the tracked state or currently active actions mask.
		/// </summary>
		public bool ShouldActionBeEnabled(InputAction action)
		{
			if (m_MasksStack.Count > 0) {
				if (m_CurrentActionsMask.Contains(action)) {
					return m_Actions[action].Count > 0;
				} else {
					return false;
				}
			} else {
				return m_Actions[action].Count > 0;
			}
		}

		/// <summary>
		/// Returns a list of conflicting InputActions and the sources that enabled them.
		/// </summary>
		public InputActionConflictsReport GetConflictingActionRequests(IEnumerable<InputAction> uiActions)
		{
			var conflictsReport = new InputActionConflictsReport();

			foreach(var pair in m_Actions) {

				// If no sources but action is enabled means some code is enabling actions without the IInputContext.
				if (pair.Value.Count == 0 && pair.Key.enabled) {
					conflictsReport.IllegalActions.Add(pair.Key);
					continue;
				}

				if (pair.Value.Count <= 1)
					continue;

				// If there are two users, one from the player state, another from the mask (i.e. modal dialog), it is ok to have conflicts.
				// At least for the UI actions.
				// Current mask can be empty (e.g. loading screen supressing all actions, including of a modal dialog left in the background to be destroyed by the next scene)
				if (pair.Value.Count == 2 && m_MasksStack.Count > 0 /*&& m_CurrentActionsMask.Contains(pair.Key)*/ && uiActions.Contains(pair.Key))
					continue;

				conflictsReport.Conflicts.Add(KeyValuePair.Create<InputAction, IReadOnlyCollection<object>>(pair.Key, pair.Value));
			}

			return conflictsReport;
		}

		/// <summary>
		/// Cancel all enable requests and disable all input actions.
		/// </summary>
		public void ForceClearAllEnableRequests(IEnumerable<InputAction> exclude = null)
		{
			foreach(var pair in m_Actions) {
				if (exclude != null && exclude.Contains(pair.Key))
					continue;

				pair.Key.Disable();
				pair.Value.Clear();
			}
		}

		/// <summary>
		/// Reapply the input actions state according to the known tracked one or the currently active mask.
		/// </summary>
		public void ForceRefreshInputActionStates()
		{
			foreach(var pair in m_Actions) {
				InputAction action = pair.Key;
				HashSet<object> enableSources = pair.Value;

				if (m_MasksStack.Count > 0) {

					if (m_CurrentActionsMask.Contains(action)) {

						if (enableSources.Count > 0) {
							action.Enable();
						} else {
							action.Disable();
						}

					} else {
						action.Disable();
					}

				} else {

					if (enableSources.Count > 0) {
						action.Enable();
					} else {
						action.Disable();
					}
				}
			}
		}
	}
}
#endif