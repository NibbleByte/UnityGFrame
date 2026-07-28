using DevLocker.GFrame.Input;
using DevLocker.GFrame.Input.Contexts;
using UnityEngine;

namespace DevLocker.GFrame.UIUtils
{
	/// <summary>
	/// Will deactivate <see cref="CanvasGroup.blocksRaycasts"/> if <see cref="InputUIRootObject.IsLevelLoading"/> is set to true.
	/// </summary>
	[RequireComponent(typeof(CanvasGroup))]
	public class UIDisableCanvasGroupOnLevelLoading : MonoBehaviour
	{
		private CanvasGroup m_CanvasGroup;

		private bool m_LastIsLevelLoading;

		// Use Start() to make sure the player context association is already in place.
		void Start()
		{
			m_CanvasGroup = GetComponent<CanvasGroup>();
			m_CanvasGroup.blocksRaycasts = false;
			m_LastIsLevelLoading = true;
		}

		void Update()
		{
			if (LevelsManager.Instance == null || m_CanvasGroup == null)
				return;

			bool isLevelLoading = LevelsManager.Instance.IsChangingLevel;

			// Set the canvas group only if level loading flag changed. Others may also set the canvas during states change etc.
			if (m_LastIsLevelLoading == isLevelLoading)
				return;

			m_LastIsLevelLoading = isLevelLoading;

			m_CanvasGroup.blocksRaycasts = !isLevelLoading;
		}

		void OnDestroy()
		{
			if (m_CanvasGroup) {
				m_CanvasGroup.blocksRaycasts = true;
			}
		}
	}
}