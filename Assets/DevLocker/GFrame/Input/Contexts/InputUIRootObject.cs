using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DevLocker.GFrame.Input.Contexts
{
	/// <summary>
	/// When using multiple event systems (e.g. split-screen) add this component to the root UI canvas object of each player.
	/// This marks all children as owned by the specific player.
	/// At some point set the owning event system via <see cref="SetupContext"/>.
	/// Other child components may use this information to work properly in such environment.
	///
	/// Make sure the component exists on the Awake() step of the child objects, i.e. have it defined in the UI prefab. If that is not possible:
	///		- Use <see cref="InputUIRootForwarder"/>.
	///		- Make a prefab variant with added component and instantiate that one instead.
	///		- Have the prefab saved as inactive. Add the component after instantiation, then activate the prefab.
	///		- If not working with prefabs - add the component to the scene UI root game object, instantiate it and destroy the original.
	///
	/// Additionally, there is a <see cref="GlobalUIRoot"/> fall-back instance automatically created for any objects that do not belong to any player.
	/// For this reason, for single player game don't attach any components. Just call <see cref="SetupGlobal"/>.
	///
	/// Note: this is how Unity UI can support multiple players each having their own UI selection and navigation.
	///		  Basically they are limited by the <see cref="UnityEngine.InputSystem.UI.MultiplayerEventSystem.playerRoot"/>
	///		  Good explanation: https://www.youtube.com/watch?v=Ur2tBl58YOc
	///		  Also: https://opsive.com/support/documentation/ultimate-inventory-system/input/split-screen-co-op-ui/
	/// </summary>
	public class InputUIRootObject : MonoBehaviour, IInputUIRoot
	{
		/// <summary>
		/// Global root object used by any components not owned by any player.
		/// You may want to set this as DontDestroyOnLoad() or parent it under your event system.
		/// </summary>
		public static InputUIRootObject GlobalUIRoot {
			get {
				if (!Application.isPlaying) {
					Debug.LogError($"[Input] Trying to get {nameof(GlobalUIRoot)} while game is not playing. This is not allowed.");
					return null;
				}

				if (m_GlobalUIRoot == null) {
					m_GlobalUIRoot = new GameObject(nameof(GlobalUIRoot)).AddComponent<InputUIRootObject>();

					// Awake() will add it to the list.
					m_PlayerUIRoots.Remove(m_GlobalUIRoot);

					// Make it don't destroy on load until setup is done,
					// or it will be destroyed on switching scenes in the mean time, causing all component subscribers to skip initialization.
					DontDestroyOnLoad(m_GlobalUIRoot);
				}

				return m_GlobalUIRoot;
			}
		}
		private static InputUIRootObject m_GlobalUIRoot;

		/// <summary>
		/// Root objects for all the players, excluding the Global one.
		/// </summary>
		public static IReadOnlyCollection<InputUIRootObject> PlayerUIRoots => m_PlayerUIRoots;
		private static List<InputUIRootObject> m_PlayerUIRoots = new List<InputUIRootObject>();

		/// <summary>
		/// All player root objects, including the Global one.
		/// </summary>
		public static IEnumerable<InputUIRootObject> AllUIRoots {
			get {
				yield return GlobalUIRoot;

				foreach(InputUIRootObject rootObject in m_PlayerUIRoots) {
					yield return rootObject;
				}
			}
		}


		// Called when setup happens or immediately if setup was already done.
		private IInputUIRoot.SetupCallbackDelegate m_CallbacksOnSetup;

		/// <summary>
		/// Is the player setup ready.
		/// </summary>
		public bool IsActive => EventSystem != null;

		/// <summary>
		/// The input context for this player. The heart of this framework.
		/// Includes the InputStack that should be used everywhere.
		/// </summary>
		public IInputContext InputContext { get; private set; }

		/// <summary>
		/// Event system used by this player.
		/// </summary>
		public EventSystem EventSystem { get; private set; }

		/// <summary>
		/// Short-cut - get selected UI object for this player.
		/// </summary>
		public GameObject SelectedGameObject => EventSystem?.currentSelectedGameObject;

		/// <summary>
		/// Short-cut - set selected UI object for this player.
		/// </summary>
		public void SetSelectedGameObject(GameObject selected) => EventSystem?.SetSelectedGameObject(selected);

		/// <summary>
		/// Get the top-most root object.
		/// </summary>
		public InputUIRootObject GetRootObject() => this;

		/// <summary>
		/// Called when assembly reload is disabled.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ClearStaticsCache()
		{
			m_GlobalUIRoot = null;
			m_PlayerUIRoots.Clear();
		}

		protected virtual void Awake()
		{
			if (!m_PlayerUIRoots.Contains(this)) {
				m_PlayerUIRoots.Add(this);
			}
		}

		protected virtual void OnDestroy()
		{
			InputContext?.Dispose();
			m_PlayerUIRoots.Remove(this);
			EventSystem = null;
		}

		/// <summary>
		/// Handler is called when setup (<see cref="SetupGlobal"/> or <see cref="SetupContext"/>) happens or immediately if setup was already done.
		/// Use this to delay your initialization if you need the InputContext on Awake() or OnEnable(), but it is not yet available.
		/// Once called after setup, the callback is lost - won't be called again so no need to unsubscribe.
		/// </summary>
		public void AddSetupCallback(IInputUIRoot.SetupCallbackDelegate setupReadyCallback)
		{
			if (IsActive) {
				setupReadyCallback(false);
			} else {
				m_CallbacksOnSetup += setupReadyCallback;
			}
		}

		/// <summary>
		/// Set the owning event system for the <see cref="GlobalUIRoot"/>.
		/// Will attach the player root to the EventSystem.
		/// </summary>
		public void SetupGlobal(EventSystem eventSystem, IInputContext inputContext)
		{
			if (this != m_GlobalUIRoot) {
				throw new InvalidOperationException($"Trying to setup {name} as global player root object. Please use \"{nameof(SetupContext)}()\"");
			}

			InputContext = inputContext;
			EventSystem = eventSystem;

			transform.SetParent(eventSystem.transform);

			m_CallbacksOnSetup?.Invoke(true);
			m_CallbacksOnSetup = null;
		}

		/// <summary>
		/// Set the owning event system & player index.
		/// If event system is <see cref="UnityEngine.InputSystem.UI.MultiplayerEventSystem"/> will set it's playerRoot.
		/// </summary>
		public void SetupContext(EventSystem eventSystem, IInputContext inputContext)
		{
			if (this == m_GlobalUIRoot) {
				throw new InvalidOperationException($"Trying to setup {name} as non-global player root object. Please use \"{nameof(SetupGlobal)}()\"");
			}

			if (eventSystem is UnityEngine.InputSystem.UI.MultiplayerEventSystem multiplayerEventSystem) {
				multiplayerEventSystem.playerRoot = gameObject;
			}

			InputContext = inputContext;
			EventSystem = eventSystem;

			m_CallbacksOnSetup?.Invoke(true);
			m_CallbacksOnSetup = null;
		}
	}
}