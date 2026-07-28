using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DevLocker.GFrame.Input.UIInputDisplay
{
	/// <summary>
	/// Attach this component next to <see cref="TextMeshProUGUI"/> and it will replace and update any displayed InputActions in the text.
	/// Input actions should be surrounded by curly braces: {Jump}
	/// You can also specify which binding to use (if multiple are present) and which part (if it is composite, e.g. axis)
	///	by separating the zero based numbers with | character: {Jump|1|2}
	///
	/// Text changes by other code are automatically detected and refreshed.
	/// </summary>
	[RequireComponent(typeof(TextMeshProUGUI))]
	public class TextInlineInputDisplayUI : MonoBehaviour
	{
		public class TextToken
		{
			public readonly bool IsAction;
			public readonly int StartIndex;
			public readonly string OriginalText;
			public readonly string DisplayText;
			public readonly InputAction Action;

			public readonly int BindingNumberToUse;
			public readonly int CompositePartNumberToUse;

			public bool DisplaysIcon => IsAction && DisplayText.Contains("<sprite");

			public TextToken(int startIndex, string originalText, string displayText, InputAction action, int bindingNumberToUse, int compositePartNumberToUse)
			{
				IsAction = true;
				StartIndex = startIndex;
				OriginalText = originalText;
				DisplayText = displayText;
				Action = action;
				BindingNumberToUse = bindingNumberToUse;
				CompositePartNumberToUse = compositePartNumberToUse;
			}
			public TextToken(int startIndex, string originalText)
			{
				IsAction = false;
				StartIndex = startIndex;
				OriginalText = originalText;
			}
		}

		[Serializable]
		public class ExtraSettingsType
		{
			public bool UseShortText = true;

			[Tooltip("Should default fallback text be used when no appropriate display data was found?")]
			public IInputContext.InputBehaviourOverride FallbackToDefaultDisplayTexts;

			[Tooltip("Disable the text mesh pro component if input action for the current device is unavailable or fallback is not desired.\nIf layout element is present on this object, it will set it to ignore the layout as well.")]
			public bool HideTextIfBindingUnavailable = true;

			[Tooltip("When hotkey is not visible only the text component is disabled, which may still affect the layout.\n\nEnable this to also set attached LayoutElement component to \"Ignore Layout\".")]
			public bool DisableLayoutElementWhenHidden = false;
		}

		[Space]
		[Tooltip("(Optional) Format selected binding display text if it doesn't use sprites.\n\"{binding}\" will be replaced with the binding display text.")]
		public string FormatBindingTexts = "";
		[Tooltip("(Optional) Format selected binding display text if it contains sprites.\n\"{binding}\" will be replaced with the binding display text.")]
		public string FormatBindingSprites = "";

		[Tooltip("This prefab will be instantiated and placed BEHIND the hotkeys represented by texts (not sprites). It is set as sibling BEFORE this object to render in the correct order.")]
		public RectTransform BackgroundForTexts;

		public ExtraSettingsType ExtraSettings = new ExtraSettingsType();

		private static Regex s_ActionPattern = new Regex(@"\{[\w\d]+(\|\d+){0,2}\}");

		private TextMeshProUGUI m_Text;
		private LayoutElement m_LayoutElement;
		private bool m_ChangingText = false;

		// Contains ALL tokens (normal text and hotkey indicators).
		public IReadOnlyList<TextToken> DisplayedTokens => m_DisplayedTokens;
		private List<TextToken> m_DisplayedTokens = new List<TextToken>();
		public string OriginalText { get; private set; } = "";
		private string m_LastProcessedText = "";

		private List<RectTransform> m_BackgroundsPool = new List<RectTransform>();

		private IInputBindingDisplayDataProvider m_LastDisplayDataProvider;


		// Used for multiple event systems (e.g. split screen).
		protected IInputUIRoot m_InputUIRoot;

		protected bool m_HasInitialized = false;

		void Awake()
		{
			m_InputUIRoot = InputContextUtils.GetInputUIRootFor(gameObject);

			m_Text = GetComponent<TextMeshProUGUI>();
			m_LayoutElement = GetComponent<LayoutElement>();

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

			if (m_Text) {
				// This will be called right after the change happens, but before layout rebuild happens, so it shouldn't be slow.
				m_Text.RegisterDirtyLayoutCallback(OnTextLayoutChanged);

				// This is called after meshes were generated.
				// NOTE: Doesn't allow adding or enabling background elements at this time.
				//TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChangeFinished);
			}

			if (m_InputUIRoot.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(TextInlineInputDisplayUI)} {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			m_InputUIRoot.InputContext.LastUsedDeviceChanged += OnLastUsedDeviceChanged;

			OnLastUsedDeviceChanged();

			// Because OnLastUsedDeviceChanged() may NOT refresh the text, which will not enable the backgrounds.
			if (BackgroundForTexts && m_DisplayedTokens.Any(t => t.IsAction && !t.DisplaysIcon) && (!m_BackgroundsPool.FirstOrDefault()?.gameObject.activeSelf ?? true)) {
				RefreshTextBackgrounds();
			}
		}

		void OnDisable()
		{
			if (!m_HasInitialized)
				return;

			if (m_Text) {
				m_Text.UnregisterDirtyLayoutCallback(OnTextLayoutChanged);
			}

			if (m_InputUIRoot.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(TextInlineInputDisplayUI)} {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			foreach (RectTransform background in m_BackgroundsPool) {
				if (background) {
					background.gameObject.SetActive(false);
				}
			}

			m_InputUIRoot.InputContext.LastUsedDeviceChanged -= OnLastUsedDeviceChanged;
		}

		void OnDestroy()
		{
			foreach (RectTransform background in m_BackgroundsPool) {
				if (background) {
					Destroy(background.gameObject);
				}
			}
		}

		public void RefreshTextInputSprites()
		{
			if (m_InputUIRoot.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(TextInlineInputDisplayUI)} {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			var currentProvider = m_InputUIRoot.InputContext.GetCurrentDisplayDataProvider();
			if (currentProvider == null)
				return;

			// If text was changed, use that and update our cache.
			if (m_Text.text != m_LastProcessedText) {
				OriginalText = m_Text.text;
			}

			m_LastDisplayDataProvider = currentProvider;
			m_DisplayedTokens.Clear();

			// Just in case someone destroyed them.
			m_BackgroundsPool.RemoveAll(b => b == null);

			MatchCollection matches = s_ActionPattern.Matches(OriginalText);
			if (matches.Count == 0) {
				// Will be activated when the meshes are generated, check the OnTextChangeFinished callback.
				foreach (RectTransform background in m_BackgroundsPool) {
					background.gameObject.SetActive(false);
				}

				// If text doesn't contain any action names, just leave it?
				m_Text.enabled = true; //!ExtraSettings.HideTextIfBindingUnavailable;

				if (m_LayoutElement && ExtraSettings.DisableLayoutElementWhenHidden) {
					m_LayoutElement.ignoreLayout = !m_Text.enabled;
				}
				return;
			}

			bool shouldHideText = false;
			int prevOriginalIndex = 0;
			int processedLength = 0;

			foreach (Match match in matches) {
				m_DisplayedTokens.Add(new TextToken(processedLength, OriginalText.Substring(prevOriginalIndex, match.Index - prevOriginalIndex)));
				processedLength += m_DisplayedTokens.Last().OriginalText.Length;

				prevOriginalIndex = match.Index + match.Value.Length;

				// Remove the curly braces {}
				string actionName = match.Value.Substring(1, match.Value.Length - 2);
				int bindingNumberToUse = 0;
				int compositePartNumberToUse = 0;
				if (actionName.Contains('|')) {
					var matchArgs = actionName.Split('|', StringSplitOptions.RemoveEmptyEntries);

					actionName = matchArgs[0];

					if (!int.TryParse(matchArgs[1], out bindingNumberToUse)) {
						Debug.LogError($"Invalid second parameter used for bindingNumberToUse in \"{match.Value}\", part of the text:\n{OriginalText}");
					}

					if (matchArgs.Length > 2 && !int.TryParse(matchArgs[2], out compositePartNumberToUse)) {
						Debug.LogError($"Invalid third parameter used for compositePartNumberToUse in \"{match.Value}\", part of the text:\n{OriginalText}");
					}
				}

				InputAction action = m_InputUIRoot.InputContext.FindActionFor(actionName);

				string displayText = GetDisplayTextFor(action, currentProvider, bindingNumberToUse, compositePartNumberToUse);
				if (!string.IsNullOrEmpty(displayText)) {

					m_DisplayedTokens.Add(new TextToken(processedLength, match.Value, displayText, action, bindingNumberToUse, compositePartNumberToUse));
					processedLength += displayText.Length;

				} else {

					shouldHideText = true;

					m_DisplayedTokens.Add(new TextToken(processedLength, match.Value));
					processedLength += match.Value.Length;
				}
			}

			// Remainder of the text.
			if (prevOriginalIndex < OriginalText.Length) {
				m_DisplayedTokens.Add(new TextToken(processedLength, OriginalText.Substring(prevOriginalIndex, OriginalText.Length - prevOriginalIndex)));
			}

			m_LastProcessedText = string.Join("", m_DisplayedTokens.Select(t => t.IsAction ? t.DisplayText : t.OriginalText));

			m_Text.enabled = !ExtraSettings.HideTextIfBindingUnavailable || !shouldHideText;
			if (m_LayoutElement && ExtraSettings.DisableLayoutElementWhenHidden) {
				m_LayoutElement.ignoreLayout = !m_Text.enabled;
			}

			m_ChangingText = true;
			m_Text.text = m_LastProcessedText;
			m_ChangingText = false;

			if (BackgroundForTexts && m_DisplayedTokens.Any(t => t.IsAction && !t.DisplaysIcon)) {
				m_Text.ForceMeshUpdate();
				RefreshTextBackgrounds();
			} else {
				foreach (RectTransform background in m_BackgroundsPool) {
					background.gameObject.SetActive(false);
				}
			}
		}

		protected virtual void RefreshTextBackgrounds()
		{
			if (BackgroundForTexts == null)
				return;

			int backgroundIndex = 0;
			var infos = m_Text.textInfo.characterInfo.Take(m_Text.textInfo.characterCount).ToArray();

			foreach (TextToken token in m_DisplayedTokens) {
				if (token.IsAction && !token.DisplaysIcon) {

					if (backgroundIndex >= m_BackgroundsPool.Count) {
						var instance = Instantiate(BackgroundForTexts, m_Text.transform.parent);
						instance.SetSiblingIndex(m_Text.transform.GetSiblingIndex());
						instance.name = $"__{BackgroundForTexts.name}_{m_BackgroundsPool.Count}";
						m_BackgroundsPool.Add(instance);
					}

					var background = m_BackgroundsPool[backgroundIndex];
					background.gameObject.SetActive(true);
					backgroundIndex++;

					int startOriginalIndex = token.StartIndex;
					int endOriginalIndex = token.StartIndex + token.DisplayText.Length - 1;

					// Infos contain text only without rich text meta tags. But we can use index.
					int startInfoIndex = Array.FindIndex(infos, n => startOriginalIndex <= n.index && n.index <= endOriginalIndex);
					int endInfoIndex = Array.FindLastIndex(infos, n => startOriginalIndex <= n.index && n.index <= endOriginalIndex);

					PositionTextBackground(background, token, infos, startInfoIndex, endInfoIndex);
				}
			}

			for(; backgroundIndex < m_BackgroundsPool.Count; backgroundIndex++) {
				m_BackgroundsPool[backgroundIndex].gameObject.SetActive(false);
			}
		}

		// It's easy to add background behind 1-letter hotkey representations, but there are various way to represent multi-leter ones.
		// Best option is to use icons for them. In case you need text+background, you can extend this method and customize the behaviour.
		protected virtual void PositionTextBackground(RectTransform background, TextToken token, TMP_CharacterInfo[] infos, int startInfoIndex, int endInfoIndex)
		{
			// Infos contain text only without meta tags.
			// TMP_CharacterInfo.index stores the source index of that character.
			var startInfo = infos[startInfoIndex];
			var endInfo = infos[endInfoIndex];
			int displayedCharactersCount = (endInfoIndex - startInfoIndex) + 1;

			Vector3 worldCenter = m_Text.transform.TransformPoint(startInfo.bottomLeft + (endInfo.topRight - startInfo.bottomLeft) / 2f);

			// Get original size, current may be modified.
			Vector2 backgroundSize = BackgroundForTexts.sizeDelta;

			// More than one letter - expand the background.
			if (startInfo.index != endInfo.index) {
				backgroundSize.x += backgroundSize.x * 0.25f * displayedCharactersCount;
			}

			background.position = worldCenter;
			background.sizeDelta = backgroundSize;
		}


		protected virtual string GetDisplayTextFor(InputAction action, IInputBindingDisplayDataProvider displayDataProvider, int bindingNumberToUse, int compositePartNumberToUse)
		{
			int count = 0;
			var displayData = new InputBindingDisplayData();

			foreach (var bindingDisplay in displayDataProvider.GetBindingDisplaysFor(action)) {
				if (count == bindingNumberToUse) {

					if (compositePartNumberToUse == 0) {
						displayData = bindingDisplay;
					} else if (compositePartNumberToUse - 1 < bindingDisplay.CompositeBindingParts.Count) {
						displayData = bindingDisplay.CompositeBindingParts[compositePartNumberToUse - 1];
					}

					break;
				}
				count++;
			}


			// Probably no match on binding number or composite part number.
			if (!displayData.IsValid && !ExtraSettings.HideTextIfBindingUnavailable) {
				displayData = displayDataProvider.GetBindingDisplaysFor(action).LastOrDefault();
			}

			if (!displayData.IsValid)
				return string.Empty;

			if (displayData.IsFallback && !ExtraSettings.FallbackToDefaultDisplayTexts.FinalValue(displayDataProvider.FallbackToDefaultDisplayTexts)) {
				return string.Empty;
			}

			if (displayData.Text.Contains("<sprite")) {
				string locallyFormatted = string.IsNullOrWhiteSpace(FormatBindingSprites) ? displayData.Text : FormatBindingSprites.Replace("{binding}", displayData.Text, StringComparison.OrdinalIgnoreCase);
				return displayDataProvider.FormatBindingDisplayText(locallyFormatted);
			} else {
				string hotkeyText = ExtraSettings.UseShortText ? displayData.ShortText : displayData.Text;

				// Add <b> tag to store the input action in an attribute so we can recognize it later and update it if needed.
				string displayText = $"<b inputAction=\"{action.name}\">{hotkeyText}</b>";
				string locallyFormatted = string.IsNullOrWhiteSpace(FormatBindingTexts) ? displayText : FormatBindingTexts.Replace("{binding}", displayText, StringComparison.OrdinalIgnoreCase);

				return displayDataProvider.FormatBindingDisplayText(locallyFormatted);
			}
		}

		private void OnTextLayoutChanged()
		{
			// Called by change we are making at the moment.
			if (m_ChangingText)
				return;

			RefreshTextInputSprites();
		}

		private void OnLastUsedDeviceChanged()
		{
			if (m_InputUIRoot.InputContext == null) {
				Debug.LogWarning($"[Input] {nameof(TextInlineInputDisplayUI)} {name} can't be used if Unity Input System is not provided.", this);
				enabled = false;
				return;
			}

			var currentProvider = m_InputUIRoot.InputContext.GetCurrentDisplayDataProvider();
			if (currentProvider == null)
				return;

			if (m_LastDisplayDataProvider == null || m_LastDisplayDataProvider != currentProvider) {
				// NOTE: This will not update on changing keyboard layout/language. Someday...
				RefreshTextInputSprites();
			}
		}
	}


#if UNITY_EDITOR
	[CustomEditor(typeof(TextInlineInputDisplayUI))]
	[CanEditMultipleObjects]
	internal class TextInlineInputDisplayUIEditor : Editor
	{
		private bool m_FoldOut = true;

		protected void DrawScriptProperty()
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
			EditorGUI.EndDisabledGroup();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawScriptProperty();

			m_FoldOut = EditorGUILayout.Foldout(m_FoldOut, "Hint");
			if (m_FoldOut) {
				EditorGUILayout.HelpBox("Will replace and update any displayed InputActions in the text.\nInput actions should be surrounded by curly braces: {Jump}\n\n" +
					"You can also specify which binding to use (if multiple are present) and which part (if it is composite, e.g. axis) " +
					"by separating the zero based numbers with | character in this order: {ActionName|binding index|composite part index}\n" +
					"{Move|1|3} - will display second binding (0 is first one), second composite part (0 is all parts)", MessageType.Info);
			}

			EditorGUI.BeginChangeCheck();

			// Will draw any child properties without [HideInInspector] attribute.
			DrawPropertiesExcluding(serializedObject, "m_Script");

			if (EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
#endif
}