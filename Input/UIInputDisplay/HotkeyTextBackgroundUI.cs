#if USE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIInputDisplay
{
	/// <summary>
	/// This component shows or hides it's image component when the <see cref="HotkeyDisplayUI"/> is showing text based info (not icon).
	/// Use to "add" background behind hotkeys that don't use icons (i.e. keyboard letter bindings).
	/// Because the hotkey text needs to render in front, best add it as a child of this component and link it.
	/// </summary>
	[RequireComponent(typeof(Image))]
	public class HotkeyTextBackgroundUI : MonoBehaviour
	{
		public HotkeyDisplayUI HotkeyDisplayUI;

		private Image m_Image;
		private LayoutElement m_LayoutElement;

		void Awake()
		{
			HotkeyDisplayUI.Refreshed += OnHotkeyRefreshed;

			m_Image = GetComponent<Image>();
			m_LayoutElement = GetComponent<LayoutElement>();
		}

		void OnEnable()
		{
			OnHotkeyRefreshed();
		}

		private void OnHotkeyRefreshed()
		{
			m_Image.enabled = !HotkeyDisplayUI.DisplaysIcon && HotkeyDisplayUI.CurrentlyDisplayedData.HasText;

			if (m_LayoutElement && HotkeyDisplayUI.ExtraSettings.DisableLayoutElementWhenHidden) {
				m_LayoutElement.ignoreLayout = !m_Image.enabled;
			}
		}
	}


}
#endif