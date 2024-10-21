using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DevLocker.GFrame.Input.UIScope
{
	/// <summary>
	/// Forces scroll view to follow selection.
	/// </summary>
	public class ScrollWithSelected : MonoBehaviour
	{
		public enum EasingType
		{
			SnapInstant,
			Linear,
			OutQuad,
			OutQuart,
			OutQuint,
			OutExpo,
		}

		public ScrollRect ScrollRect;

		[Tooltip("Margin when snapping")]
		public RectOffset Margin;

		public EasingType Easing = EasingType.OutExpo;
		public float EasingDuration = 0.5f;

		// Used for multiple event systems (e.g. split screen).
		protected IPlayerContext m_PlayerContext;

		private GameObject m_LastSelectedObject;
		private Coroutine m_EaseCoroutine;


		void Reset()
		{
			ScrollRect = GetComponent<ScrollRect>();
		}

		void Awake()
		{
			if (ScrollRect == null) {
				ScrollRect = GetComponent<ScrollRect>();
			}

			m_PlayerContext = PlayerContextUtils.GetPlayerContextFor(gameObject);
		}

		void LateUpdate()
		{
			if (!m_PlayerContext.IsActive)
				return;

			if (m_LastSelectedObject != m_PlayerContext.SelectedGameObject) {
				m_LastSelectedObject = m_PlayerContext.SelectedGameObject;

				if (m_LastSelectedObject && m_LastSelectedObject.transform.IsChildOf(ScrollRect.transform)) {
					ScrollToChild(m_LastSelectedObject.GetComponent<RectTransform>(), useEasing: true);
				}
			}
		}

		/// <summary>
		/// Scroll so the provided child is in view.
		/// </summary>
		/// <param name="useEasing">Should easing be used or snap instantly.</param>
		public void ScrollToChild(RectTransform child, bool useEasing = true)
		{
			if (m_EaseCoroutine != null) {
				StopCoroutine(m_EaseCoroutine);
			}

			Vector3 scrollDelta = KeepChildInScrollViewportDelta(ScrollRect, child, Margin);

			if (scrollDelta.magnitude <= 0.001f)
				return;

			if (Easing == EasingType.SnapInstant || !useEasing) {
				ScrollRect.content.localPosition += scrollDelta;
			} else {
				Func<float, float> easeFunc = Easing switch {
					EasingType.SnapInstant => null,
					EasingType.Linear => Linear,
					EasingType.OutQuad => OutQuad,
					EasingType.OutQuart => OutQuart,
					EasingType.OutQuint => OutQuint,
					EasingType.OutExpo => OutExpo,
					_ => throw new NotImplementedException(),
				};

				m_EaseCoroutine = StartCoroutine(EaseScrollCrt(ScrollRect.content.localPosition, ScrollRect.content.localPosition + scrollDelta, easeFunc));
			}
		}

		/// <summary>
		/// Move scroll so child is in view, if it isn't already.
		/// </summary>
		public static void KeepChildInScrollViewport(ScrollRect scrollRect, RectTransform child, RectOffset margin = null)
		{
			scrollRect.content.localPosition += KeepChildInScrollViewportDelta(scrollRect, child, margin);
		}

		/// <summary>
		/// Delta position to add to the <see cref="ScrollRect.content"/> local position so the child will be in view, if it isn't already.
		/// </summary>
		public static Vector3 KeepChildInScrollViewportDelta(ScrollRect scrollRect, RectTransform child, RectOffset margin = null)
		{
			Canvas.ForceUpdateCanvases();

			// Get min and max of the viewport and child in local space to the viewport so we can compare them.
			// NOTE: use viewport instead of the scrollRect as viewport doesn't include the scrollbars in it.
			Vector2 viewPosMin = scrollRect.viewport.rect.min;
			Vector2 viewPosMax = scrollRect.viewport.rect.max;

			Vector2 childPosMin = scrollRect.viewport.InverseTransformPoint(child.TransformPoint(child.rect.min));
			Vector2 childPosMax = scrollRect.viewport.InverseTransformPoint(child.TransformPoint(child.rect.max));

			if (margin != null) {
				childPosMin -= new Vector2(margin.left, margin.bottom);
				childPosMax += new Vector2(margin.right, margin.top);
			}

			Vector2 move = Vector2.zero;

			// Check if one (or more) of the child bounding edges goes outside the viewport and
			// calculate move vector for the content rect so it can keep it visible.
			if (childPosMax.y > viewPosMax.y) {
				move.y = childPosMax.y - viewPosMax.y;
			}
			if (childPosMin.x < viewPosMin.x) {
				move.x = childPosMin.x - viewPosMin.x;
			}
			if (childPosMax.x > viewPosMax.x) {
				move.x = childPosMax.x - viewPosMax.x;
			}
			if (childPosMin.y < viewPosMin.y) {
				move.y = childPosMin.y - viewPosMin.y;
			}

			// Transform the move vector to world space, then to content local space (in case of scaling or rotation?) and apply it.
			Vector3 worldMove = scrollRect.viewport.TransformDirection(move);
			return -1 * scrollRect.content.InverseTransformDirection(worldMove);
		}


		/// <summary>
		/// Snap scroll to child, so it is at the top left most position..
		/// Copied from https://stackoverflow.com/a/30769550/4612666 with some fixes.
		/// </summary>
		public static void SnapScrollToChild(ScrollRect scrollRect, RectTransform child, Vector2 margin)
		{
			Canvas.ForceUpdateCanvases();

			Vector2 contentPos = scrollRect.transform.InverseTransformPoint(scrollRect.content.position);
			Vector2 childPosLocal = new Vector2(child.rect.xMin - margin.x, child.rect.yMax + margin.y);
			Vector2 childPos = scrollRect.transform.InverseTransformPoint(child.TransformPoint(childPosLocal));

			Vector2 endPos = contentPos - childPos;

			float horizontalMax = scrollRect.content.rect.width - scrollRect.viewport.rect.width;
			float verticalMax = scrollRect.content.rect.height - scrollRect.viewport.rect.height;

			Vector2 result = Vector2.zero;

			if (scrollRect.horizontal && horizontalMax > 0f) {
				result.x = Mathf.Clamp(endPos.x, -horizontalMax, 0f);
			}
			if (scrollRect.vertical && verticalMax > 0f) {
				result.y = Mathf.Clamp(endPos.y, 0f, verticalMax);
			}

			scrollRect.content.localPosition = result;
		}


		private IEnumerator EaseScrollCrt(Vector3 scrollStart, Vector3 scrollEnd, Func<float, float> easeFunc)
		{
			Vector3 dist = scrollEnd - scrollStart;

			float easeTime = 0;
			while (easeTime < EasingDuration) {
				yield return null;

				easeTime += Time.unscaledDeltaTime;
				ScrollRect.content.localPosition = scrollStart + dist * easeFunc(easeTime / EasingDuration);
			}

			ScrollRect.content.localPosition = scrollEnd;
			m_EaseCoroutine = null;
		}

		#region Ease Functions

		private static float Linear(float t) => t;

		private static float InQuad(float t) => t * t;
		private static float OutQuad(float t) => 1 - InQuad(1 - t);

		private static float InQuart(float t) => t * t * t * t;
		private static float OutQuart(float t) => 1 - InQuart(1 - t);

		private static float InQuint(float t) => t * t * t * t * t;
		private static float OutQuint(float t) => 1 - InQuint(1 - t);

		private static float InExpo(float t) => (float)Math.Pow(2, 10 * (t - 1));
		private static float OutExpo(float t) => 1 - InExpo(1 - t);

		#endregion
	}
}