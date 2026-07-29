using DevLocker.WiseInput;
using DevLocker.WiseInput.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DevLocker.GFrame
{
	/// <summary>
	/// Represents the state of the current player. Each player has its own <see cref="PlayerStateStack"/> and <see cref="IInputContext"/>.
	/// The stack passes the <see cref="PlayerStatesContext"/> to the states and to <see cref="ILevelLoadedListener"/> events,
	/// which can be used to access the player context and other references.
	/// </summary>
	public class PlayerContext
	{
		/// <summary>
		/// GameObject roots associated with this player. Useful for split-screen games.
		/// </summary>
		public readonly List<GameObject> AssociatedRoots = new();

		public InputUIRootObject InputUIRoot { get; private set; }

		/// <summary>
		/// The input context for this player.
		/// Includes the InputStack that should be used everywhere.
		/// </summary>
		public IInputContext InputContext => InputUIRoot?.InputContext;

		/// <summary>
		/// Stack of player states. States can be pushed in / replaced / popped out of the stack.
		/// </summary>
		public PlayerStateStack StatesStack { get; private set; }

		/// <summary>
		/// Event system used by this player.
		/// </summary>
		public EventSystem EventSystem => InputUIRoot?.EventSystem;

		/// <summary>
		/// Short-cut - get selected UI object for this player.
		/// </summary>
		public GameObject SelectedGameObject => InputUIRoot?.EventSystem?.currentSelectedGameObject;

		/// <summary>
		/// Short-cut - set selected UI object for this player.
		/// </summary>
		public void SetSelectedGameObject(GameObject selected) => InputUIRoot?.EventSystem?.SetSelectedGameObject(selected);

		public event Action StatesStackCreated;
		public event Action StatesStackDestroyed;

		/// <summary>
		/// Create the <see cref="PlayerStateStack"/> for this player. The passed on references will be used as context.
		/// </summary>
		public virtual void CreatePlayerStack(params object[] references)
		{
			StatesStack = new PlayerStateStack(references);
			StatesStack.Context.AddReference(this);

			InputUIRoot = references.OfType<InputUIRootObject>().FirstOrDefault();
			AssociatedRoots.AddRange(references.OfType<IInputUIRoot>().OfType<MonoBehaviour>().Select(b => b.gameObject));

			StatesStackCreated?.Invoke();
		}

		/// <summary>
		/// Dispose of the states stack. Do this when switching levels, especially with the <see cref="GlobalRoot"/>
		/// </summary>
		public virtual void DisposePlayerStack()
		{
			if (!StatesStack.IsEmpty) {
				StatesStack.ClearStackAndState();
			}

			StatesStackDestroyed?.Invoke();

			StatesStack = null;
		}

		public virtual void SetInputUIRootObject(InputUIRootObject inputUIRoot)
		{
			InputUIRoot = inputUIRoot;
		}
	}
}