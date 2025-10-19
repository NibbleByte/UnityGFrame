namespace DevLocker.GFrame.MessageBox.UIControllers
{
	/// <summary>
	/// Displays an in-game prompt with message for the user to answer.
	/// </summary>
	public class MessageBoxSimpleUIController : MessageBoxUIControllerBase
	{
		public TMPro.TMP_Text Title;
		public TMPro.TMP_Text Subtitle;
		public TMPro.TMP_Text TextContent;

		public override void Show(MessageData data)
		{
			if (Title) Title.text = data.Title;
			if (Subtitle) Subtitle.text = data.Subtitle;
			if (TextContent) TextContent.text = data.Content;

			base.Show(data);
		}
	}
}