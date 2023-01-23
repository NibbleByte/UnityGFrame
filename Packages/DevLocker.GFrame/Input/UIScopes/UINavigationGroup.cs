using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Isolates all child selectables into a separate navigation group (can't navigate out from it, unless specified to).
	/// NOTE: outside selectables can still navigate to this group.
	/// </summary>
	public class UINavigationGroup : MonoBehaviour
	{
		public enum WrapMode
		{
			None,
			Wrap,
			Auto,
			Explicit,
			FirstSelectableOfNavigationGroup,
			LastSelectableOfNavigationGroup,
			FocusScope,
			TriggerEvent,
		}

		public enum NavigationMode
		{
			Grid,
			Horizontal,
			Vertical,
		}

		[Serializable]
		public struct WrapBehaviour
		{
			public WrapMode Mode;
			public bool IsNoneMode => Mode == WrapMode.None;
			public bool IsWrapMode => Mode == WrapMode.Wrap;
			public bool IsAutoMode => Mode == WrapMode.Auto;
			public bool IsExplicitMode => Mode == WrapMode.Explicit;
			public bool IsFirstSelectableOfNavigationGroupMode => Mode == WrapMode.FirstSelectableOfNavigationGroup;
			public bool IsLastSelectableOfNavigationGroupMode => Mode == WrapMode.LastSelectableOfNavigationGroup;
			public bool IsFocusScopeMode => Mode == WrapMode.FocusScope;
			public bool IsTriggerEventMode => Mode == WrapMode.TriggerEvent;

			public Selectable Selectable;
			public UINavigationGroup NavigationGroup;
			public UIScope Scope;
			public UnityEvent Event;
		}

		// TODO: Apply wrap mode ONLY when selected boundary button.

		[Header("Wrap Behaviours")]
		public WrapBehaviour WrapUp;
		public WrapBehaviour WrapDown;
		public WrapBehaviour WrapLeft;
		public WrapBehaviour WrapRight;

		[Header("Details")]
		[Tooltip("Scan for added or removed selectables on Update(). If disabled, call methods manually on change.\nNOTE: Changing positions of selectables will always require manual notification.")]
		public bool AutoScanForSelectables = true;

		public NavigationMode Navigation = NavigationMode.Grid;


		[Tooltip("Include these selectables to navigation links, in addition to any group children.\nUseful to include special selectables from outside the group, but not all outsiders.")]
		public List<Selectable> Include;

		[Tooltip("Exclude these selectables from navigation links.")]
		public List<Selectable> Exclude;

		private Selectable[] m_AllActiveSelectables = new Selectable[10];

		private List<Selectable> m_ManagedSelectables = new List<Selectable>();

		public IReadOnlyList<Selectable> ManagedSelectables => m_ManagedSelectables;

		/// <summary>
		/// Most top-left selectable.
		/// </summary>
		public Selectable FirstSelectable {
			get {
				if (m_FirstSelectable == null) {
					RescanSelectables();
				}

				return m_FirstSelectable;
			}
		}

		/// <summary>
		/// Most bottom-right selectable.
		/// </summary>
		public Selectable LastSelectable {
			get {
				if (m_LastSelectable == null) {
					RescanSelectables();
				}

				return m_LastSelectable;
			}
		}

		private Selectable m_FirstSelectable = null;
		private Selectable m_LastSelectable = null;

		/// <summary>
		/// Call this if you have added or removed child selectables and have <see cref="AutoScanForSelectables"/> disabled (no updates, no polling).
		/// </summary>
		public bool RescanSelectables()
		{
			if (Application.isPlaying) {
				Selectable.AllSelectablesNoAlloc(m_AllActiveSelectables);

				// Keep expanding till array is big enough to fit all the active selectables.
				while (m_AllActiveSelectables.Last() != null) {
					m_AllActiveSelectables = new Selectable[m_AllActiveSelectables.Length * 2];
					Selectable.AllSelectablesNoAlloc(m_AllActiveSelectables);
				}

			} else {
				m_AllActiveSelectables = GetComponentsInChildren<Selectable>().Concat(Include).Distinct().ToArray();
			}

			bool needsRefresh = false;

			// Remove destroyed or inactive selectables.
			for (int i = 0; i < m_ManagedSelectables.Count; ++i) {
				Selectable selectable = m_ManagedSelectables[i];

				// Destroyed - yes. Re-parented - no. :(
				// Check active instead gameObject.activeInHierarchy hoping it will be faster.
				if (selectable == null || !IsStillActive(selectable)) {
					m_ManagedSelectables.RemoveAt(i);
					--i;
					needsRefresh = true;
					continue;
				}
			}

			// Check for new selectables.
			foreach (Selectable selectable in m_AllActiveSelectables) {

				// End of array... or destroyed?
				if (selectable == null)
					break;

				if (!Exclude.Contains(selectable)
					&& !m_ManagedSelectables.Contains(selectable)
					&& (Include.Contains(selectable) || selectable.transform.IsChildOf(transform))
					) {
					m_ManagedSelectables.Add(selectable);
					needsRefresh = true;
				}
			}

			if (needsRefresh) {
				RefreshNavigationLinks();
			}

			return needsRefresh;
		}

		/// <summary>
		/// Will re-evaluate all managed navigation links.
		/// Call this if you have moved the selectables around (this won't be detected automatically)
		/// </summary>
		public void RefreshNavigationLinks()
		{
			m_FirstSelectable = m_LastSelectable = m_ManagedSelectables.FirstOrDefault();

#if UNITY_EDITOR
			if (!Application.isPlaying) {
				Undo.RecordObjects(m_ManagedSelectables.ToArray(), this.ToString());
			}
#endif

			for (int i = 0; i < m_ManagedSelectables.Count; ++i) {
				Selectable selectable = m_ManagedSelectables[i];

				// Destroyed - yes. Re-parented - no. :(
				if (selectable == null) {
					m_ManagedSelectables.RemoveAt(i);
					--i;
					continue;
				}

				Navigation nav = selectable.navigation;
				nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;

				//
				// https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Selectable.html#UnityEngine_UI_Selectable_FindSelectable_UnityEngine_Vector3_
				// Don't use FindSelectableOn*() as it won't work if "Explicit" mode set.
				nav.selectOnUp = (Navigation == NavigationMode.Grid || Navigation == NavigationMode.Vertical) ? selectable.FindSelectable(selectable.transform.rotation * Vector3.up) : null;
				nav.selectOnDown = (Navigation == NavigationMode.Grid || Navigation == NavigationMode.Vertical) ? selectable.FindSelectable(selectable.transform.rotation * Vector3.down) : null;
				nav.selectOnLeft = (Navigation == NavigationMode.Grid || Navigation == NavigationMode.Horizontal) ? selectable.FindSelectable(selectable.transform.rotation * Vector3.left) : null;
				nav.selectOnRight = (Navigation == NavigationMode.Grid || Navigation == NavigationMode.Horizontal) ? selectable.FindSelectable(selectable.transform.rotation * Vector3.right) : null;

				bool outsideUp = nav.selectOnUp == null || !m_ManagedSelectables.Contains(nav.selectOnUp);
				bool outsideDown = nav.selectOnDown == null || !m_ManagedSelectables.Contains(nav.selectOnDown);
				bool outsideLeft = nav.selectOnLeft == null || !m_ManagedSelectables.Contains(nav.selectOnLeft);
				bool outsideRight = nav.selectOnRight == null || !m_ManagedSelectables.Contains(nav.selectOnRight);

				if (outsideUp == outsideLeft == false) {
					m_FirstSelectable = selectable;
				}

				if (outsideDown == outsideRight == false) {
					m_LastSelectable = selectable;
				}

				// Not one of ours? Don't link then!
				if (outsideUp && !WrapUp.IsAutoMode) nav.selectOnUp = null;
				if (outsideDown && !WrapDown.IsAutoMode) nav.selectOnDown = null;
				if (outsideLeft && !WrapLeft.IsAutoMode) nav.selectOnLeft = null;
				if (outsideRight && !WrapRight.IsAutoMode) nav.selectOnRight = null;

				if (nav.selectOnUp == null && WrapUp.IsExplicitMode) nav.selectOnUp = WrapUp.Selectable;
				if (nav.selectOnDown == null && WrapDown.IsExplicitMode) nav.selectOnDown = WrapDown.Selectable;
				if (nav.selectOnLeft == null && WrapLeft.IsExplicitMode) nav.selectOnLeft = WrapLeft.Selectable;
				if (nav.selectOnRight == null && WrapRight.IsExplicitMode) nav.selectOnRight = WrapRight.Selectable;

				selectable.navigation = nav;
			}

			//
			// Now do it again to setup wrapping wrap modes, after all links are setup.
			//
			if (WrapUp.IsWrapMode || WrapDown.IsWrapMode || WrapLeft.IsWrapMode || WrapRight.IsWrapMode) {

				for (int i = 0; i < m_ManagedSelectables.Count; ++i) {
					Selectable selectable = m_ManagedSelectables[i];

					Navigation nav = selectable.navigation;
					nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;

					int sanityCount = 0;
					const int sanityCountLimit = 10000;

					if (nav.selectOnUp == null && WrapUp.IsWrapMode) {
						Selectable it = selectable;
						while(it.navigation.selectOnDown != null && it.navigation.selectOnDown != selectable) {
							it = it.navigation.selectOnDown;

							sanityCount++;
							if (sanityCount > sanityCountLimit) {
								Debug.LogError($"Navigation group couldn't wrap around {selectable}!", this);
								break;
							}
						}

						if (selectable != it) {
							nav.selectOnUp = it;
						}
					}


					if (nav.selectOnDown == null && WrapDown.IsWrapMode) {
						Selectable it = selectable;
						while(it.navigation.selectOnUp != null && it.navigation.selectOnUp != selectable) {
							it = it.navigation.selectOnUp;

							sanityCount++;
							if (sanityCount > sanityCountLimit) {
								Debug.LogError($"Navigation group couldn't wrap around {selectable}!", this);
								break;
							}
						}

						if (selectable != it) {
							nav.selectOnDown = it;
						}
					}


					if (nav.selectOnLeft == null && WrapLeft.IsWrapMode) {
						Selectable it = selectable;
						while(it.navigation.selectOnRight != null && it.navigation.selectOnRight != selectable) {
							it = it.navigation.selectOnRight;

							sanityCount++;
							if (sanityCount > sanityCountLimit) {
								Debug.LogError($"Navigation group couldn't wrap around {selectable}!", this);
								break;
							}
						}

						if (selectable != it) {
							nav.selectOnLeft = it;
						}
					}


					if (nav.selectOnRight == null && WrapRight.IsWrapMode) {
						Selectable it = selectable;
						while(it.navigation.selectOnLeft != null && it.navigation.selectOnLeft != selectable) {
							it = it.navigation.selectOnLeft;

							sanityCount++;
							if (sanityCount > sanityCountLimit) {
								Debug.LogError($"Navigation group couldn't wrap around {selectable}!", this);
								break;
							}
						}

						if (selectable != it) {
							nav.selectOnRight = it;
						}
					}

					selectable.navigation = nav;
				}
			}
		}

		/// <summary>
		/// Call this in dire situations (re-parenting selectables).
		/// </summary>
		public void ClearCache()
		{
			m_ManagedSelectables.Clear();
		}

		private bool IsStillActive(Selectable selectable)
		{
			foreach(Selectable activeSelectable in m_AllActiveSelectables) {
				if (activeSelectable == null) return false;	// Reached the end of the array.
				if (activeSelectable == selectable) return true;
			}

			return false;
		}

#if UNITY_EDITOR
		[ContextMenu("Apply Group Navigation")]
		internal void ApplyGroupNavigation()
		{
			ClearCache();
			RescanSelectables();
		}
#endif

		void Update()
		{
			if (AutoScanForSelectables) {
				RescanSelectables();
			}
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(UINavigationGroup.WrapBehaviour))]
	internal class NavigationGroupWrapBehaviourPropertyDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var mode = (UINavigationGroup.WrapMode) property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)).enumValueIndex;
			switch (mode) {
				case UINavigationGroup.WrapMode.None:
				case UINavigationGroup.WrapMode.Wrap:
				case UINavigationGroup.WrapMode.Auto:
				case UINavigationGroup.WrapMode.Explicit:
				case UINavigationGroup.WrapMode.FirstSelectableOfNavigationGroup:
				case UINavigationGroup.WrapMode.LastSelectableOfNavigationGroup:
				case UINavigationGroup.WrapMode.FocusScope:
					return EditorGUIUtility.singleLineHeight;
				case UINavigationGroup.WrapMode.TriggerEvent:
					return EditorGUIUtility.singleLineHeight + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Event)));
				default:
					throw new NotSupportedException(mode.ToString());
			}
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			label = EditorGUI.BeginProperty(position, label, property);

			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			var mode = (UINavigationGroup.WrapMode)property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)).enumValueIndex;

			switch (mode) {
				case UINavigationGroup.WrapMode.None:
				case UINavigationGroup.WrapMode.Wrap:
				case UINavigationGroup.WrapMode.Auto:
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)), new GUIContent());
					break;

				case UINavigationGroup.WrapMode.Explicit:
					position.width /= 2f;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)), new GUIContent());
					position.x += position.width;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Selectable)), new GUIContent());
					break;

				case UINavigationGroup.WrapMode.FirstSelectableOfNavigationGroup:
				case UINavigationGroup.WrapMode.LastSelectableOfNavigationGroup:
					position.width /= 2f;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)), new GUIContent());
					position.x += position.width;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.NavigationGroup)), new GUIContent());
					break;

				case UINavigationGroup.WrapMode.FocusScope:
					position.width /= 2f;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)), new GUIContent());
					position.x += position.width;
					EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Scope)), new GUIContent());
					break;

				case UINavigationGroup.WrapMode.TriggerEvent:
					EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Mode)), new GUIContent());
					position.height -= EditorGUIUtility.singleLineHeight;
					position.y += EditorGUIUtility.singleLineHeight;
					EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.FindPropertyRelative(nameof(UINavigationGroup.WrapBehaviour.Event)), new GUIContent());
					break;

				default:
					throw new NotSupportedException(mode.ToString());
			}

			EditorGUI.EndProperty();
		}
	}

	[CustomEditor(typeof(UINavigationGroup))]
	[CanEditMultipleObjects]
	internal class NavigationGroupEditor : Editor
	{
		/// <summary>
		/// Copy-pasted logic for drawing navigation links from <see cref="SelectableEditor"/> - s_ShowNavigation.
		/// </summary>


		private static List<NavigationGroupEditor> s_Editors = new List<NavigationGroupEditor>();
		private static bool s_ShowNavigation = false;
		private static string s_ShowNavigationKey = "SelectableEditor.ShowNavigation";
		private static System.Reflection.MethodInfo s_StaticOnSceneGUIMethod;
		private GUIContent m_VisualizeNavigation = EditorGUIUtility.TrTextContent("Visualize", "Show navigation flows between selectable UI elements.");

		protected virtual void OnEnable()
		{
			s_Editors.Add(this);
			//RegisterStaticOnSceneGUI();

			if (s_Editors.Count == 1) {
				SceneView.duringSceneGui += StaticOnSceneGUI;
			}

			s_ShowNavigation = EditorPrefs.GetBool(s_ShowNavigationKey);
			SyncShowNavigation();
		}

		protected virtual void OnDisable()
		{
			s_Editors.Remove(this);
			//RegisterStaticOnSceneGUI();

			if (s_Editors.Count == 0) {
				SceneView.duringSceneGui -= StaticOnSceneGUI;
			}
		}

		// HACK: Having two inspectors causes twice the OnEnabled on select and OnDisable on deselect.
		//		 Unsubscribing multiple times in OnDisable() at the same time fails for some reason and handler keeps getting called.
		//		 Don't use this approach as it can also double-subscribe and render twice the nav-links.
		//private void RegisterStaticOnSceneGUI()
		//{
		//	SceneView.duringSceneGui -= StaticOnSceneGUI;
		//	if (s_Editors.Count > 0)
		//		SceneView.duringSceneGui += StaticOnSceneGUI;
		//}

		private void StaticOnSceneGUI(SceneView view)
		{
			if (!s_ShowNavigation)
				return;

			// HACK: Having two inspectors causes twice the OnEnabled on select and OnDisable on deselect.
			//		 Unsubscribing multiple times in OnDisable() at the same time fails for some reason and handler keeps getting called.
			if (s_Editors.Count == 0) {
				SceneView.duringSceneGui -= StaticOnSceneGUI;
			}

			if (s_StaticOnSceneGUIMethod == null) {
				Type selectableEditorType = typeof(UnityEditor.UI.SelectableEditor);
				s_StaticOnSceneGUIMethod = selectableEditorType.GetMethod("StaticOnSceneGUI", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			}

			// Something changed? Fail silently...
			if (s_StaticOnSceneGUIMethod == null)
				return;

			s_StaticOnSceneGUIMethod.Invoke(null, new object[] { view });
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			EditorGUI.BeginChangeCheck();

			s_ShowNavigation = GUILayout.Toggle(s_ShowNavigation, m_VisualizeNavigation, EditorStyles.miniButton);

			if (GUILayout.Button("Apply Group Navigation")) {
				foreach(UnityEngine.Object target in targets) {
					if (target is UINavigationGroup group) {
						group.ApplyGroupNavigation();
					}
				}
			}

			if (EditorGUI.EndChangeCheck()) {
				EditorPrefs.SetBool(s_ShowNavigationKey, s_ShowNavigation);

				SyncShowNavigation();

				SceneView.RepaintAll();
			}
		}

		private static void SyncShowNavigation()
		{
			Type selectableEditorType = typeof(UnityEditor.UI.SelectableEditor);
			var s_ShowNavigationField = selectableEditorType.GetField("s_ShowNavigation", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

			// Something changed? Fail silently...
			if (s_ShowNavigationField == null)
				return;

			s_ShowNavigationField.SetValue(null, s_ShowNavigation);
		}
	}
#endif

}