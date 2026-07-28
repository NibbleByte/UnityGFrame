using UnityEngine;

namespace DevLocker.GFrame.SampleGame.Game
{
	/// <summary>
	/// LevelsManager is the center of your game.
	/// They take care of the levels switching.
	/// They are also good candidate for singleton entry point of your game - add the game context on it.
	/// </summary>
	public class SampleLevelsManager : LevelsManager
	{
		public static new SampleLevelsManager Instance => (SampleLevelsManager) LevelsManager.Instance;

		public SampleGameContext GameContext { get; private set; }

		public void SetGameContext(SampleGameContext gameContext)
		{
			GameContext = gameContext;
		}

#if UNITY_EDITOR
		internal static string GetEditorSampleScenePath(string sampleSceneNameWithExtension)
		{
			string path = "Assets/DevLocker/GFrame/SampleGame/Scenes/" + sampleSceneNameWithExtension;
			if (UnityEditor.AssetDatabase.AssetPathExists(path))
				return path;

			if (UnityEditor.AssetDatabase.AssetPathExists(path))
				return path;

			return "Packages/devlocker.gframe/SampleGame/Scenes/" + sampleSceneNameWithExtension;
		}
#endif
	}
}