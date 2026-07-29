using UnityEngine;
using UnityEngine.InputSystem;

namespace DevLocker.WiseInput.Sample
{
	/// <summary>
	/// Controller used to demonstrate how to save and load bindings.
	/// </summary>
	public class SampleUITesterBindingsController : MonoBehaviour
	{
		public void SaveBindings()
		{
			var rebinds = SampleSceneController.Instance.PlayerInput.actions.SaveBindingOverridesAsJson();
			PlayerPrefs.SetString("sample-game-rebinds", rebinds);

			Debug.Log($"Rebinds saved:\n{rebinds}");
		}

		public void LoadBindings()
		{
			var rebinds = PlayerPrefs.GetString("sample-game-rebinds");
			if (!string.IsNullOrEmpty(rebinds)) {
				SampleSceneController.Instance.PlayerInput.actions.LoadBindingOverridesFromJson(rebinds);
				SampleSceneController.Instance.InputContext.TriggerLastUsedDeviceChanged();
			}

			Debug.Log($"Rebinds loaded:\n{rebinds}");
		}
	}

}