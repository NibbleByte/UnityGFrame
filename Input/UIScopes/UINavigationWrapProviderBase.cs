using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Base class for <see cref="UINavigationGroup"/> wrap providers.
	/// These allow you to have custom wrap handling in case the default options are not enough.
	/// </summary>
	public abstract class UINavigationWrapProviderBase : MonoBehaviour
	{
		public abstract Selectable GetNextSelectable(UINavigationGroup navGroup, Selectable prevSelected, AxisEventData eventData);
	}
}