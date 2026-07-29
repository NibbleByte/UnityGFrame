using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevLocker.WiseInput.UIScope
{
	/// <summary>
	/// Wrap provider that returns first active <see cref="Selectable"/> from the list.
	/// </summary>
	public class UINavigationWrapProviderSelectablesList : UINavigationWrapProviderBase
	{
		public Selectable[] Selectables;

		[Tooltip("If no match is found, use this wrap provider.")]
		public UINavigationWrapProviderBase Fallback;

		public override Selectable GetNextSelectable(UINavigationGroup navGroup, Selectable prevSelected, AxisEventData eventData)
		{
			foreach (var selectable in Selectables) {
				if (selectable && selectable.isActiveAndEnabled && selectable.IsInteractable()) {
					return selectable;
				}
			}

			if (Fallback) {
				return Fallback.GetNextSelectable(navGroup, prevSelected, eventData);
			}

			return null;
		}
	}
}