#if USE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIInputDisplay
{
	/// <summary>
	/// Displays hotkey icon / text.
	/// Refreshes if devices change.
	/// </summary>
	public class HotkeyDisplayUI : MonoBehaviour, UIScope.IHotkeysWithInputActions, UIScope.IWritableHotkeyInputActionReference
	{
		public enum DisplayModes
		{
			UpdateWithCurrentDevice,
			UpdateWithCurrentDeviceOnlyForControlScheme,
			UpdateWithCurrentDeviceExcludeSchemes,
			DisplaySpecificDeviceIgnoringTheCurrentOne,
		}

		[Serializable]
		public struct DisplayModeData
		{
			[Tooltip("What should the component display.")]
			public DisplayModes Mode;

			[Tooltip("Device layout name to display bindings for.")]
			public string DisplayedDeviceLayout;

			[Tooltip("When excluded, should it keep displaying the last available device bindings, or should it hide everything?")]
			public bool KeepDisplayingLastDevice;

			[InputControlSchemePicker]
			[Tooltip("Control scheme to update for. One control scheme can match multiple devices (e.g. XBox and PS gamepads). Use the picker to avoid typos.")]
			public string DisplayedControlScheme;

			[InputControlSchemePicker]
			[NonReorderable]
			[Tooltip("Control scheme to exclude. Use the picker to avoid typos.")]
			public string[] ExcludedControlSchemes;
		}

		[Serializable]
		public class ExtraSettingsType
		{
			public bool UseShortText = true;

			[Tooltip("Enter how the hotkey text should be displayed. Use \"{Hotkey}\" to be replaced with the matched text.\nLeave empty to skip.")]
			public string FormatText;

			[Tooltip("Should default fallback text be used when no appropriate display data was found?")]
			public IInputContext.InputBehaviourOverride FallbackToDefaultDisplayTexts;
		}

		public InputActionReference InputAction => m_InputAction;

		[SerializeField]
		[FormerlySerializedAs("InputAction")]
		protected InputActionReference m_InputAction;
		// Maybe you'd like to have the option to specify the binding here too.
		// You can do this easily with the InputActionBindingPair class.
		// But that is probably a bad idea, since you'll be locking this display to the binding control scheme.
		// Most likely you'd want to update the display dynamically with the current control scheme.
		// (i.e. binding displays keyboard key, but player switches to a controller).

		[FormerlySerializedAs("TextMeshProText")]
		public TMPro.TextMeshProUGUI Text;

		[Range(0, 5)]
		[Tooltip("If multiple bindings are present in the action matching this device, display the n-th one.\n(i.e. \"alternative binding\")")]
		public int BindingNumberToUse = 0;

		[Range(0, 6)]
		[Tooltip("If matched binding is composite (consists of multiple parts, e.g. WASD or Arrows), display the n-th part of it instead.\nIf value is left 0, it will display the initial composite binding summarized (e.g. \"W/A/S/D\")")]
		public int CompositePartNumberToUse = 0;

		public DisplayModeData DisplayMode;

		public ExtraSettingsType ExtraSettings = new ExtraSettingsType();

		[Tooltip("Optional - list of objects to be activated when hotkeys are displayed. Useful for labels indicating the result of the action.")]
		public List<GameObject> AdditionalObjectsToActivate;

		public InputBindingDisplayData CurrentlyDisplayedData { get; private set; }
		public bool DisplaysIcon { get; private set; }

		private InputDevice m_LastDevice;

		private LayoutElement m_LayoutElement;

		protected bool m_GameQuitting = false;

		// Used for multiple event systems (e.g. split screen).
		protected IPlayerContext m_PlayerContext;

		protected bool m_HasInitialized = false;

		/// <summary>
		/// Call this if you rebind the input or something...
		/// </summary>
		public void RefreshDisplay()
		{
			if (!m_HasInitialized)
				return;

			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyDisplayUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			m_LastDevice = null;

			if (m_InputAction) {
				RefreshDisplay(m_PlayerContext.InputContext);
			}
		}

		/// <summary>
		/// Get input actions used by this component.
		/// </summary>
		public IEnumerable<InputAction> GetUsedActions(IInputContext inputContext)
		{
			if (m_InputAction == null)
				yield break;

#if UNITY_EDITOR
			// For editor purposes.
			if (inputContext == null) {
				yield return m_InputAction;
				yield break;
			}
#endif

			InputAction action = inputContext.FindActionFor(m_InputAction);
			if (action != null) {
				yield return action;
			}
		}

		/// <summary>
		/// Set input action. Will rebind it properly.
		/// </summary>
		public void SetInputAction(InputActionReference inputActionReference)
		{
			bool wasEnabled = Application.isPlaying && isActiveAndEnabled;
			if (wasEnabled) {
				OnDisable();
			}

			m_InputAction = inputActionReference;

			if (wasEnabled) {
				OnEnable();
			}
		}

		private void RefreshDisplay(IInputContext context)
		{
			string deviceLayout;

			if (DisplayMode.Mode != DisplayModes.DisplaySpecificDeviceIgnoringTheCurrentOne) {
				InputDevice device = context.GetLastUsedInputDevice();

				// HACK: Prevent from spamming on PC.
				//		 Keyboard & Mouse are be considered (usually) the same. Gamepads are not - each one comes with its own assets.
				// NOTE: Don't check for (device == m_LastDevice). Keyboard refresh is triggered on changing layout (switching language), but device remains the same.
				if ((device is Keyboard && m_LastDevice is Mouse) || (device is Mouse && m_LastDevice is Keyboard)) {
					m_LastDevice = device;
					return;
				}

				m_LastDevice = device;

				if (DisplayMode.Mode == DisplayModes.UpdateWithCurrentDeviceOnlyForControlScheme) {
					var lastControlScheme = context.GetLastUsedInputControlScheme().bindingGroup;

					if (!DisplayMode.DisplayedControlScheme.Equals(lastControlScheme, StringComparison.OrdinalIgnoreCase)) {

						if (!DisplayMode.KeepDisplayingLastDevice) {
							Text.enabled = false;

							if (m_LayoutElement) {
								m_LayoutElement.ignoreLayout = !Text.enabled;
							}
							SetAdditionalObjects(false);
							return;
						}

						// I'm displayed for the first time - display first available device if current device is to be excluded.
						m_LastDevice = InputSystem.devices
							.FirstOrDefault(d => DisplayMode.DisplayedControlScheme.Equals(context.GetInputControlSchemeFor(d).bindingGroup))
							;
					}
				}

				if (DisplayMode.Mode == DisplayModes.UpdateWithCurrentDeviceExcludeSchemes) {
					var lastControlScheme = context.GetLastUsedInputControlScheme().bindingGroup;

					if (DisplayMode.ExcludedControlSchemes.Contains(lastControlScheme)) {

						if (!DisplayMode.KeepDisplayingLastDevice) {
							Text.enabled = false;

							if (m_LayoutElement) {
								m_LayoutElement.ignoreLayout = !Text.enabled;
							}
							SetAdditionalObjects(false);
							return;
						}

						// I'm displayed for the first time - display first available device if current device is to be excluded.
						m_LastDevice = InputSystem.devices
							.FirstOrDefault(d => !DisplayMode.ExcludedControlSchemes.Contains(context.GetInputControlSchemeFor(d).bindingGroup))
							;
					}
				}

				// Player may not have available devices at the moment. Abort in that case.
				if (m_LastDevice == null)
					return;

				deviceLayout = m_LastDevice.layout;

			} else {
				deviceLayout = DisplayMode.DisplayedDeviceLayout;
			}

			InputAction action = context.FindActionFor(m_InputAction);
			if (action == null) {
				Debug.LogError($"[Input] {nameof(HotkeyDisplayUI)} couldn't find specified action {m_InputAction.name} for player {m_PlayerContext.PlayerName}", this);
				return;
			}

			int count = 0;
			var displayedData = new InputBindingDisplayData();

			IInputBindingDisplayDataProvider displayDataProvider = context.GetFirstMatchingDisplayDataProvider(deviceLayout);

			// No display asset found for this scheme - abort.
			// This may happen when you add "VirtualMouse" device that comes with this layout.
			if (displayDataProvider == null)
				return;

			foreach (var bindingDisplay in displayDataProvider.GetBindingDisplaysFor(action)) {

				if (count == BindingNumberToUse) {

					if (CompositePartNumberToUse == 0) {
						displayedData = bindingDisplay;
					} else if (CompositePartNumberToUse - 1 < bindingDisplay.CompositeBindingParts.Count) {
						displayedData = bindingDisplay.CompositeBindingParts[CompositePartNumberToUse - 1];
					}

					if (displayedData.IsFallback && !ExtraSettings.FallbackToDefaultDisplayTexts.FinalValue(displayDataProvider.FallbackToDefaultDisplayTexts)) {
						displayedData.ShortText = "";
						displayedData.Text = "";
					}

					break;
				}
				count++;
			}

			displayedData.DeviceLayout = deviceLayout;
			CurrentlyDisplayedData = displayedData;
			DisplaysIcon = false;

			string usedText = ExtraSettings.UseShortText && !string.IsNullOrWhiteSpace(CurrentlyDisplayedData.ShortText)
				? CurrentlyDisplayedData.ShortText
				: CurrentlyDisplayedData.Text
				;

			if (!string.IsNullOrEmpty(usedText) && !string.IsNullOrWhiteSpace(ExtraSettings.FormatText)) {
				usedText = ExtraSettings.FormatText.Replace("{Hotkey}", usedText);
			}

			Text.enabled = CurrentlyDisplayedData.HasText;
			Text.text = displayDataProvider.FormatBindingDisplayText(usedText);
			DisplaysIcon = usedText != null ? usedText.Contains("<sprite") : false;

			if (m_LayoutElement) {
				m_LayoutElement.ignoreLayout = !Text.enabled;
			}
			SetAdditionalObjects(Text.enabled);
		}

		protected virtual void Reset()
		{
			var hotkey = GetComponentInParent<UIScope.HotkeyBaseScopeElement>(true);
			if (hotkey) {
				m_InputAction = hotkey.InputAction;
			}

			Text = GetComponent<TMPro.TextMeshProUGUI>();
		}

		protected virtual void Awake()
		{
			m_PlayerContext = PlayerContextUtils.GetPlayerContextFor(gameObject);

			m_LayoutElement = GetComponent<LayoutElement>();
			if (Text == null) {
				Text = GetComponent<TMPro.TextMeshProUGUI>();

				if (Text == null) {
					Debug.LogError($"\"{name}\" needs to display input action, but no text component available to do so.", this);
				}
			}

			m_PlayerContext.AddSetupCallback((delayedSetup) => {
				m_HasInitialized = true;

				if (delayedSetup && isActiveAndEnabled) {
					OnEnable();
				}
			});
		}

		protected virtual void OnEnable()
		{
			if (!m_HasInitialized)
				return;

			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyDisplayUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			m_PlayerContext.InputContext.LastUsedDeviceChanged += OnLastUsedDeviceChanged;
			m_LastDevice = null;

			if (m_InputAction) {
				RefreshDisplay(m_PlayerContext.InputContext);

			}
		}

		protected virtual void OnDisable()
		{
			if (!m_HasInitialized)
				return;

			if (m_PlayerContext.InputContext == null) {
				if (!m_GameQuitting) {
					Debug.LogWarning($"[Input] {nameof(HotkeyDisplayUI)} button {name} can't be used if Unity Input System is not provided.", this);
					enabled = false;
				}
				return;
			}

			Text.enabled = false;
			SetAdditionalObjects(false);

			m_PlayerContext.InputContext.LastUsedDeviceChanged -= OnLastUsedDeviceChanged;
		}

		private void SetAdditionalObjects(bool active)
		{
			// HACK: Cause Unity can sometimes miss those... just for old serialized data...
			if (AdditionalObjectsToActivate == null)
				return;

			foreach(GameObject go in AdditionalObjectsToActivate) {
				if (go) {
					go.SetActive(active);
				}
			}
		}

		private void OnLastUsedDeviceChanged()
		{
			if (m_PlayerContext.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(HotkeyDisplayUI)} button {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			// Keyboard/Mouse check is done in the RefreshDisplay() method. Don't do it here.

			if (m_InputAction) {
				RefreshDisplay(m_PlayerContext.InputContext);
			}
		}

		protected virtual void OnValidate()
		{
			Utils.Validation.ValidateMissingObject(this, m_InputAction, nameof(m_InputAction));
			Utils.Validation.ValidateMissingObject(this, Text, nameof(TMPro.TextMeshProUGUI));
		}

		void OnApplicationQuit()
		{
			m_GameQuitting = true;
		}
	}

#if UNITY_EDITOR
	[UnityEditor.CustomPropertyDrawer(typeof(HotkeyDisplayUI.DisplayModeData))]
	internal class HotkeyDisplayUIUpdateModeDataPropertyDrawer : UnityEditor.PropertyDrawer
	{
		public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
		{
			var modeProperty = property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.Mode));
			var mode = (HotkeyDisplayUI.DisplayModes)modeProperty.intValue;

			float height = UnityEditor.EditorGUIUtility.singleLineHeight;

			switch(mode) {
				case HotkeyDisplayUI.DisplayModes.DisplaySpecificDeviceIgnoringTheCurrentOne:
					height += UnityEditor.EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedDeviceLayout)), label);
					break;
				case HotkeyDisplayUI.DisplayModes.UpdateWithCurrentDeviceOnlyForControlScheme:
					height += UnityEditor.EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.KeepDisplayingLastDevice)), label);
					height += UnityEditor.EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedControlScheme)), label);
					break;
				case HotkeyDisplayUI.DisplayModes.UpdateWithCurrentDeviceExcludeSchemes:
					height += UnityEditor.EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.KeepDisplayingLastDevice)), label);
					height += UnityEditor.EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.ExcludedControlSchemes)), label);
					break;
			}

			return height;
		}

		public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
		{
			label = UnityEditor.EditorGUI.BeginProperty(position, label, property);

			Rect lineRect = position;
			lineRect.height = UnityEditor.EditorGUIUtility.singleLineHeight;

			var modeProperty = property.FindPropertyRelative("Mode");
			UnityEditor.EditorGUI.PropertyField(lineRect, modeProperty, label);

			lineRect.y += UnityEditor.EditorGUIUtility.singleLineHeight + UnityEditor.EditorGUIUtility.standardVerticalSpacing;

			var mode = (HotkeyDisplayUI.DisplayModes) modeProperty.intValue;
			UnityEditor.EditorGUI.indentLevel++;
			switch(mode) {
				case HotkeyDisplayUI.DisplayModes.UpdateWithCurrentDevice:
					if (property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedDeviceLayout)).stringValue != string.Empty) {
						property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedDeviceLayout)).stringValue = string.Empty;
					}
					if (property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.ExcludedControlSchemes)).arraySize != 0) {
						property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.ExcludedControlSchemes)).arraySize = 0;
					}
					break;

				case HotkeyDisplayUI.DisplayModes.DisplaySpecificDeviceIgnoringTheCurrentOne:
					UnityEditor.EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedDeviceLayout)));
					break;
				case HotkeyDisplayUI.DisplayModes.UpdateWithCurrentDeviceOnlyForControlScheme:
					UnityEditor.EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.KeepDisplayingLastDevice)), true);
					lineRect.y += UnityEditor.EditorGUIUtility.singleLineHeight + UnityEditor.EditorGUIUtility.standardVerticalSpacing;
					UnityEditor.EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.DisplayedControlScheme)), true);
					break;
				case HotkeyDisplayUI.DisplayModes.UpdateWithCurrentDeviceExcludeSchemes:
					UnityEditor.EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.KeepDisplayingLastDevice)), true);
					lineRect.y += UnityEditor.EditorGUIUtility.singleLineHeight + UnityEditor.EditorGUIUtility.standardVerticalSpacing;
					UnityEditor.EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(HotkeyDisplayUI.DisplayModeData.ExcludedControlSchemes)), true);
					break;
			}
			UnityEditor.EditorGUI.indentLevel--;

			UnityEditor.EditorGUI.EndProperty();
		}
	}
#endif

}
#endif