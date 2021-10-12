using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Marks scope elements to be controlled by the UIScope.
	/// </summary>
	public interface IScopeElement
	{
		bool enabled { get; set; }
	}

#if USE_INPUT_SYSTEM
	/// <summary>
	/// Used with Unity InputSystem for pushing and popping states in the InputActionsStack.
	/// </summary>
	public interface IHotkeyWithInputAction
	{
		IEnumerable<UnityEngine.InputSystem.InputAction> GetUsedActions();

		bool CheckIfAnyActionIsEnabled();
	}
#endif

	/// <summary>
	/// Used to skip hotkeys in some cases.
	/// </summary>
	[Flags]
	public enum SkipHotkeyOption
	{
		InputFieldTextFocused = 1 << 0,
		NonTextSelectableFocused = 1 << 1,
	}

	/// <summary>
	/// When working with more hotkeys, selections, etc. on screen, some conflicts may arise.
	/// For example:
	/// > "Back" hotkey element is attached on the displayed menu and another "Back" hotkey attached to the "Yes/No" pop up, displayed on top.
	/// > The usual solution is to invoke only the last enabled hotkey instead of all of them.
	/// UIScope groups all child scope elements into a single group. The last enabled UIScope is focused - it and all its parent scopes are activated, while the rest will be disabled.
	/// </summary>
	[SelectionBase]
	public class UIScope : MonoBehaviour
	{
		public enum OnEnablePolicy
		{
			Focus = 0,
			FocusWithFramePriority = 2,	// If multiple activations
			FocusIfCurrentIsLowerDepth = 5,
			FocusIfCurrentIsLowerOrEqualDepth = 7,
			DontFocus = 20,
		}

		public enum OnDisablePolicy
		{
			FocusScopeWithHighestDepth = 0,
			//FocusPreviousScope = 5, Someday... decide what to do if prev is inactive and what happens if gets disabled the same frame as well.
			FocusParentScope = 10,
			FocusFirstEnabledScopeFromList = 15,
			EmptyFocus = 20,
		}

		[Tooltip("What should happen when this scope gets enabled.\n\nFocusWithFramePriority - use when multiple scopes get enabled in the same frame to prioritize this one.")]
		public OnEnablePolicy OnEnableBehaviour;
		[Tooltip("What should happen when this scope gets disabled ONLY if this is the FOCUSED (deepest) one.")]
		public OnDisablePolicy OnDisableBehaviour;

		public List<UIScope> OnDisableScopes;

#if USE_INPUT_SYSTEM
		[Tooltip("Reset all input actions.\nThis will interrupt their progress and any gesture, drag, sequence will be canceled.")]
		public bool ResetAllActionsOnEnable = true;

		[Space]
		[Tooltip("When scope gets enabled this will activate the input actions (hotkeys) that are used by the scope elements under it.\nOn disabling it will deactivate the actions.\nNOTE: to avoid input conflicts, don't control the same actions from the code.")]
		public bool EnableUsedInputActions = true;
		[Tooltip("Use this for modal windows to suppress background hotkeys.\n\nPushes a new input state in the stack.\nOn deactivating, will pop this state and restore the previous one.\nThe only enabled actions will be the used ones by (under) this scope.")]
		public bool PushInputStack = false;
		[Tooltip("Enable the UI actions with the scope ones, after pushing the new input state.")]
		public bool IncludeUIActions = true;
#endif

		private int m_FrameEnabled = -1;
		private int m_ScopeDepth;

		/// <summary>
		/// Focused scope which keeps it and all its parents active (and the rest will be inactive).
		/// </summary>
		public static UIScope FocusedScope => m_ActiveScopes.LastOrDefault();

		/// <summary>
		/// The focused scope plus all it's parents - top to bottom.
		/// </summary>
		public static IReadOnlyCollection<UIScope> ActiveScopes => m_ActiveScopes;

		private static UIScope[] m_ActiveScopes = Array.Empty<UIScope>();

		private static List<UIScope> s_Scopes = new List<UIScope>();

		public IReadOnlyList<IScopeElement> OwnedElements => m_ScopeElements;
		public IReadOnlyList<UIScope> DirectChildScopes => m_DirectChildScopes;

		private List<IScopeElement> m_ScopeElements = new List<IScopeElement>();
		private List<UIScope> m_DirectChildScopes = new List<UIScope>();

		private bool m_HasScannedForElements = false;

		// Switching scopes may trigger user code that may switch scopes indirectly, while already doing so.
		// Any such change will be pushed to a queue and applied later on.
		private static bool s_ChangingActiveScopes = false;
		private static Queue<KeyValuePair<UIScope, bool>> s_PendingScopeChanges = new Queue<KeyValuePair<UIScope, bool>>();

		private bool m_GameQuitting = false;

		protected virtual void Awake()
		{
			if (!m_HasScannedForElements) {
				ScanForChildScopeElements();
			}
		}

		void OnApplicationQuit()
		{
			m_GameQuitting = true;
		}

		public virtual void OnValidate()
		{
			for(int i = 0; i < OnDisableScopes.Count; ++i) {
				if (OnDisableScopes[i] == null) {
					Debug.LogError($"\"{name}\" has missing scope in {nameof(OnDisableScopes)} list at scene \"{gameObject.scene.name}\"", this);
				}
			}
		}

		void OnEnable()
		{
			m_FrameEnabled = Time.frameCount;

			if (s_ChangingActiveScopes) {
				s_PendingScopeChanges.Enqueue(new KeyValuePair<UIScope, bool>(this, true));
				return;
			}

			// Child scope was active, but this one was disabled. The user just enabled me.
			// Re-insert me (us) to the collections keeping the correct order.
			if (m_ActiveScopes.Length > 0 && m_ActiveScopes.Last().transform.IsChildOf(transform)) {

				// That would include me, freshly enabled.
				UIScope[] nextScopes = CollectScopes(m_ActiveScopes.Last());

				for (int i = 0; i < nextScopes.Length; ++i) {
					UIScope scope = nextScopes[i];
					scope.m_ScopeDepth = i; // Always set this in case hierarchy changed in the mean time.
				}

				if (!s_Scopes.Contains(this)) {
					s_Scopes.Add(this);
				}

				SwitchActiveScopes(ref m_ActiveScopes, nextScopes);

			} else {

				// OnEnabled() order of execution is undefined - sometimes parent invoked first, sometimes the children.
				// Ensure that collections don't have any duplicates and are filled in the right order - parent to child.
				UIScope[] nextScopes = CollectScopes(this);

				for(int i = 0; i < nextScopes.Length; ++i) {
					UIScope scope = nextScopes[i];
					scope.m_ScopeDepth = i;	// Always set this in case hierarchy changed in the mean time.

					if (!s_Scopes.Contains(scope)) {
						s_Scopes.Add(scope);
					}
				}

				UIScope lastActive = m_ActiveScopes.LastOrDefault();

				switch (OnEnableBehaviour) {
					case OnEnablePolicy.Focus:
						// Skip activation if this frame another scope was activated with higher priority.
						if (!m_ActiveScopes.Any(s => s.m_FrameEnabled == m_FrameEnabled && s.OnEnableBehaviour == OnEnablePolicy.FocusWithFramePriority)) {
							SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
						}
						break;

					case OnEnablePolicy.FocusWithFramePriority:
						SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
						break;

					case OnEnablePolicy.FocusIfCurrentIsLowerDepth:
						if (lastActive == null || lastActive.m_ScopeDepth < m_ScopeDepth) {
							SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
						}
						break;

					case OnEnablePolicy.FocusIfCurrentIsLowerOrEqualDepth:
						if (lastActive == null || lastActive.m_ScopeDepth <= m_ScopeDepth) {
							SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
						}
						break;

					case OnEnablePolicy.DontFocus:
						break;

					default:
						throw new NotSupportedException(OnEnableBehaviour.ToString());
				}
			}
		}

		void OnDisable()
		{
			m_FrameEnabled = -1;

			if (s_ChangingActiveScopes) {
				s_PendingScopeChanges.Enqueue(new KeyValuePair<UIScope, bool>(this, false));
				return;
			}

			s_Scopes.Remove(this);

			// HACK: On turning off the game OnDisable() gets called which may call methods on destroyed objects.
			int activeIndex = Array.IndexOf(m_ActiveScopes, this);
			if (activeIndex != -1 && !m_GameQuitting) {

				// Proceed only if this is the deepest scope (i.e. the focused one).
				if (activeIndex != m_ActiveScopes.Length - 1) {
					var activeScopes = CollectScopes(m_ActiveScopes.Last());
					SwitchActiveScopes(ref m_ActiveScopes, activeScopes);
					return;
				}

				// Something else just activated, don't change the focus.
				foreach(UIScope scope in s_Scopes) {
					if (scope.m_FrameEnabled == Time.frameCount) {
						return;
					}
				}

				UIScope nextScope = null;
				UIScope[] nextScopes;

				switch (OnDisableBehaviour) {
					case OnDisablePolicy.FocusParentScope:
						Transform scopeTransform = transform.parent;
						while(scopeTransform) {
							UIScope scope = scopeTransform.GetComponent<UIScope>();
							if (scope && scope.isActiveAndEnabled) {
								nextScope = scope;
								break;
							}

							scopeTransform = scopeTransform.parent;
						}
						break;

					case OnDisablePolicy.FocusScopeWithHighestDepth:
						nextScope = s_Scopes.FirstOrDefault();
						foreach(UIScope scope in s_Scopes) {
							if (nextScope.m_ScopeDepth < scope.m_ScopeDepth && scope.isActiveAndEnabled) {
								nextScope = scope;
							}
						}
						break;

					case OnDisablePolicy.FocusFirstEnabledScopeFromList:
						nextScope = OnDisableScopes.FirstOrDefault(s => s.isActiveAndEnabled);
						break;

					case OnDisablePolicy.EmptyFocus:
						nextScope = null;
						break;

					default:
						throw new NotSupportedException(OnDisableBehaviour.ToString());
				}

				nextScopes = nextScope
					? CollectScopes(nextScope)
					: Array.Empty<UIScope>()
					;

				SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
			}
		}

		/// <summary>
		/// Deactivate the current active scopes and reactivate them back, forcing full reinitialization.
		/// NOTE: This will not rescan for new ScopeElements.
		/// </summary>
		public static void RefocusActiveScopes()
		{
			if (m_ActiveScopes.Length == 0)
				return;

			var lastActive = m_ActiveScopes.Last();

			if (s_ChangingActiveScopes) {
				s_PendingScopeChanges.Enqueue(new KeyValuePair<UIScope, bool>(lastActive, true));
				return;
			}

			// Force full re-initialization of all the scopes.
			SwitchActiveScopes(ref m_ActiveScopes, new UIScope[0]);
			lastActive.ForceRefocusScope();
		}

		/// <summary>
		/// Force selected scope to be active, instead of the last enabled.
		/// </summary>
		[ContextMenu("Force activate scope")]
		public void ForceRefocusScope()
		{
			if (s_ChangingActiveScopes) {
				s_PendingScopeChanges.Enqueue(new KeyValuePair<UIScope, bool>(this, true));
				return;
			}

			// That would be weird.
			if (!gameObject.activeInHierarchy) {
				Debug.LogWarning($"Trying to force activate UIScope {name}, but it is not active in the hierarchy. Abort!", this);
				return;
			}

			UIScope[] nextScopes = CollectScopes(this);

			SwitchActiveScopes(ref m_ActiveScopes, nextScopes);
		}

		public static bool IsScopeActive(UIScope scope) => m_ActiveScopes.Contains(scope);

		/// <summary>
		/// Call this if you changed your UI hierarchy and expect added or removed scope elements.
		/// </summary>
		[ContextMenu("Rescan for child scope elements")]
		public void ScanForChildScopeElements()
		{
			m_ScopeElements.Clear();
			m_DirectChildScopes.Clear();
			ScanForChildScopeElements(this, transform, m_ScopeElements, m_DirectChildScopes);
			m_HasScannedForElements = true;

			if (Array.IndexOf(m_ActiveScopes, this) != -1) {
				var lastActive = m_ActiveScopes.Last();

				if (s_ChangingActiveScopes) {
					s_PendingScopeChanges.Enqueue(new KeyValuePair<UIScope, bool>(lastActive, true));
					return;
				}

				// Force full re-initialization of all the scopes including this one.
				SwitchActiveScopes(ref m_ActiveScopes, new UIScope[0]);
				lastActive.ForceRefocusScope();

			} else {
				foreach(IScopeElement scopeElement in m_ScopeElements) {
					scopeElement.enabled = false;
				}
			}
		}

		public bool Owns(IScopeElement scopeElement)
		{
			return m_ScopeElements.Contains(scopeElement);
		}

		internal static void ScanForChildScopeElements(UIScope parentScope, Transform transform, List<IScopeElement> scopeElements, List<UIScope> directChildScopes)
		{
			var scope = transform.GetComponent<UIScope>();
			// Another scope begins, it will handle its own child hotkey elements.
			if (scope && parentScope != scope) {
				directChildScopes.Add(scope);
				return;
			}

			scopeElements.AddRange(transform.GetComponents<IScopeElement>());

			foreach(Transform child in transform) {
				ScanForChildScopeElements(parentScope, child, scopeElements, directChildScopes);
			}
		}

		protected static UIScope[] CollectScopes(Component target)
		{
			return target
				.GetComponentsInParent<UIScope>(true)	// Collect all so they get deactivated properly later on.
				.Reverse()
				.Where(s => s.enabled)
				.ToArray();
		}

		protected static void SwitchActiveScopes(ref UIScope[] prevScopes, UIScope[] nextScopes)
		{
			// Switching scopes may trigger user code that may switch scopes indirectly, while already doing so.
			// Any such change will be pushed to a queue and applied later on.
			// TODO: This was never really tested.
			s_ChangingActiveScopes = true;

			try {
				// Reversed order, just in case.
				foreach (UIScope scope in prevScopes.Reverse()) {
					if (!nextScopes.Contains(scope)) {
						scope.SetScopeState(false);
					}
				}

				foreach (UIScope scope in nextScopes) {
					if (!prevScopes.Contains(scope)) {
						scope.SetScopeState(true);
					}
				}
			}

			finally {
				prevScopes = nextScopes;
				s_ChangingActiveScopes = false;
			}

			while(s_PendingScopeChanges.Count > 0) {
				var scopeChange = s_PendingScopeChanges.Dequeue();

				if (scopeChange.Value) {
					scopeChange.Key.OnEnable();
				} else {
					scopeChange.Key.OnDisable();
				}
			}
		}

		protected virtual void SetScopeState(bool active)
		{
			// If this scope isn't still initialized, do it now, or no elements will be enabled.
			// This happens when child scope tries to activate the parent scope for the first time, while the parent was still inactive.
			if (!m_HasScannedForElements) {
				ScanForChildScopeElements();
			}

			foreach(IScopeElement scopeElement in m_ScopeElements) {
				scopeElement.enabled = active;
			}

			ProcessInput(active);
		}

		protected void ProcessInput(bool active)
		{
#if USE_INPUT_SYSTEM
			var context = Input.InputContextManager.InputContext;

			if (context == null) {
				Debug.LogWarning($"{nameof(UIScope)} {name} can't be used if Unity Input System is not provided.", this);
				return;
			}

			// Pushing input on stack will reset the actions anyway.
			if (ResetAllActionsOnEnable && active && !PushInputStack) {

				// Resets all enabled actions. This will interrupt their progress and any gesture, drag, sequence will be canceled.
				// Useful on changing states or scopes, so gestures, drags, sequences don't leak in.
				foreach (var action in context.GetAllActionsFor(Input.PlayerIndex.AnyPlayer)) {
					if (action.enabled) {
						action.Reset();
					}
				}
			}

			if (active) {

				if (PushInputStack) {
					context.PushActionsState(this);

					if (IncludeUIActions) {
						foreach (var action in context.GetUIActions()) {
							action.Enable();
						}
					}
				}

				// Because the PushInputStack will have disabled all input actions.
				if (EnableUsedInputActions || PushInputStack) {
					foreach (var action in m_ScopeElements
						.OfType<IHotkeyWithInputAction>()
						.SelectMany(element => element.GetUsedActions())
						.Distinct()) {

						// MessageBox has multiple buttons with the same hotkey, but only one is active.
						if (action.enabled) {
							Debug.LogWarning($"{nameof(UIScope)} {name} is enabling action {action.name} that is already enabled. This is a sign of an input conflict!", this);
						}
						action.Enable();
					}
				}

			} else {

				if (PushInputStack) {
					context.PopActionsState(this);

				} else if (EnableUsedInputActions) {

					foreach (IHotkeyWithInputAction hotkeyElement in m_ScopeElements.OfType<IHotkeyWithInputAction>()) {
						foreach (var action in hotkeyElement.GetUsedActions()) {
							// This can often be a valid case since the code may push a new state in the input stack, resetting all the actions, before changing the UIScopes.
							//if (!action.enabled) {
							//	Debug.LogWarning($"{nameof(UIScope)} {name} is disabling action {action.name} that is already disabled. This is a sign of an input conflict!", this);
							//}
							action.Disable();
						}
					}

				}
			}
#endif
		}
	}

#if UNITY_EDITOR
	[UnityEditor.CustomEditor(typeof(UIScope), true)]
	internal class UIScopeEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			UnityEditor.EditorGUI.BeginDisabledGroup(true);
			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
			UnityEditor.EditorGUI.EndDisabledGroup();

			var uiScope = (UIScope)target;

			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.OnEnableBehaviour)));
			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.OnDisableBehaviour)));

			if (uiScope.OnDisableBehaviour == UIScope.OnDisablePolicy.FocusFirstEnabledScopeFromList) {
				UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.OnDisableScopes)));
			}

#if USE_INPUT_SYSTEM
			UnityEditor.EditorGUILayout.Space();

			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.ResetAllActionsOnEnable)));

			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.EnableUsedInputActions)));
			UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.PushInputStack)));
			if (uiScope.PushInputStack) {
				UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(UIScope.IncludeUIActions)));
			}
#endif
			serializedObject.ApplyModifiedProperties();


			var scopeElements = new List<IScopeElement>();
			var directChildScopes = new List<UIScope>();
			UIScope.ScanForChildScopeElements(uiScope, uiScope.transform, scopeElements, directChildScopes);


			UnityEditor.EditorGUILayout.Space();
			UnityEditor.EditorGUILayout.LabelField("Controlled Elements:", UnityEditor.EditorStyles.boldLabel);

			foreach(var element in scopeElements) {
				UnityEditor.EditorGUILayout.BeginHorizontal();
				UnityEditor.EditorGUILayout.ObjectField(element as UnityEngine.Object, typeof(IScopeElement), true);

#if USE_INPUT_SYSTEM
				if (element is IHotkeyWithInputAction hotkeyElement) {

					var prevColor = GUI.color;

					bool actionsActive = uiScope.enabled && uiScope.gameObject.activeInHierarchy && hotkeyElement.CheckIfAnyActionIsEnabled();
					string activeStr = actionsActive ? "Active" : "Inactive";
					GUI.color = actionsActive ? Color.green : Color.red;

					GUILayout.Label(new GUIContent(activeStr, "Are the hotkey input actions active or not?"), GUILayout.ExpandWidth(false));
					GUI.color = prevColor;
				}
#endif
				UnityEditor.EditorGUILayout.EndHorizontal();
			}
		}
	}
#endif
}