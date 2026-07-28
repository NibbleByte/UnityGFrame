using UnityEngine;

namespace DevLocker.GFrame.SampleGame.MainMenu
{
	public class SampleMainMenuNewGamePanel : MonoBehaviour
	{
		public void StartNewGame()
		{
			Game.SampleLevelsManager.Instance.SwitchLevelAsync(new Play.SamplePlaySupervisor());
		}

		public void StartMultiplayerNewGame()
		{
			Game.SampleLevelsManager.Instance.SwitchLevelAsync(new Play.SampleMultiPlaySupervisor());
		}
	}

}