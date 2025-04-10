using System;
using DevLocker.GFrame.Input.Contexts;
using DevLocker.GFrame.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// When this component is enabled, it will set this object as selected in the Unity event system.
	/// </summary>
	public class SelectionController : MonoBehaviour
	{
		public enum PersistSelectionActionType
		{
			DoNotPersist = 0,
			AlwaysPersist = 1,
			PersistOnlyIfObjectIsActiveInHierarchy = 4,
		};

		public enum StartSelectionSourceTypes
		{
			Selectables = 0,
			NavigationGroups = 4,
		}

		[Serializable]
		public class ExtraSettingsType
		{
			[Tooltip("Should navigation selection persist while using the mouse or should it automatically deselect the object.")]
			public IInputContext.InputBehaviourOverride MouseSupportsUINavigationSelection;
			[Tooltip("Set selected object to none if the current device doesn't support it. E.g. hide selection for mouse & keyboard, but show it for Gamepad.\nCheck the device InputBindingDisplayAsset.")]
			public IInputContext.InputBehaviourOverride RemoveSelectionIfDeviceDoesntSupportIt;

			[Tooltip("Should it consider only selections that are child of this component for persistent or last selected object?")]
			public bool TrackOnlyChildren = true;

			public bool ClearSelectionOnDisable = true;
		}

		public StartSelectionSourceTypes StartSelectionSource = StartSelectionSourceTypes.Selectables;

		[Tooltip("Select the first interactable object from the managed selectables of this navigation group.")]
		public List<UINavigationGroup> StartNavigationGroups;

		[Tooltip("Select the first interactable object on enable.")]
		public List<Selectable> StartSelections;

		[Tooltip("Persist selection even if component or object is disabled.")]
		public PersistSelectionActionType PersistentSelection = PersistSelectionActionType.PersistOnlyIfObjectIsActiveInHierarchy;

		public ExtraSettingsType ExtraSettings = new ExtraSettingsType();

		private GameObject m_PersistedSelection = null;
		private Selectable m_PersistedSelectable;	// Not sure if having selected object without selectable component is possible.
		private bool m_PersistedIsAvailable => m_PersistedSelectable
			? m_PersistedSelectable.IsInteractable() && m_PersistedSelectable.isActiveAndEnabled
			: m_PersistedSelection != null && m_PersistedSelection.activeInHierarchy
		;

		/// <summary>
		/// Is controller about to do selection on the next update.
		/// </summary>
		public bool IsSelectRequested { get; private set; }
		private bool m_ControlSchemeMatched = true;

		// Only one should be active per context. It's a list to avoid issues while changing active scopes.
		private static Dictionary<PlayerContextUIRootObject, List<SelectionController>> s_ActiveInstances = new Dictionary<PlayerContextUIRootObject, List<SelectionController>>();

		private List<CanvasGroup> m_CanvasGroups = new List<CanvasGroup>();

		// What was the selected object when I enabled.
		private GameObject m_SelectedObjectOnEnable;

		// Used for multiple event systems (e.g. split screen).
		protected IPlayerContext m_PlayerContext;

		protected bool m_HasInitialized = false;

		/// <summary>
		/// Find the first active and interactable selectable from <see cref="StartNavigationGroups"/> or <see cref="StartSelections"/>.
		/// </summary>
		public virtual Selectable GetStartSelection()
		{
			switch(StartSelectionSource) {
				case StartSelectionSourceTypes.Selectables:
					if (StartSelections == null)
						return null;

					foreach(Selectable selectable in StartSelections) {
						if (selectable && selectable.isActiveAndEnabled && selectable.IsInteractable())
							return selectable;
					}
					break;

				case StartSelectionSourceTypes.NavigationGroups:
					if (StartNavigationGroups == null)
						return null;

					foreach (UINavigationGroup navigationGroup in StartNavigationGroups) {

						if (navigationGroup.FirstSelectable)
							return navigationGroup.FirstSelectable;

						foreach (Selectable selectable in navigationGroup.ManagedSelectables) {
							if (selectable && selectable.isActiveAndEnabled && selectable.IsInteractable())
								return selectable;
						}
					}
					break;
			}


			return null;
		}

		/// <summary>
		/// Set the persistant selectable for this controller.
		/// If set to null then the start selection will be used.
		///
		/// Also check <see cref="PersistentSelection"/>.
		/// </summary>
		/// <param name="selectable"></param>
		public void SetPersistantSelection(Selectable selectable)
		{
			m_PersistedSelectable = selectable;
			m_PersistedSelection = selectable != null ? selectable.gameObject : null;
			m_PlayerContext?.SetSelectedGameObject(m_PersistedSelection);
		}

		public virtual bool IsInStartSelection(Selectable selectable)
		{
			switch(StartSelectionSource) {
				case StartSelectionSourceTypes.Selectables:
					return StartSelections != null && StartSelections.IndexOf(selectable) != -1;

				case StartSelectionSourceTypes.NavigationGroups:
					if (StartNavigationGroups == null)
						return false;

					foreach (UINavigationGroup navigationGroup in StartNavigationGroups) {
						foreach (Selectable startSelectable in navigationGroup.ManagedSelectables) {
							if (startSelectable == selectable)
								return true;
						}
					}
					break;
			}

			return false;
		}

		/// <summary>
		/// Get the active instance for the specified player context (i.e. singleton).
		/// </summary>
		public static SelectionController GetActiveInstanceFor(PlayerContextUIRootObject playerContext)
		{
			s_ActiveInstances.TryGetValue(playerContext, out List<SelectionController> instance);

			return instance?.FirstOrDefault();
		}

		protected virtual void Reset()
		{
			var navigation = GetComponent<UINavigationGroup>();
			if (navigation) {
				StartSelectionSource = StartSelectionSourceTypes.NavigationGroups;
				StartNavigationGroups = new List<UINavigationGroup>();
				StartNavigationGroups.Add(navigation);
			}
		}

		protected virtual void Awake()
		{
			m_PlayerContext = PlayerContextUtils.GetPlayerContextFor(gameObject);

			m_PlayerContext.AddSetupCallback((delayedSetup) => {
				m_HasInitialized = true;

				if (delayedSetup && isActiveAndEnabled) {
					OnEnable();
				}
			});
		}

		protected virtual void OnDestroy()
		{
			// Remove references for easier memory profiling and debugging. NOTE: if object was never awaken, this won't get executed.
			StartSelections.Clear();
			StartNavigationGroups.Clear();

			m_PersistedSelectable = null;
			m_PersistedSelection = null;

			m_SelectedObjectOnEnable = null;

			m_CanvasGroups = null;
			m_PlayerContext = null;
		}

		protected virtual void OnEnable()
		{
			if (!m_HasInitialized)
				return;

			IsSelectRequested = true;

			m_SelectedObjectOnEnable = m_PlayerContext.SelectedGameObject;

			s_ActiveInstances.TryGetValue(m_PlayerContext.GetRootObject(), out List<SelectionController> activeInstances);
			if (activeInstances == null) {
				activeInstances = new List<SelectionController>(2);
				s_ActiveInstances.Add(m_PlayerContext.GetRootObject(), activeInstances);
			}
			activeInstances.Add(this);

			CollectParentCanvasGroups();
		}

		protected virtual void OnDisable()
		{
			if (!m_HasInitialized)
				return;

			s_ActiveInstances.TryGetValue(m_PlayerContext.GetRootObject(), out List<SelectionController> activeInstances);
			if (activeInstances != null && activeInstances.Contains(this)) {
				activeInstances.Remove(this);
				if (activeInstances.Count == 0) {
					s_ActiveInstances.Remove(m_PlayerContext.GetRootObject());
				}
			}

			if (ExtraSettings.ClearSelectionOnDisable && m_PlayerContext.IsActive) {
				TryClearSelection();
			}

			if (PersistentSelection == PersistSelectionActionType.PersistOnlyIfObjectIsActiveInHierarchy && !gameObject.activeInHierarchy) {
				m_PersistedSelection = null;
				m_PersistedSelectable = null;
			}
		}

		void Update()
		{
			if (!m_PlayerContext.IsActive || !m_HasInitialized)
				return;

			bool removeSelectionIfDeviceDoesntSupportIt = false;
			if (m_PlayerContext.InputContext != null) {
				removeSelectionIfDeviceDoesntSupportIt = m_PlayerContext.InputContext.DefaultBehaviours.RemoveSelectionIfDeviceDoesntSupportIt;
				removeSelectionIfDeviceDoesntSupportIt = ExtraSettings.RemoveSelectionIfDeviceDoesntSupportIt.FinalValue(removeSelectionIfDeviceDoesntSupportIt);

				m_ControlSchemeMatched = m_PlayerContext.InputContext.DeviceSupportsUINavigationSelection;
				if (m_PlayerContext.InputContext.GetLastUsedInputDevice() is Mouse mouse) {

					var mouseSupport = m_PlayerContext.InputContext.DefaultBehaviours.MouseSupportsUINavigationSelection;
					mouseSupport = ExtraSettings.MouseSupportsUINavigationSelection.FinalValue(mouseSupport);

					if (!mouseSupport) {

						// Is dragging some object (for example slider). In that case don't deselect.
						bool isDragging = mouse.leftButton.isPressed
							&& m_PlayerContext.SelectedGameObject
							&& m_PlayerContext.SelectedGameObject.GetComponent<UnityEngine.EventSystems.IDragHandler>() != null
							;

						m_ControlSchemeMatched = isDragging;
					}
				}
			}

			// Wait till clickable to work with selection.
			if (!IsClickable()) {
				if (!m_ControlSchemeMatched && removeSelectionIfDeviceDoesntSupportIt && m_PlayerContext.SelectedGameObject) {
					TryClearSelection();
				}

				if (ExtraSettings.ClearSelectionOnDisable && m_PlayerContext.SelectedGameObject) {
					TryClearSelection();
				}

				return;
			}

			if (IsSelectRequested) {

				if (!ExtraSettings.TrackOnlyChildren && UIUtils.IsLayoutRebuildPending())
					return;

				if (ExtraSettings.TrackOnlyChildren && UIUtils.IsLayoutRebuildPendingUnder(transform))
					return;

				// Call this on update, to avoid errors while switching active object (turn on one, turn off another). In the end, only one should be active.
				s_ActiveInstances.TryGetValue(m_PlayerContext.GetRootObject(), out List<SelectionController> activeInstances);
				if (activeInstances == null || activeInstances.Count != 1 || !activeInstances.Contains(this)) {
					Debug.LogError($"[Input] There are two or more {nameof(SelectionController)} instances active at the same time - this is not allowed. Currently active: {string.Join(", ", activeInstances?.Select(s => s.name) ?? Array.Empty<string>())}. Selection requested for: \"{name}\"", this);
				}

#if USE_INPUT_SYSTEM
				// Hotkeys subscribe for the InputAction.perform event, which executes on key press / down,
				// while "Submit" action of the InputSystemUIInputModule runs on key release / up.
				// This makes hotkey being executed, new screen scope shown and executing the newly selected button on release.
				// We don't want that so wait till submit action is no more pressed.
				var inputModule = m_PlayerContext.EventSystem.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
				var submitAction = inputModule?.submit?.action;
				if (submitAction != null && submitAction.IsPressed())
					return;
#endif

				IsSelectRequested = false;

				// If user changed selection during my select request don't override their choice (unless it's outside the my scope).
				if (m_SelectedObjectOnEnable != m_PlayerContext.SelectedGameObject && m_PlayerContext.SelectedGameObject && m_PlayerContext.SelectedGameObject.activeInHierarchy)
					if (!ExtraSettings.TrackOnlyChildren || m_PlayerContext.SelectedGameObject.transform.IsChildOf(transform))
						return;

				GameObject targetSelection = (PersistentSelection > 0 && m_PersistedIsAvailable)
					? m_PersistedSelection
					: GetStartSelection()?.gameObject // Null-check is safe.
					;

				if (targetSelection && !targetSelection.activeInHierarchy) {
					Debug.LogWarning($"[Input] {name} {nameof(SelectionController)} is trying to select inactive object!", this);
				}

				if (m_ControlSchemeMatched) {

					// If UI was deactivated but selection didn't change, activating it back will leave the button selected but not highlighted.
					if (m_PlayerContext.SelectedGameObject == targetSelection) {
						m_PlayerContext.SetSelectedGameObject(null);
					}

					m_PlayerContext.SetSelectedGameObject(targetSelection);

					m_PersistedSelection = m_PlayerContext.SelectedGameObject;
					m_PersistedSelectable = m_PersistedSelection ? m_PersistedSelection.GetComponent<Selectable>() : null;

					OnSelected();

				} else {

					m_PersistedSelection = targetSelection;
					m_PersistedSelectable = m_PersistedSelection ? m_PersistedSelection.GetComponent<Selectable>() : null;
				}

			} else {

				if (m_ControlSchemeMatched) {
					GameObject selectedObject = m_PlayerContext.SelectedGameObject;
					Selectable selectable = selectedObject ? selectedObject.GetComponent<Selectable>() : null;
					if (selectedObject && selectedObject.activeInHierarchy && (selectable == null || selectable.IsInteractable())) {

						if (!ExtraSettings.TrackOnlyChildren
							|| selectable && IsInStartSelection(selectable)
							|| m_PlayerContext.SelectedGameObject.transform.IsChildOf(transform)
							) {
							m_PersistedSelection = m_PlayerContext.SelectedGameObject;
							m_PersistedSelectable = m_PersistedSelection ? m_PersistedSelection.GetComponent<Selectable>() : null;
						}

					// Selectable exists, but got deactivated. Check if neighbour selectables are availble to auto-select.
					} else if (selectable) {
						Selectable bestSelectable = null;
						var neighbourSelectables = new Selectable[] {
							selectable.navigation.selectOnDown,
							selectable.navigation.selectOnUp,
							selectable.navigation.selectOnRight,
							selectable.navigation.selectOnLeft,
						};

						foreach(Selectable neighbourSelectable in neighbourSelectables) {
							if (neighbourSelectable && neighbourSelectable.gameObject.activeInHierarchy) {
								bestSelectable = neighbourSelectable;
								break;
							}
						}

						if (bestSelectable) {
							m_PlayerContext.SetSelectedGameObject(bestSelectable.gameObject);
							OnSelected();

						} else {
							DoNoSelectionAction();
						}
					} else {
						DoNoSelectionAction();
					}
				} else {
					if (removeSelectionIfDeviceDoesntSupportIt && m_PlayerContext.SelectedGameObject) {
						TryClearSelection();
					}
				}
			}
		}

		private void DoNoSelectionAction()
		{
			if (m_PlayerContext.InputContext == null)	// Not sure about this.
				return;

			if (m_PlayerContext.InputContext.DefaultBehaviours.AllowEmptyClicksToDeselect
				&& m_PlayerContext.InputContext.GetLastUsedInputDevice() is Mouse) {
				// Do nothing.
			} else {
				GameObject startObject = GetStartSelection()?.gameObject; // Null-check is safe.
				m_PlayerContext.SetSelectedGameObject(m_PersistedIsAvailable ? m_PersistedSelection : startObject);
			}
		}

		private void TryClearSelection()
		{
			if (!m_PlayerContext.IsTextFieldFocused()) {
				m_PlayerContext.SetSelectedGameObject(null);
			}
		}

		protected virtual void OnSelected() { }

		private void CollectParentCanvasGroups()
		{
			m_CanvasGroups.Clear();

			var group = gameObject.GetComponentInParent<CanvasGroup>(true);

			while (group) {
				m_CanvasGroups.Add(group);

				if (group.ignoreParentGroups)
					break;

				Transform parent = group.transform.parent;
				if (parent == null)
					break;

				group = group.transform.parent.GetComponentInParent<CanvasGroup>(true);
			}
		}

		private bool IsClickable()
		{
			foreach(CanvasGroup group in m_CanvasGroups) {
				if (group == null || !group.enabled)
					continue;

				if (!group.interactable || !group.blocksRaycasts)
					return false;
			}

			return true;
		}

		protected virtual void OnValidate()
		{
			switch(StartSelectionSource) {
				case StartSelectionSourceTypes.Selectables:
					if (StartSelections != null) {
						// Having no start selection is valid if selectables will be created dynamically.
						foreach (Selectable selectable in StartSelections) {
							if (selectable == null) {
								Debug.LogError($"[Input] {name} {nameof(SelectionController)} has missing start selection.", this);
							}
						}
					}
					break;

				case StartSelectionSourceTypes.NavigationGroups:
					if (StartNavigationGroups != null) {
						// Having no start selection is valid if selectables will be created dynamically.
						foreach (UINavigationGroup navGroup in StartNavigationGroups) {
							if (navGroup == null) {
								Debug.LogError($"[Input] {name} {nameof(SelectionController)} has missing start selection.", this);
							}
						}
					}
					break;
			}

		}
	}


#if UNITY_EDITOR
	[UnityEditor.CustomEditor(typeof(SelectionController), true)]
	[UnityEditor.CanEditMultipleObjects]
	internal class SelectionControllerEditor : UnityEditor.Editor
	{

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			UnityEditor.EditorGUI.BeginDisabledGroup(true);
			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
			UnityEditor.EditorGUI.EndDisabledGroup();

			var startSelectionSource = serializedObject.FindProperty(nameof(SelectionController.StartSelectionSource));
			UnityEditor.EditorGUILayout.PropertyField(startSelectionSource);

			var sourceType = (SelectionController.StartSelectionSourceTypes) startSelectionSource.intValue;

			switch(sourceType) {
				case SelectionController.StartSelectionSourceTypes.Selectables:
					UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SelectionController.StartSelections)));
					break;
				case SelectionController.StartSelectionSourceTypes.NavigationGroups:
					UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SelectionController.StartNavigationGroups)));
					break;
				default:
					Debug.LogError($"Unknown type {sourceType}", target);
					break;
			}

			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SelectionController.PersistentSelection)));

			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SelectionController.ExtraSettings)));

			serializedObject.ApplyModifiedProperties();
		}
	}
#endif
}