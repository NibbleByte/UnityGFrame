#if USE_INPUT_SYSTEM
using DevLocker.GFrame.Input.Contexts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input
{

	/// <summary>
	/// Includes everything needed to display <see cref="InputBinding"/> to the UI.
	/// Any one of those fields can be null (except Binding).
	/// All texts can be localization keys if needed.
	/// </summary>
	public struct InputBindingDisplayData
	{
		/// <summary>
		/// The binding that the text / icon data is for.
		/// If this is a composite binding (e.g. 2DVector with WASD controls),
		/// texts will be summarized like "W/A/S/D" by default.
		/// You can check each part of the composite binding at the <see cref="CompositeBindingParts"/>.
		///
		/// https://docs.unity3d.com/Packages/com.unity.inputsystem@1.1/api/UnityEngine.InputSystem.InputBinding.html
		/// </summary>
		public InputBinding Binding;

		/// <summary>
		/// The binding index this Binding was found at.
		/// </summary>
		public int BindingIndex;

		/// <summary>
		/// The control scheme that matched the binding.
		/// </summary>
		public string ControlScheme;

		/// <summary>
		/// The device layout that matched the binding.
		/// </summary>
		public string DeviceLayout;

		/// <summary>
		/// If the matched <see cref="Binding"/> is composite, this list stores it's part bindings.
		///
		/// NOTE: If <see cref="Binding"/> is part of composite, this collection will be null.
		/// </summary>
		public IReadOnlyList<InputBindingDisplayData> CompositeBindingParts;

		/// <summary>
		/// Does this entry contain fallback texts, as it was not found in the <see cref="IInputBindingDisplayDataProvider"/> list.
		/// </summary>
		public bool IsFallback;

		public string Text;
		public string ShortText;

		public bool IsValid => Binding.id != Guid.Empty;

		/// <summary>
		/// If binding is composite, it contains other bindings as part of it.
		/// In that case this binding doesn't contain actual controls to use - the sub parts do.
		/// The part bindings can be found in <see cref="CompositeBindingParts" />
		///
		/// https://docs.unity3d.com/Packages/com.unity.inputsystem@1.1/api/UnityEngine.InputSystem.InputBinding.html
		/// </summary>
		public bool IsComposite => Binding.isComposite;

		public bool IsPartOfComposite => Binding.isPartOfComposite;

		public bool HasText => !string.IsNullOrWhiteSpace(Text) || !string.IsNullOrWhiteSpace(ShortText);

		public override string ToString() => $"{Binding.name} - {ShortText}";
	}

	/// <summary>
	/// Provides the required binding representations to display hotkeys in the UI for specific device.
	/// </summary>
	public interface IInputBindingDisplayDataProvider
	{
		/// <summary>
		/// If one of the action's bindings doesn't have a defined display data in the list, use the default display name provided by Unity.
		/// </summary>
		bool FallbackToDefaultDisplayTexts { get; }

		/// <summary>
		/// Is UI navigation with selected element allowed when this type of device is used?
		/// </summary>
		bool SupportsUINavigationSelection { get; }

		/// <summary>
		/// Pass in the selected binding display text to get additionally formatted.
		/// </summary>
		public string FormatBindingDisplayText(string displayText);

		/// <summary>
		/// Does this provider has representations for this binding's control scheme.
		/// </summary>
		bool MatchesBinding(InputBinding binding);

		/// <summary>
		/// What devices does this provider has representations for.
		/// </summary>
		bool MatchesDevice(string deviceLayout);

		/// <summary>
		/// Get the display representations for the passed action.
		/// An action can have multiple bindings for the same device.
		/// </summary>
		IEnumerable<InputBindingDisplayData> GetBindingDisplaysFor(InputAction action);

	}

	/// <summary>
	/// Manages player context and input. This also should be a MonoBehavior marking the UI hierarchy used by the specific player (use <see cref="PlayerContextUtils.GetPlayerContextFor"/>.
	/// For more info check <see cref="PlayerContextUIRootObject"/> and <see cref="PlayerContextUIRootForwarder"/>.
	/// Remember, you have a Global player root as well - <see cref="PlayerContextUIRootObject.GlobalPlayerContext"/>
	/// </summary>
	public interface IPlayerContext
	{
		/// <summary>
		/// Is the player setup ready.
		/// </summary>
		bool IsActive { get; }

		/// <summary>
		/// Name of the player.
		/// </summary>
		string PlayerName { get; }

		/// <summary>
		/// The input context for this player. The heart of this framework.
		/// Includes the InputStack that should be used everywhere.
		/// </summary>
		IInputContext InputContext { get; }

		/// <summary>
		/// Stack of player states. States can be pushed in / replaced / popped out of the stack.
		/// This is optional, you can go without one for simple games.
		///
		/// NOTE: Context is automatically disposed of on switching level supervisors.
		/// </summary>
		PlayerStateStack StatesStack { get; }

		/// <summary>
		/// Event system used by this player.
		/// </summary>
		EventSystem EventSystem { get; }

		/// <summary>
		/// Short-cut - get selected UI object for this player.
		/// </summary>
		GameObject SelectedGameObject { get; }

		/// <summary>
		/// Short-cut - set selected UI object for this player.
		/// </summary>
		void SetSelectedGameObject(GameObject selected);

		/// <summary>
		/// Get the top-most root object.
		/// </summary>
		PlayerContextUIRootObject GetRootObject();


		/// <summary>
		/// Called when setup (<see cref="PlayerContextUIRootObject.SetupGlobal"/> or <see cref="PlayerContextUIRootObject.SetupPlayer"/>) happens or immediately if setup was already done.
		/// Use this to delay your initialization if you need the InputContext on Awake() or OnEnable(), but it is not yet available.
		/// Once called after setup, the callback is lost - won't be called again so no need to unsubscribe.
		/// </summary>
		void AddSetupCallback(SetupCallbackDelegate setupReadyCallback);
		delegate void SetupCallbackDelegate(bool delayedSetup);
	}

	/// <summary>
	/// Implement this if your game uses Unity Input system with generated <see cref="IInputActionCollection"/>.
	/// HINT: Your implementation of <see cref="IInputActionCollection"/> can also implement this interface,
	///		  forwarding the calls to the real input context so it is easier to use.
	/// </summary>
	public interface IInputContext
	{
		/// <summary>
		/// Specify how input should behave.
		/// </summary>
		public struct InputBehaviours
		{

			/// <summary>
			/// Allows to have no UI object selected when clicking with the mouse on empty space.
			/// </summary>
			public bool AllowEmptyClicksToDeselect;

			/// <summary>
			/// Should navigation selection persist while using the mouse or should it automatically deselect the object.
			/// This overrides the <see cref="DeviceSupportsUINavigationSelection"/>, as keyboard and mouse are usually bundled together.
			/// Use with the <see cref="UIScope.DisableHoverWhenMouseInactive"/>
			/// </summary>
			public bool MouseSupportsUINavigationSelection;

			/// <summary>
			/// Set selected object to none if the current device doesn't support it. E.g. hide selection for mouse & keyboard, but show it for Gamepad.
			/// Check the device <see cref="InputBindingDisplayAsset"/>.
			/// </summary>
			public bool RemoveSelectionIfDeviceDoesntSupportIt;

			public static readonly InputBehaviours Default = new InputBehaviours() {
				AllowEmptyClicksToDeselect = false,
				MouseSupportsUINavigationSelection = false,
				RemoveSelectionIfDeviceDoesntSupportIt = true,
			};
		}

		/// <summary>
		/// Used for overriding the default input behaviour.
		/// </summary>
		public enum InputBehaviourOverride { DefaultBehaviour, Enable, Disable }

		/// <summary>
		/// Specify how input should behave.
		/// </summary>
		InputBehaviours DefaultBehaviours { get; }

		/// <summary>
		/// Last device used got changed.
		/// </summary>
		event Action LastUsedDeviceChanged;

		/// <summary>
		/// Last device used got changed.
		/// </summary>
		event Action LastUsedInputControlSchemeChanged;

		/// <summary>
		/// Associated input user.
		/// Useful for local multiplayer / split screen. For single player game, you can skip this.
		/// </summary>
		InputUser User { get; }

		/// <summary>
		/// Get all paired devices to this player (user).
		/// </summary>
		ReadOnlyArray<InputDevice> PairedDevices { get; }

		/// <summary>
		/// Pair the given device to a user.
		/// Useful for local multiplayer / split screen. For single player game, you can skip this.
		/// </summary>
		void PerformPairingWithDevice(InputDevice device, InputUserPairingOptions options = InputUserPairingOptions.None);

		/// <summary>
		/// Unpair the device from the user.
		/// </summary>
		void UnpairDevice(InputDevice device);

		/// <summary>
		/// Will make the user paired with no devices - they won't have any input.
		/// This is different from <see cref="UnpairDevices()"/>, as unpaired users by default listen for all the devices - <see cref="IInputActionCollection.devices"/>.
		/// It will unpair any current devices.
		///
		/// Useful for local multiplayer / split screen. For single player game, you can skip this.
		/// </summary>
		void PerformPairingWithEmptyDevice();

		/// <summary>
		/// Unpair devices from the current user.
		/// Useful for local multiplayer / split screen. For single player game, you can skip this.
		/// </summary>
		void UnpairDevices();

		/// <summary>
		/// Set to force the context to use only this device and ignore the rest.
		/// This will force only hotkey icons to display for it.
		/// </summary>
		InputDevice ForcedDevice { get; set; }

		InputActionsMaskedStack InputActionsMaskedStack { get; }

		/// <summary>
		/// Does the current device support UI navigation.
		/// </summary>
		bool DeviceSupportsUINavigationSelection { get; }

		/// <summary>
		/// Find InputAction by action name or id. In case of duplicate action names, specify the map like this: "map1/action1".
		/// </summary>
		InputAction FindActionFor(string actionNameOrId, bool throwIfNotFound = false);

		/// <summary>
		/// Find InputAction by action id.
		/// </summary>
		InputAction FindActionFor(Guid id, bool throwIfNotFound = false);

		/// <summary>
		/// Enable action via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		void EnableAction(object source, InputAction action);	// Don't name "Enable()" as it causes conflicts with the extension methods.

		/// <summary>
		/// Disable action via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		void DisableAction(object source, InputAction action);	// Don't name "Disable()" as it causes conflicts with the extension methods.

		/// <summary>
		/// Disable all input actions enabled by the provided source object via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		void DisableAll(object source);

		/// <summary>
		/// Returns all input actions enabled by specified source.
		/// </summary>
		IEnumerable<InputAction> GetInputActionsEnabledBy(object source);

		/// <summary>
		/// Get all sources that enabled specific action.
		/// </summary>
		public IEnumerable<object> GetEnablingSourcesFor(InputAction action);

		/// <summary>
		/// Is the specified action enabled by the provided source.
		/// </summary>
		public bool IsEnabledBy(object source, InputAction action);

		/// <summary>
		/// Push actions mask filtering in actions allowed to be enabled in the <see cref="InputActionsMaskedStack"/>.
		/// If mask is added or set to the top of the stack it will be applied immediately disabling any actions not included.
		/// Masks not on the top of the stack don't affect the actions state.
		/// </summary>
		void PushOrSetActionsMask(object source, IEnumerable<InputAction> actionsMask, bool setBackToTop = false);

		/// <summary>
		/// Remove actions mask by the source it pushed it in the <see cref="InputActionsMaskedStack"/>.
		/// Removing it will restore the actions state according to the next mask in the stack or their original tracked state.
		/// </summary>
		void PopActionsMask(object source);

		/// <summary>
		/// Return all actions required for the UI input to work properly.
		/// Usually those are the ones specified in the <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/>,
		/// which you can easily obtain from <see cref="EventSystem.current.currentInputModule"/>.
		/// If you have <see cref="IInputActionCollection"/>, you can just get the InputActionMap responsible for the UI.
		/// Example: PlayerControls.UI.Get();
		/// </summary>
		IEnumerable<InputAction> GetUIActions();

		/// <summary>
		/// Returns all <see cref="InputAction"/>.
		/// </summary>
		IEnumerable<InputAction> GetAllActions();

		/// <summary>
		/// Get last updated device.
		/// </summary>
		InputDevice GetLastUsedInputDevice();

		/// <summary>
		/// Get last used <see cref="InputControlScheme" />.
		/// NOTE: If no devices used, empty control scheme will be returned.
		/// </summary>
		InputControlScheme GetLastUsedInputControlScheme();

		/// <summary>
		/// Force invoke the LastUsedDeviceChanged, so UI and others can refresh.
		/// This is useful if the player changed the controls or similar,
		/// or if you're using PlayerInput component with SendMessage / Broadcast notification.
		/// </summary>
		void TriggerLastUsedDeviceChanged();

		/// <summary>
		/// Force invoke the LastUsedInputControlSchemeChanged, so UI and others can refresh.
		/// </summary>
		void TriggerLastUsedInputControlSchemeChanged();

		/// <summary>
		/// Get all used <see cref="InputControlScheme" />.
		/// </summary>
		IEnumerable<InputControlScheme> GetAllInputControlSchemes();

		/// <summary>
		/// Get all display data providers.
		/// </summary>
		IReadOnlyList<IInputBindingDisplayDataProvider> GetAllDisplayDataProviders();

		/// <summary>
		/// Get the currently used displayed data provider. Can be null.
		/// </summary>
		IInputBindingDisplayDataProvider GetCurrentDisplayDataProvider();

		/// <summary>
		/// Dispose the context when you finished working with it.
		/// </summary>
		void Dispose();
	}

	/// <summary>
	/// Used to skip hotkeys in some cases.
	/// </summary>
	[Flags]
	public enum SkipHotkeyOption
	{
		InputFieldTextFocused = 1 << 0,
		NonTextSelectableFocused = 1 << 1,
	}

	public class PlayerContextUtils
	{
		/// <summary>
		/// Get the owning player context root. If no owner found, <see cref="PlayerContextUIRootObject.GlobalPlayerContext"/> is returned.
		/// </summary>
		/// <param name="go">target object needing player context</param>
		public static IPlayerContext GetPlayerContextFor(GameObject go)
		{
			var rootObject = go.transform.GetComponentInParent<IPlayerContext>(true);
			if (rootObject != null) {
				return rootObject;
			}

			return PlayerContextUIRootObject.GlobalPlayerContext;
		}

		public static bool ShouldSkipHotkey(IPlayerContext context, SkipHotkeyOption option)
		{
			if (context == null)
				return true;

			if ((option & SkipHotkeyOption.NonTextSelectableFocused) != 0
				&& context.SelectedGameObject
				&& !context.IsTextFieldFocused()
				)
				return true;

			if ((option & SkipHotkeyOption.InputFieldTextFocused) != 0
				&& context.SelectedGameObject
				&& context.IsTextFieldFocused()
				)
				return true;

			return false;
		}
	}

	public static class InputSystemIntegrationExtensions
	{
		/// <summary>
		/// Checks if currently selected object by this player is text field that is focused.
		/// </summary>
		public static bool IsTextFieldFocused(this IPlayerContext context)
		{
			GameObject currentSelection = context.SelectedGameObject;
			if (currentSelection == null)
				return false;

			if (currentSelection.TryGetComponent(out InputField inputField) && inputField.isFocused)
				return true;

			if (currentSelection.TryGetComponent(out TMPro.TMP_InputField inputFieldTMP) && inputFieldTMP.isFocused)
				return true;

			return false;
		}

		/// <summary>
		/// Get the display representations of the matched device for the passed action.
		/// An action can have multiple bindings for the same device.
		/// </summary>
		public static IInputBindingDisplayDataProvider GetFirstMatchingDisplayDataProvider(this IInputContext context, string deviceLayout)
		{
			foreach (var displayData in context.GetAllDisplayDataProviders()) {
				if (displayData.MatchesDevice(deviceLayout))
					return displayData;
			}

			return null;
		}

		/// <summary>
		/// Get <see cref="InputControlScheme" /> of specific device.
		/// </summary>
		public static InputControlScheme GetInputControlSchemeFor(this IInputContext context, InputDevice device)
		{
			return context.GetAllInputControlSchemes().FirstOrDefault(c => c.SupportsDevice(device));
		}

		/// <summary>
		/// Find InputAction by action reference.
		/// </summary>
		public static InputAction FindActionFor(this IInputContext context, InputActionReference inputActionReference, bool throwIfNotFound = false)
		{
			return context.FindActionFor(inputActionReference.action.id, throwIfNotFound);
		}

		/// <summary>
		/// Get the display text for provided <see cref="InputAction"/> (the first binding) for the current <see cref="IInputBindingDisplayDataProvider"/>.
		/// </summary>
		public static string GetDisplayTextFor(this IInputContext context, InputAction action, bool useShortText = true, bool fallbackToDefaultDisplayTexts = true)
		{
			IInputBindingDisplayDataProvider currentDisplayDataProvider = context.GetCurrentDisplayDataProvider();
			if (currentDisplayDataProvider == null)
				return "";

			InputBindingDisplayData displayData = currentDisplayDataProvider.GetBindingDisplaysFor(action).FirstOrDefault();
			if (!displayData.IsValid)
				return "";

			if (displayData.IsFallback && !fallbackToDefaultDisplayTexts)
				return "";

			string usedText = useShortText && !string.IsNullOrWhiteSpace(displayData.ShortText)
					? displayData.ShortText
					: displayData.Text
					;

			return currentDisplayDataProvider.FormatBindingDisplayText(usedText);
		}

		public static bool FinalValue(this IInputContext.InputBehaviourOverride overrideValue, bool defaultValue)
			=> overrideValue switch {
				IInputContext.InputBehaviourOverride.DefaultBehaviour => defaultValue,
				IInputContext.InputBehaviourOverride.Enable => true,
				IInputContext.InputBehaviourOverride.Disable => false,
				_ => throw new NotImplementedException()
			};

		// ======================================================================== \\
		// ======================================================================== \\
		// ======================================================================== \\

		#region IInputContext Enable/Disable Actions

		/// <summary>
		/// Enable action via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Enable(this InputAction action, object source, IInputContext context)
		{
			context.EnableAction(source, action);
		}

		/// <summary>
		/// Enable actions via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Enable(this IInputContext context, object source, params InputAction[] actions)
		{
			if (actions.Length == 0)
				throw new ArgumentException("Empty actions array");

			foreach (InputAction action in actions) {
				context.EnableAction(source, action);
			}
		}

		/// <summary>
		/// Enable actions via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Enable(this IInputContext context, object source, IEnumerable<InputAction> actions)
		{
			foreach (InputAction action in actions) {
				context.EnableAction(source, action);
			}
		}

		/// <summary>
		/// Enable actions via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Enable(this IInputContext context, object source, InputActionMap actionsMap)
		{
			foreach (InputAction action in actionsMap) {
				context.EnableAction(source, action);
			}
		}

		/// <summary>
		/// Enable action via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static InputAction Enable(this IInputContext context, object source, InputActionReference actionReference)
		{
			var action = context.FindActionFor(actionReference);
			context.EnableAction(source, action);
			return action;
		}

		/// <summary>
		/// Enable actions via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static IEnumerable<InputAction> Enable(this IInputContext context, object source, params InputActionReference[] actionReferences)
		{
			if (actionReferences.Length == 0)
				throw new ArgumentException("Empty actions array");

			var actions = actionReferences.Select(ar => context.FindActionFor(ar));
			context.Enable(source, actions);
			return actions;
		}

		/// <summary>
		/// Enable actions via <see cref="InputActionsMaskedStack"/>
		/// If mask is applied it may not be enabled.
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static IEnumerable<InputAction> Enable(this IInputContext context, object source, IEnumerable<InputActionReference> actionReferences)
		{
			var actions = actionReferences.Select(ar => context.FindActionFor(ar));
			context.Enable(source, actions);
			return actions;
		}

		// --------------------------------------------------------------------------------------------------------- \\

		/// <summary>
		/// Disable action via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Disable(this InputAction action, object source, IInputContext context)
		{
			context.DisableAction(source, action);
		}

		/// <summary>
		/// Disable actions via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Disable(this IInputContext context, object source, params InputAction[] actions)
		{
			if (actions.Length == 0)
				throw new ArgumentException("Empty actions array");

			foreach (InputAction action in actions) {
				context.DisableAction(source, action);
			}
		}

		/// <summary>
		/// Disable actions via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Disable(this IInputContext context, object source, IEnumerable<InputAction> actions)
		{
			foreach (InputAction action in actions) {
				context.DisableAction(source, action);
			}
		}

		/// <summary>
		/// Disable actions via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static void Disable(this IInputContext context, object source, InputActionMap actionsMap)
		{
			foreach (InputAction action in actionsMap) {
				context.DisableAction(source, action);
			}
		}

		/// <summary>
		/// Disable action via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static InputAction Disable(this IInputContext context, object source, InputActionReference actionReference)
		{
			var action = context.FindActionFor(actionReference);
			context.DisableAction(source, action);
			return action;
		}

		/// <summary>
		/// Disable actions via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static IEnumerable<InputAction> Disable(this IInputContext context, object source, params InputActionReference[] actionReferences)
		{
			if (actionReferences.Length == 0)
				throw new ArgumentException("Empty actions array");

			var actions = actionReferences.Select(ar => context.FindActionFor(ar));
			context.Disable(source, actions);
			return actions;
		}

		/// <summary>
		/// Disable actions via <see cref="InputActionsMaskedStack"/>
		/// Always enable/disable actions via this input context.
		///
		/// Enable requests are ref-counted by the source objects. No source object requests, action will be disabled.
		/// </summary>
		public static IEnumerable<InputAction> Disable(this IInputContext context, object source, IEnumerable<InputActionReference> actionReferences)
		{
			var actions = actionReferences.Select(ar => context.FindActionFor(ar));
			context.Disable(source, actions);
			return actions;
		}

		#endregion
	}
}
#endif