using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevLocker.GFrame
{
	/// <summary>
	/// Implement this if you want to show / hide loading screen between your levels (e.g. fade out effects).
	/// Set it to the <see cref="LevelsManager"> to be used.
	/// </summary>
	public interface ILevelLoadingScreen
	{
		Task ShowAsync();
		Task HideAsync();

		void ShowInstantly();
		void HideInstantly();

		bool HasShowFinished { get; }
		bool HasHideFinished { get; }
	}

	/// <summary>
	/// Controls the whole level: loading, unloading, switching states (via the StatesStack),
	/// even updates if <see cref="IUpdateListener"/>, <see cref="IFixedUpdateListener"/> or <see cref="ILateUpdateListener"/> are implemented."/>
	///
	/// Implementation should create and manage the <see cref="PlayerContext"/>s for all players in the level.
	///
	/// It should also call <see cref="ILevelLoadedListener"/> events on scene objects.
	/// </summary>
	public interface ILevelSupervisor
	{
		public IReadOnlyCollection<PlayerContext> PlayerContexts { get; }

		Task LoadAsync();

		Task UnloadAsync();
	}

	/// <summary>
	/// Use this interface in your supervisors to notify your scene behaviours and controllers that the level is currently loading.
	/// The supervisor should wait on the <see cref="OnLevelLoadingAsync(PlayerStatesContext)"/>, as the behaviours can loading on their own.
	/// This interface is optional and you can make another one that suits your needs.
	/// </summary>
	public interface ILevelLoadingListener
	{
		Task OnLevelLoadingAsync(PlayerStatesContext context);
	}

	/// <summary>
	/// Use this interface in your supervisors to notify your scene behaviours and controllers that the level has finished loading or will be unloading.
	/// This interface is optional and you can make another one that suits your needs.
	/// </summary>
	public interface ILevelLoadedListener
	{
		void OnLevelLoaded(PlayerStatesContext context);
		void OnLevelUnloading();
	}

	/// <summary>
	/// Your level supervisor or level state can implement this to get invoked on Unity update.
	/// </summary>
	public interface IUpdateListener
	{
		void Update();
	}

	/// <summary>
	/// Your level supervisor or level state can implement this to get invoked on Unity fixed update.
	/// </summary>
	public interface IFixedUpdateListener
	{
		void FixedUpdate();
	}

	/// <summary>
	/// Your level supervisor or level state can implement this to get invoked on Unity late update.
	/// </summary>
	public interface ILateUpdateListener
	{
		void LateUpdate();
	}
}