#if USE_INPUT_SYSTEM
using DevLocker.GFrame.Input.Contexts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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


		public Sprite Icon;
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

		public bool HasIcon => Icon != null;
		public bool HasText => !string.IsNullOrWhiteSpace(Text) || !string.IsNullOrWhiteSpace(ShortText);

		public override string ToString() => $"{Binding.name} - {ShortText}";
	}

	/// <summary>
	/// Provides the required binding representations to display hotkeys in the UI for specific device.
	/// </summary>
	public interface IInputBindingDisplayDataProvider
	{
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
	/// Manages player context and input. This also should be a MonoBehavior marking the UI hierarchy used by the specific player.
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
		/// Get arbitrary object from this player root. Useful for attaching level state stack or similar per player.
		/// </summary>
		T GetContextReference<T>();

		/// <summary>
		/// Get the top-most root object.
		/// </summary>
		PlayerContextUIRootObject GetRootObject();
	}

	/// <summary>
	/// Implement this if your game uses Unity Input system with generated IInputActionCollection.
	/// </summary>
	public interface IInputContext : IDisposable
	{
		/// <summary>
		/// Notifies if any player joined or left, or any other action that would require a refresh.
		/// </summary>
		event Action PlayersChanged;

		/// <summary>
		/// Last device used got changed.
		/// </summary>
		event Action LastUsedDeviceChanged;

		/// <summary>
		/// Find InputAction by action name or id.
		/// </summary>
		InputAction FindActionFor(string actionNameOrId, bool throwIfNotFound = false);

		/// <summary>
		/// Push a new entry in the input actions stack, by specifying who is the source of the request.
		/// All Enable() / Disable() InputAction calls after that belong to the newly pushed (top) entry.
		/// If resetActions is true, all InputActions will be disabled after this call.
		/// Previous top entry will record the InputActions enabled flags at the moment and re-apply them when it is reactivated.
		/// It is strongly recommended to implement this method using <see cref="InputActionsStack" />.
		/// </summary>
		void PushActionsState(object source, bool resetActions = true);

		/// <summary>
		/// Removes an entry made from the specified source in the input actions stack.
		/// If that entry was the top of the stack, next entry state's enabled flags are applied to the InputActions.
		/// It is strongly recommended to implement this method using <see cref="InputActionsStack" />.
		/// </summary>
		bool PopActionsState(object source);

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
		/// Get all used <see cref="InputControlScheme" />.
		/// </summary>
		IEnumerable<InputControlScheme> GetAllInputControlSchemes();

		/// <summary>
		/// Get the display representations of the matched device for the passed action.
		/// An action can have multiple bindings for the same device.
		/// </summary>
		IEnumerable<InputBindingDisplayData> GetBindingDisplaysFor(string deviceLayout, InputAction action);
	}

	public class PlayerContextUtils
	{
		public static PlayerContextUIRootObject GlobalPlayerContext => PlayerContextUIRootObject.GlobalPlayerContext;

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
	}

	public static class InputSystemIntegrationExtensions
	{
		/// <summary>
		/// Get the display representations of the matched device for the passed action.
		/// An action can have multiple bindings for the same device.
		/// </summary>
		public static IEnumerable<InputBindingDisplayData> GetBindingDisplaysFor(this IInputContext context, InputAction action)
		{
			InputDevice lastUsedDevice = context.GetLastUsedInputDevice();
			if (lastUsedDevice == null) {
				return Enumerable.Empty<InputBindingDisplayData>();
			}

			return context.GetBindingDisplaysFor(lastUsedDevice.layout, action);
		}

		public static InputControlScheme GetInputControlSchemeFor(this IInputContext context, InputDevice device)
		{
			return context.GetAllInputControlSchemes().FirstOrDefault(c => c.SupportsDevice(device));
		}
	}
}
#endif