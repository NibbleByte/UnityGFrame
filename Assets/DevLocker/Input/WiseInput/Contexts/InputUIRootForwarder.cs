using UnityEngine;
using UnityEngine.EventSystems;

namespace DevLocker.WiseInput.Contexts
{
	/// <summary>
	/// Use this to forward some UI objects input toward specific <see cref="InputUIRootObject"/>.
	/// You can set the <see cref="m_PlayerRootObject"/> edit time or <see cref="PlayerRootObject"/> runtime via script.
	/// </summary>
	public class InputUIRootForwarder : MonoBehaviour, IInputUIRoot
	{
		// Used for edit time setup only.
		[SerializeField] private InputUIRootObject m_PlayerRootObject;

		public IInputUIRoot PlayerRootObject { get; private set; }

		public bool IsActive => PlayerRootObject?.IsActive ?? false;

		public IInputContext InputContext => PlayerRootObject?.InputContext;

		public EventSystem EventSystem => PlayerRootObject?.EventSystem;

		public GameObject SelectedGameObject => PlayerRootObject?.SelectedGameObject;

		public void SetSelectedGameObject(GameObject selected) => PlayerRootObject?.SetSelectedGameObject(selected);

		public InputUIRootObject GetRootObject() => PlayerRootObject?.GetRootObject();

		private IInputUIRoot.SetupCallbackDelegate m_CallbacksOnSetup;

		public void AddSetupCallback(IInputUIRoot.SetupCallbackDelegate setupReadyCallback)
		{
			if (PlayerRootObject != null) {
				PlayerRootObject.AddSetupCallback(setupReadyCallback);
			} else {
				m_CallbacksOnSetup += setupReadyCallback;
			}
		}

		/// <summary>
		/// Use this to setup the actual <see cref="InputUIRootObject"/> this instance forwards to.
		/// </summary>
		public void SetupTargetRootObject(InputUIRootObject rootObject)
		{
			PlayerRootObject = rootObject;

			PlayerRootObject.AddSetupCallback(m_CallbacksOnSetup);
			m_CallbacksOnSetup = null;
		}

		void Awake()
		{
			if (PlayerRootObject == null && m_PlayerRootObject) {
				SetupTargetRootObject(m_PlayerRootObject);
			}
		}

		void OnDestroy()
		{
			PlayerRootObject = null;
		}

	}
}