using DevLocker.GFrame.Input;
using System.Linq;
using UnityEngine;

namespace DevLocker.GFrame
{
	/// <summary>
	/// Helper functions for level objects lifecycle.
	/// </summary>
	public static class LevelUtils
	{
		/// <summary>
		/// Invoke <see cref="ILevelLoadedListener.OnLevelLoaded(PlayerStatesContext)"/> for all components in the target and its children.
		/// Call this from your <see cref="ILevelSupervisor.LoadAsync"/> implementation after the level is loaded on all relevant scene objects or
		/// when you spawn dynamically created objects that need to be notified. Keeps the objects lifecycle consistent.
		/// </summary>
		/// <param name="playerContext">Relevant player context. If null, context is obtained from the object. If only 1 player is present, that context is always used.</param>
		public static void InvokeLoadedEvent(GameObject target, PlayerContext playerContext = null)
		{
			if (LevelsManager.Instance == null || LevelsManager.Instance.LevelSupervisor == null)
				return;

			if (LevelsManager.Instance.LevelSupervisor.PlayerContexts.Count == 1) {
				playerContext = LevelsManager.Instance.LevelSupervisor.PlayerContexts.FirstOrDefault();
			}

			playerContext ??= LevelsManager.Instance.GetPlayerContextFor(target);

			foreach (var levelListener in target.GetComponentsInChildren<ILevelLoadedListener>(includeInactive: true)) {
				levelListener.OnLevelLoaded(playerContext.StatesStack.Context);
			}
		}

		/// <summary>
		/// Invoke <see cref="ILevelLoadedListener.OnLevelUnloading"/> for all components in the target and its children.
		/// Call this from your <see cref="ILevelSupervisor.UnloadAsync"/> implementation before the level is unloaded on all relevant scene objects or
		/// when you destroy dynamically created objects that need to be notified. Keeps the objects lifecycle consistent.
		/// </summary>
		public static void InvokeUnloadedEvent(GameObject target)
		{
			foreach (var levelListener in target.GetComponentsInChildren<ILevelLoadedListener>(includeInactive: true)) {
				levelListener.OnLevelUnloading();
			}
		}

		/// <summary>
		/// Instantiate your object with this method to have <see cref="ILevelLoadedListener.OnLevelLoaded(PlayerStatesContext)"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		/// <param name="playerContext">Relevant player context. If null, context is obtained from the object. If only 1 player is present, that context is always used.</param>
		public static GameObject Instantiate(GameObject target, PlayerContext playerContext = null) => Instantiate(target, parent: null, worldPositionStays: false, playerContext);

		/// <summary>
		/// Instantiate your object with this method to have <see cref="ILevelLoadedListener.OnLevelLoaded(PlayerStatesContext)"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		/// <param name="playerContext">Relevant player context. If null, context is obtained from the object. If only 1 player is present, that context is always used.</param>
		public static GameObject Instantiate(GameObject target, Transform parent = null, bool worldPositionStays = false, PlayerContext playerContext = null)
		{
			GameObject go = parent ? GameObject.Instantiate(target, parent, worldPositionStays) : GameObject.Instantiate(target);

			InvokeLoadedEvent(go, playerContext);

			return go;
		}

		/// <summary>
		/// Instantiate your object with this method to have <see cref="ILevelLoadedListener.OnLevelLoaded(PlayerStatesContext)"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		/// <param name="playerContext">Relevant player context. If null, context is obtained from the object. If only 1 player is present, that context is always used.</param>
		public static T Instantiate<T>(T target, PlayerContext playerContext = null) where T : Component => Instantiate(target, parent: null, worldPositionStays: false, playerContext);

		/// <summary>
		/// Instantiate your object with this method to have <see cref="ILevelLoadedListener.OnLevelLoaded(PlayerStatesContext)"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		/// <param name="playerContext">Relevant player context. If null, context is obtained from the object. If only 1 player is present, that context is always used.</param>
		public static T Instantiate<T>(T target, Transform parent = null, bool worldPositionStays = false, PlayerContext playerContext = null) where T : Component
		{
			T go = parent ? GameObject.Instantiate(target, parent, worldPositionStays) : GameObject.Instantiate(target);

			InvokeLoadedEvent(go.gameObject, playerContext);

			return go;
		}

		/// <summary>
		/// Destroy your object with this method to have <see cref="ILevelLoadedListener.OnLevelUnloading"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		public static void Destroy(GameObject target)
		{
			InvokeUnloadedEvent(target);
			GameObject.Destroy(target);
		}

		/// <summary>
		/// Destroy your object with this method to have <see cref="ILevelLoadedListener.OnLevelUnloading"/> called automatically for all components in the target and its children.
		/// Keeps the objects lifecycle consistent.
		/// </summary>
		public static void Destroy<T>(T target) where T : Component, ILevelLoadedListener
		{
			target.OnLevelUnloading();
			GameObject.Destroy(target);
		}
	}
}