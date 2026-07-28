using DevLocker.GFrame.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DevLocker.GFrame
{
	/// <summary>
	/// Contains the currently active level supervisor and can switch it to another.
	/// </summary>
	public class LevelsManager : MonoBehaviour
	{
		// HINT: If you inherit this class, make your own Instance property with your own type and use the "new" keyword to hide this one.
		public static LevelsManager Instance { get; protected set; }

		/// <summary>
		/// Assign this if you want to show / hide loading screen between your levels (e.g. fade out effects).
		/// You may assign this multiple times based on your needs and next level to load.
		/// If null, it will be skipped.
		/// </summary>
		public ILevelLoadingScreen LevelLoadingScreen;

		[Tooltip("Should level loading screen show before exiting the level states or after.")]
		public bool ShowLoadingScreenBeforeLevelStates = false;

		/// <summary>
		/// Current level supervisor.
		/// </summary>
		public ILevelSupervisor LevelSupervisor { get; private set; }

		/// <summary>
		/// When changing levels, this property provides the next level supervisor, during the process.
		/// </summary>
		public ILevelSupervisor NextLevelSupervisor { get; private set; }

		/// <summary>
		/// Is level currently changing. Can't start another change while this is true.
		/// Initially set to true as there is no level loaded and it is expected to load.
		/// </summary>
		public bool IsChangingLevel { get; private set; } = true;

		// Listen for supervisor change.
		// NOTE: avoid using events with more complex logic as it will blow up in your face.
		//		 If you really need to do it, you can inherit this LevelsManager and override the corresponding protected methods.
		public event Action UnloadingSupervisor;
		public event Action UnloadedSupervisor;
		public event Action LoadingSupervisor;
		public event Action LoadedSupervisor;

		/// <summary>
		/// Will be called after level was loaded and loading screen transition finished.
		/// Will be called once, then all subscribers be removed - no need to unsubscribe.
		/// </summary>
		public event Action LevelLoadedAndShownCallOnce;

		protected virtual void Awake()
		{
			if (Instance) {
				GameObject.DestroyImmediate(this);
				return;
			}

			Instance = this;

			if (transform.parent == null) {
				DontDestroyOnLoad(gameObject);
			}
		}

		protected virtual void OnDestroy()
		{
			if (Instance == this) {
				Instance = null;
			}
		}

		protected virtual void Update()
		{
			if (LevelSupervisor == null)
				return;

			if (LevelSupervisor is IUpdateListener updateSupervisor) {
				updateSupervisor.Update();
			}

			foreach(PlayerContext playerContext in LevelSupervisor.PlayerContexts) {

				if (playerContext.StatesStack?.CurrentState is IUpdateListener updateState && !playerContext.StatesStack.ChangingStates) {
					updateState.Update();
				}
			}
		}

		protected virtual void FixedUpdate()
		{
			if (LevelSupervisor == null)
				return;

			if (LevelSupervisor is IFixedUpdateListener updateSupervisor) {
				updateSupervisor.FixedUpdate();
			}
			foreach (PlayerContext playerContext in LevelSupervisor.PlayerContexts) {

				if (playerContext.StatesStack?.CurrentState is IFixedUpdateListener updateState && !playerContext.StatesStack.ChangingStates) {
					updateState.FixedUpdate();
				}
			}
		}

		protected virtual void LateUpdate()
		{
			if (LevelSupervisor == null)
				return;

			if (LevelSupervisor is ILateUpdateListener updateSupervisor) {
				updateSupervisor.LateUpdate();
			}

			foreach (PlayerContext playerContext in LevelSupervisor.PlayerContexts) {

				if (playerContext.StatesStack?.CurrentState is ILateUpdateListener updateState && !playerContext.StatesStack.ChangingStates) {
					updateState.LateUpdate();
				}
			}

		}


		/// <summary>
		/// Returns <see cref="PlayerContext"/> for the provided <see cref="GameObject"/>.
		/// If there is only one player it always returns that one no matter the object (simplification for single player games).
		/// For games with more than one player make sure the <see cref="PlayerContext.AssociatedRoots"/> are already in place.
		/// </summary>
		public PlayerContext GetPlayerContextFor(GameObject gameObject)
		{
			if (gameObject == null)
				throw new ArgumentNullException(nameof(gameObject));

			if (LevelSupervisor == null) {
				Debug.LogError($"[GFrame] Can't get player context for {gameObject} as there is no level supervisor set.", gameObject);
				return null;
			}

			if (LevelSupervisor.PlayerContexts.Count == 0) {
				Debug.LogError($"[GFrame] Can't get player context for {gameObject} as there are no player contexts in the level supervisor {LevelSupervisor}.", gameObject);
				return null;
			}

			if (LevelSupervisor.PlayerContexts.Count == 1)
				return LevelSupervisor.PlayerContexts.First();

			return LevelSupervisor.PlayerContexts.FirstOrDefault(pc => pc.AssociatedRoots.Any(root => root != null && gameObject.transform.IsChildOf(root.transform)));
		}

		#region Primary Player State

		/// <summary>
		/// Push state to the top of the state stack for the primary player. Can pop it out to the previous state later on.
		/// </summary>
		public void PushStateForPrimaryPlayer(IPlayerState state)
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.PushState(state);
		}

		/// <summary>
		/// Clears the state stack of any other states and pushes the provided one for the primary player.
		/// </summary>
		public void SetStateForPrimaryPlayer(IPlayerState state)
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.SetState(state);
		}

		/// <summary>
		/// Pop a single state from the state stack for the primary player.
		/// </summary>
		public void PopStateForPrimaryPlayer()
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.PopState();
		}

		/// <summary>
		/// Pops multiple states from the state stack for the primary player.
		/// </summary>
		public void PopStatesForPrimaryPlayer(int count)
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.PopStates(count);
		}

		/// <summary>
		/// Pop and push back the state at the top. Will trigger changing state events for the primary player.
		/// </summary>
		public void ReenterCurrentStateForPrimaryPlayer()
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.ReenterCurrentState();
		}

		/// <summary>
		/// Change the current state and add it to the state stack.
		/// Will notify the state itself.
		/// Any additional state changes that happened in the meantime will be queued and executed after the current change finishes for the primary player.
		/// </summary>
		public void ChangeStateForPrimaryPlayer(IPlayerState state, StackAction stackAction)
		{
			LevelSupervisor.PlayerContexts.First().StatesStack.ChangeState(state, stackAction);
		}

		#endregion


		/// <summary>
		/// Switch to another level supervisor. Will unload the current one and load the next one.
		/// Your <see cref="ILevelSupervisor"/> implementation should handle the loading and unloading of the level itself,
		/// setup player contexts and their state stacks, call <see cref="ILevelLoadedListener"/> events on scene objects, etc.
		/// </summary>
		public async void SwitchLevelAsync(ILevelSupervisor nextLevel)
		{
			if (IsChangingLevel && LevelSupervisor != null) {
				throw new InvalidOperationException($"Level is already changing. Can't switch to {nextLevel} while change is in progress.");
			}

			IsChangingLevel = true;
			NextLevelSupervisor = nextLevel;
			ILevelSupervisor prevLevel = LevelSupervisor;


			try {

				if (LevelSupervisor != null) {

					if (ShowLoadingScreenBeforeLevelStates && LevelLoadingScreen != null) {
						await LevelLoadingScreen.ShowAsync();
					}

					await UnloadingSupervisorAsync();

					foreach (PlayerContext playerContext in LevelSupervisor.PlayerContexts) {

						if (playerContext.StatesStack != null) {
							playerContext.DisposePlayerStack();
						}
					}


					if (!ShowLoadingScreenBeforeLevelStates && LevelLoadingScreen != null) {
						await LevelLoadingScreen.ShowAsync();
					}

					await LevelSupervisor.UnloadAsync();

					await UnloadedSupervisorAsync();

				} else if (LevelLoadingScreen != null) {
					LevelLoadingScreen.HideInstantly();
				}

				LevelSupervisor = nextLevel;

				await LoadingSupervisorAsync();

				await nextLevel.LoadAsync();

				// Avoid first show of loading screen when the game starts.
				if (prevLevel != null && LevelLoadingScreen != null) {

					// Wait 1 frame for performance to stabilize (or transition animations will be skipped).
					await Task.Yield();

					await LevelLoadingScreen.HideAsync();
				}

				await LoadedSupervisorAsync();

				LevelLoadedAndShownCallOnce?.Invoke();
				LevelLoadedAndShownCallOnce = null;

				IsChangingLevel = false;
				NextLevelSupervisor = null;
			}
			catch (Exception ex) {
				IsChangingLevel = false;
				NextLevelSupervisor = null;

				if (!OnException(prevLevel, nextLevel, ex)) {
					throw;
				}
			}
		}

		/// <summary>
		/// Override this according to your needs.
		/// </summary>
		protected virtual Task UnloadingSupervisorAsync()
		{
			Debug.Log($"[GFrame] Unloading level supervisor {LevelSupervisor}");
			UnloadingSupervisor?.Invoke();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Override this according to your needs.
		/// </summary>
		protected virtual Task UnloadedSupervisorAsync()
		{
			UnloadedSupervisor?.Invoke();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Override this according to your needs.
		/// </summary>
		protected virtual Task LoadingSupervisorAsync()
		{
			Debug.Log($"[GFrame] Loading level supervisor {LevelSupervisor}");
			LoadingSupervisor?.Invoke();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Override this according to your needs.
		/// </summary>
		protected virtual Task LoadedSupervisorAsync()
		{
			LoadedSupervisor?.Invoke();

			return Task.CompletedTask;
		}

		/// <summary>
		/// Chance to handle exceptions on level switching. Return true if handled.
		/// Example: switch to another fall-back level.
		/// </summary>
		protected virtual bool OnException(ILevelSupervisor prevLevel, ILevelSupervisor nextLevel, Exception exception)
		{
			return false;
		}

	}
}