using DevLocker.GFrame.Input.UIInputDisplay;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DevLocker.GFrame.Input.Contexts
{
	/// <summary>
	/// Add this next to <see cref="InputUIRootObject"/> to force selected device to be used.
	/// This will force only hotkey icons to display for it.
	/// </summary>
	public class ForceInputDevice : MonoBehaviour
	{
		[Tooltip("If empty will use the global player context.")]
		public InputUIRootObject InputUIRoot;

		public InputBindingDisplayAsset ForcedDevice;

		private InputBindingDisplayAsset m_LastForcedDevice;

		private InputDevice m_FakedDevice;

		void OnDestroy()
		{
			ForcedDevice = null;
			m_LastForcedDevice = null;

			if (m_FakedDevice != null) {
				InputSystem.RemoveDevice(m_FakedDevice);
				m_FakedDevice = null;
			}
		}

		void Update()
		{
			if (InputUIRoot == null) {
				InputUIRoot = InputUIRootObject.GlobalUIRoot;
			}

			if (InputUIRoot == null || !InputUIRoot.IsActive || InputUIRoot.InputContext == null)
				return;

			if (m_LastForcedDevice != ForcedDevice) {
				m_LastForcedDevice = ForcedDevice;

				if (m_FakedDevice != null) {
					InputSystem.RemoveDevice(m_FakedDevice);
					m_FakedDevice = null;
				}

				if (m_LastForcedDevice == null) {
					InputUIRoot.InputContext.ForcedDevice = null;
					return;
				}

				foreach(string layout in m_LastForcedDevice.MatchingDeviceLayouts) {
					InputDevice device = InputSystem.GetDevice(layout);
					if (device != null) {
						InputUIRoot.InputContext.ForcedDevice = device;
						return;
					}
				}

				InputUIRoot.InputContext.ForcedDevice =
					m_FakedDevice = InputSystem.AddDevice(m_LastForcedDevice.MatchingDeviceLayouts.First(), $"{nameof(ForceInputDevice)} - {m_LastForcedDevice.name}");
			}
		}
	}
}