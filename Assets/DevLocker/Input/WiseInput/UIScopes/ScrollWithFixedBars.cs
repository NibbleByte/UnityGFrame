using UnityEngine;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Forces scroll view to have fixed bar sizes.
	/// Because there is no official way to do it and there are many ways to hack it.
	/// </summary>
	public class ScrollWithFixedBars : MonoBehaviour
	{
		public ScrollRect ScrollRect;

		public float HorizontalBarSize = 0;
		public float VerticalBarSize = 0;

		void Reset()
		{
			ScrollRect = GetComponent<ScrollRect>();
		}

		void OnEnable()
		{
			if (ScrollRect == null) {
				ScrollRect = GetComponent<ScrollRect>();
			}

			if (ScrollRect) {
				EnforceScrollbarSize();

				// Make sure the static constructor is called first, so CanvasUpdateRegistry.PerformUpdate event handler runs before ours.
				CanvasUpdateRegistry.instance.Equals(null);

				// Subscribing for ScrollRect.onValueChanged is a well known hack, that has issues.
				// When scrollbar value is set by code, it will trigger rebuild which will reset the size: ScrollRect.Rebuild() -> UpdateScrollbars().
				// We won't get notified about this - onValueChanged is called only by user input.
				// So subscribe for Canvas.willRenderCanvases and make sure that damn size stays fixed!
				Canvas.willRenderCanvases += EnforceScrollbarSize;
			}
		}

		void OnDisable()
		{
			if (ScrollRect) {
				Canvas.willRenderCanvases -= EnforceScrollbarSize;
			}

		}

		private void EnforceScrollbarSize()
		{
			if (ScrollRect.horizontalScrollbar && ScrollRect.horizontalScrollbar.size != HorizontalBarSize) {
				ScrollRect.horizontalScrollbar.size = HorizontalBarSize;
			}

			if (ScrollRect.verticalScrollbar && ScrollRect.verticalScrollbar.size != VerticalBarSize) {
				ScrollRect.verticalScrollbar.size = VerticalBarSize;
			}
		}
	}
}