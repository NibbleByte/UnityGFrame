#if USE_INPUT_SYSTEM

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Put next to or under a UI.Selectable to be selected on performing the specified input action.
	/// Note that this component will enable the input action and it needs to stay enabled to be invoked.
	/// </summary>
	public class HotkeySelectScopeElement : HotkeyBaseScopeElement
	{
		private Selectable m_Selectable;

		protected override void OnInvoke(InputAction.CallbackContext context)
		{
			if (m_Selectable == null) {
				m_Selectable = GetComponentInParent<Selectable>();
			}

			m_PlayerContext.SetSelectedGameObject(gameObject);
		}

		protected override void OnValidate()
		{
			base.OnValidate();

			// OnValidate() gets called even if object is not active.
			var selectable = GetComponentInParent<Selectable>(true);
			if (selectable == null) {
				Debug.LogError($"[Input] No valid selectable was found for HotkeySelect {name}", this);
				return;
			}
		}
	}
}

#endif