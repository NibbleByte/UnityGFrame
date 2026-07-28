using UnityEngine;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// When UI selectable is selected under this component target UIScope is forced to be focused.
	/// With it, hotkey icons etc. may pop up.
	/// </summary>
	public class FocusUIScopeOnSelection : MonoBehaviour
	{
		public UIScope Scope;

		private GameObject m_LastSelectedObject;

		// Used for multiple event systems (e.g. split screen).
		protected IInputUIRoot m_InputUIRoot;

		void Reset()
		{
			Scope = GetComponent<UIScope>();
		}

		void Awake()
		{
			if (Scope == null) {
				Scope = GetComponent<UIScope>();
			}

			m_InputUIRoot = InputContextUtils.GetInputUIRootFor(gameObject);
		}

		/// <summary>
		/// LateUpdate() as most scripts will do selection on Update() - wait for all of them to finish... hopefully.
		/// <see cref="SelectionController"/>
		/// </summary>
		void LateUpdate()
		{
			if (!m_InputUIRoot.IsActive)
				return;

			// Don't steal selection controller selection opportunity.
			SelectionController activeSelectionController = SelectionController.GetActiveInstanceFor(m_InputUIRoot.GetRootObject());
			if (activeSelectionController && activeSelectionController.IsSelectRequested)
				return;

			if (m_LastSelectedObject != m_InputUIRoot.SelectedGameObject) {
				m_LastSelectedObject = m_InputUIRoot.SelectedGameObject;

				if (!Scope.IsActive && m_LastSelectedObject && m_LastSelectedObject.transform.IsChildOf(transform) && m_LastSelectedObject.GetComponentInParent<UIScope>() == Scope) {
					Scope.Focus();
				}
			}
		}

		private void OnDisable()
		{
			m_LastSelectedObject = null;
		}

		private void OnValidate()
		{
			Utils.Validation.ValidateMissingObject(this, Scope, nameof(Scope));
		}
	}
}