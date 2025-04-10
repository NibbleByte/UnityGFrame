using DevLocker.GFrame.Input;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;

namespace DevLocker.GFrame.SampleGame.Game
{
	public partial class @SamplePlayerControls : IInputActionCollection2, IDisposable, IInputContext
	{
		public IInputContext InputContext { get; private set; }

		public void SetInputContext(IInputContext context)
		{
			InputContext = context;
		}

		#region IInputContext Forwarding

		public event Action LastUsedDeviceChanged {
			add { InputContext.LastUsedDeviceChanged += value; }
			remove { InputContext.LastUsedDeviceChanged -= value; }
		}

		public event Action LastUsedInputControlSchemeChanged {
			add { InputContext.LastUsedInputControlSchemeChanged += value; }
			remove { InputContext.LastUsedInputControlSchemeChanged -= value; }
		}

		public bool DeviceSupportsUINavigationSelection => InputContext.DeviceSupportsUINavigationSelection;

		public InputDevice ForcedDevice { get => InputContext.ForcedDevice; set => InputContext.ForcedDevice = value; }

		public InputActionsMaskedStack InputActionsMaskedStack => InputContext.InputActionsMaskedStack;

		public IInputContext.InputBehaviours DefaultBehaviours => InputContext.DefaultBehaviours;
		public InputUser User => InputContext.User;
		public ReadOnlyArray<InputDevice> PairedDevices => InputContext.PairedDevices;

		public void PerformPairingWithDevice(InputDevice device, InputUserPairingOptions options = InputUserPairingOptions.None) => InputContext.PerformPairingWithDevice(device, options);
		public void UnpairDevice(InputDevice device) => InputContext.UnpairDevice(device);
		public void PerformPairingWithEmptyDevice() => InputContext.PerformPairingWithEmptyDevice();
		public void UnpairDevices() => InputContext.UnpairDevices();

		public InputAction FindActionFor(string actionNameOrId, bool throwIfNotFound = false) => InputContext.FindActionFor(actionNameOrId, throwIfNotFound);
		public InputAction FindActionFor(Guid id, bool throwIfNotFound = false) => InputContext.FindActionFor(id, throwIfNotFound);
		public void EnableAction(object source, InputAction action) => InputContext.EnableAction(source, action);
		public void DisableAction(object source, InputAction action) => InputContext.DisableAction(source, action);
		public void DisableAll(object source) => InputContext.DisableAll(source);
		public IEnumerable<InputAction> GetInputActionsEnabledBy(object source) => InputContext.GetInputActionsEnabledBy(source);
		public IEnumerable<object> GetEnablingSourcesFor(InputAction action) => InputContext.GetEnablingSourcesFor(action);
		public bool IsEnabledBy(object source, InputAction action) => InputContext.IsEnabledBy(source, action);

		public void PushOrSetActionsMask(object source, IEnumerable<InputAction> actionsMask, bool setBackToTop = false) => InputContext.PushOrSetActionsMask(source, actionsMask, setBackToTop);
		public void PopActionsMask(object source) => InputContext.PopActionsMask(source);

		public IEnumerable<InputAction> GetUIActions() => InputContext.GetUIActions();
		public IEnumerable<InputAction> GetAllActions() => InputContext.GetAllActions();

		public InputDevice GetLastUsedInputDevice() => InputContext.GetLastUsedInputDevice();
		public InputControlScheme GetLastUsedInputControlScheme() => InputContext.GetLastUsedInputControlScheme();
		public void TriggerLastUsedDeviceChanged() => InputContext.TriggerLastUsedDeviceChanged();
		public void TriggerLastUsedInputControlSchemeChanged() => InputContext.TriggerLastUsedInputControlSchemeChanged();


		public IEnumerable<InputControlScheme> GetAllInputControlSchemes() => InputContext.GetAllInputControlSchemes();
		public IReadOnlyList<IInputBindingDisplayDataProvider> GetAllDisplayDataProviders() => InputContext.GetAllDisplayDataProviders();
		public IInputBindingDisplayDataProvider GetCurrentDisplayDataProvider() => InputContext.GetCurrentDisplayDataProvider();

		#endregion
	}

	/// <summary>
	/// Context of the game.
	/// It is stored in the LevelsManager being accessible from everywhere.
	/// Use this to share data needed by everyone.
	/// </summary>
	public sealed class SampleGameContext
	{
		public SampleGameContext(PlayerInput playerInput, SamplePlayerControls controls, IInputContext inputContext)
		{
			PlayerInput = playerInput;
			PlayerControls = controls;
			InputContext = inputContext;
		}

		public SamplePlayerControls PlayerControls { get; }

		public PlayerInput PlayerInput { get; }

		public IInputContext InputContext { get; }
	}
}