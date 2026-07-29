using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Wrap provider that returns <see cref="Selectable"/> from the first matched <see cref="UINavigationGroup"/>
	/// </summary>
	public class UINavigationWrapProviderGroupsList : UINavigationWrapProviderBase
	{
		public enum SelectableFromGroup
		{
			AutoSelectableOfNavigationGroup = 4,
			FirstSelectableOfNavigationGroup = 8,   // NOTE: These might not be what you expect if arrangement is more irregular...
			LastSelectableOfNavigationGroup = 16,   // NOTE: These might not be what you expect if arrangement is more irregular...
		}

		public SelectableFromGroup TargetSelectable;
		public UINavigationGroup[] Groups;

		[Tooltip("If no match is found, use this wrap provider.")]
		public UINavigationWrapProviderBase Fallback;

		public override Selectable GetNextSelectable(UINavigationGroup navGroup, Selectable prevSelected, AxisEventData eventData)
		{
			Selectable nextSelectable;

			foreach (var group in Groups) {

				if (group == null || !group.isActiveAndEnabled)
					continue;

				switch (TargetSelectable) {

					// Copy-pasted from UINavigationGroup.OnMoveWrapDynamic()
					case SelectableFromGroup.AutoSelectableOfNavigationGroup:

						// In case we're using worldspace canvas with arbitrary rotation.
						// So up will point up according to me and all selectables.
						var dir = prevSelected.transform.rotation * eventData.moveVector;

						nextSelectable = UINavigationGroup.FindSelectableInDirection(prevSelected, dir, group.ManagedSelectables);

						if (nextSelectable == null) {
							nextSelectable = UINavigationGroup.FindSelectableInDirection(prevSelected, -dir, group.ManagedSelectables);

							// Failed to find any appropriate selectable from that group.
							if (nextSelectable == null) {
								return null;
							}

							int sanityCount = 0;
							const int sanityCountLimit = 10000;

							Selectable it = nextSelectable;
							while (it) {

								Selectable itNext = eventData.moveDir switch {
									// Move in the opposite direction!!!
									MoveDirection.Up => it.navigation.selectOnDown,
									MoveDirection.Down => it.navigation.selectOnUp,
									MoveDirection.Left => it.navigation.selectOnRight,
									MoveDirection.Right => it.navigation.selectOnLeft,
									_ => throw new NotSupportedException(eventData.moveDir.ToString())
								};

								// Edge or looped links.
								if (itNext == null || itNext == nextSelectable)
									break;

								it = itNext;

								sanityCount++;
								if (sanityCount > sanityCountLimit) {
									Debug.LogError($"[Input] Navigation group couldn't wrap around {prevSelected} for group {group}!", this);
									return null;
								}
							}

							nextSelectable = it;
						}

						return nextSelectable;

					case SelectableFromGroup.FirstSelectableOfNavigationGroup:
						return group.FirstSelectable;

					case SelectableFromGroup.LastSelectableOfNavigationGroup:
						return group.LastSelectable;
				}
			}

			if (Fallback) {
				return Fallback.GetNextSelectable(navGroup, prevSelected, eventData);
			}

			return null;
		}
	}
}