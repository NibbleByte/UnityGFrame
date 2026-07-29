using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DevLocker.WiseInput.UIScope
{
	/// <summary>
	/// Enable whole input actions set when the scope element is active.
	/// </summary>
	public class EnableInputActionsSetScopeElement : MonoBehaviour, IScopeElement, IHotkeysWithInputActions
	{
		public InputActionsSetDef ActionsSet;

		// Used for multiple event systems (e.g. split screen).
		protected IInputUIRoot m_InputUIRoot;

		protected bool m_HasInitialized = false;

		protected virtual void Reset()
		{
			// Let scopes do the enabling or else you'll get warnings for hotkey conflicts for multiple scopes with the same hotkey on screen.
			enabled = false;
		}

		protected virtual void Awake()
		{
			m_InputUIRoot = InputContextUtils.GetInputUIRootFor(gameObject);

			m_InputUIRoot.AddSetupCallback((delayedSetup) => {
				m_HasInitialized = true;

				if (delayedSetup && isActiveAndEnabled) {
					OnEnable();
				}
			});
		}

		void OnEnable()
		{
			if (!m_HasInitialized)
				return;

			m_InputUIRoot.InputContext.Enable(this, GetUsedActions(m_InputUIRoot.InputContext));
		}

		void OnDisable()
		{
			if (!m_HasInitialized)
				return;

			m_InputUIRoot.InputContext.Disable(this, GetUsedActions(m_InputUIRoot.InputContext));
		}

		public IEnumerable<InputAction> GetUsedActions(IInputContext inputContext)
		{
			if (ActionsSet == null)
				return Enumerable.Empty<InputAction>();

			return ActionsSet.GetActions(inputContext);
		}

		protected virtual void OnValidate()
		{
			// Check the Reset() message.
			if (!Application.isPlaying && enabled) {
				enabled = false;
			}
		}
	}
}