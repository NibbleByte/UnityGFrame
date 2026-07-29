using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DevLocker.WiseInput.UIScope
{
	/// <summary>
	/// Disable any current hover effects when NOT using the mouse.
	/// Use this script with <see cref="IInputContext.InputBehaviours.MouseSupportsUINavigationSelection"/> being false.
	/// Place this script next to your EventSystem setup or similar, i.e. only one global instance is needed.
	/// </summary>
	public class DisableHoverWhenMouseInactive : MonoBehaviour
	{
		// Used for multiple event systems (e.g. split screen).
		protected IInputUIRoot m_InputUIRoot;

		protected bool m_HasInitialized = false;

		void Awake()
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

			m_InputUIRoot.InputContext.LastUsedDeviceChanged += OnLastUsedDeviceChanged;
		}

		void OnDisable()
		{
			if (!m_HasInitialized)
				return;

			m_InputUIRoot.InputContext.LastUsedDeviceChanged -= OnLastUsedDeviceChanged;
			SetMouseHoverUsage(true);
		}

		private void OnLastUsedDeviceChanged()
		{
			bool isMouse = m_InputUIRoot.InputContext.GetLastUsedInputDevice() is Mouse;
			SetMouseHoverUsage(isMouse);
		}

		private void SetMouseHoverUsage(bool activeHover)
		{
			if (!activeHover) {
				// Simulate mouse being outside of screen so IPointerExitHandler events get triggered.
				InputState.Change(Mouse.current.position, new Vector2(-1f, -1f));
			}
		}
	}
}