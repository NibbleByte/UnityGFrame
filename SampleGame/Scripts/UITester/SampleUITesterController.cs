using DevLocker.GFrame.Input;
using System.Collections.Generic;
using UnityEngine;

namespace DevLocker.GFrame.SampleGame.UITester
{
	/// <summary>
	/// Controller for the UITestScene used for testing out the UI + Input features of the GFrame.
	/// </summary>
	public class SampleUITesterController : MonoBehaviour
	{
		public void LoadMainMenu()
		{
			Game.SampleLevelsManager.Instance.SwitchLevelAsync(new MainMenu.SampleMainMenuLevelSupervisor());
		}
	}

}