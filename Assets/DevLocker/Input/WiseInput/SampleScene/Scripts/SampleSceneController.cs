using DevLocker.WiseInput.Contexts;
using DevLocker.WiseInput.UIInputDisplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DevLocker.WiseInput.Sample
{
	/// <summary>
	/// Sample controler to setup the input.
	/// </summary>
	public class SampleSceneController : MonoBehaviour
	{
		public static SampleSceneController Instance { get; private set; }

		public GameObject GameInputPrefab;
		public InputBindingDisplayAsset[] BindingDisplayAssets;

		public SamplePlayerControls PlayerControls { get; private set; }

		public PlayerInput PlayerInput { get; private set; }

		public IInputContext InputContext { get; private set; }

		void Awake()
		{
			if (Instance) {
				GameObject.DestroyImmediate(gameObject);
				return;
			}

			Instance = this;

			PlayerControls = new SamplePlayerControls();

			var gameInputObject = Instantiate(GameInputPrefab, transform);

			gameInputObject.name = gameInputObject.name.Replace("(Clone)", "-Global");
			var playerInput = gameInputObject.GetComponentInChildren<PlayerInput>();

			// HACK: trick the PlayerInput to use the reference to our asset instead of copying the actions. Check the InputComponentContext() constructor for more info.
			// NOTE: the PlayerInput must initially have empty reference set for InputActionAsset in the prefab.
			playerInput.enabled = false;
			playerInput.actions = PlayerControls.asset;
			playerInput.enabled = true;

			var uiInputModule = gameInputObject.GetComponentInChildren<InputSystemUIInputModule>();
			uiInputModule.actionsAsset = PlayerControls.asset;  // This will refresh the UI Input action references to the new asset.

			playerInput.uiInputModule = uiInputModule;

			var inputContext = new InputComponentContext(playerInput, new InputActionsMaskedStack(PlayerControls), IInputContext.InputBehaviours.Default, BindingDisplayAssets);
			PlayerControls.SetInputContext(inputContext);

			InputUIRootObject.GlobalUIRoot.SetupGlobal(uiInputModule.GetComponent<EventSystem>(), inputContext);

			// The whole level is UI, so enable it for the whole level.
			PlayerControls.Enable(this, PlayerControls.Sample_UI);
		}

		void OnDestroy()
		{
			PlayerControls.DisableAll(this);

			if (Instance == this) {
				Instance = null;
			}
		}
	}

}