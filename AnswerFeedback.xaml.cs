using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;

namespace QzLangProg;

public partial class AnswerFeedback
{
	public AnswerFeedback()
	{
		InitializeComponent();
		WeakReferenceMessenger.Default.Register<OpenAnswerFeedback>(this, RenderAnswerFeedback);
	}

	public async void RenderAnswerFeedback(object sender, OpenAnswerFeedback message)
	{
		switch (message.Value.FeedbackType)
		{
			case 0:
				FeedbackReaction.SetAppTheme(BackgroundColorProperty,
					TaskPage.GetColorFromResource("CorrectLight"),
					TaskPage.GetColorFromResource("CorrectDark"));
				FeedbackText.Text = "Правильно!";
				break;
			case 1:
				FeedbackReaction.SetAppTheme(BackgroundColorProperty, 
					TaskPage.GetColorFromResource("IncorrectLight"), 
					TaskPage.GetColorFromResource("IncorrectDark"));
				FeedbackText.Text = $"К сожалению! Правильный ответ: {message.Value.correctAnswer}";
				break;
			default:
				FeedbackText.Text = "Error: AnswerFeedback received an unknown feedback type";
				break;
		}
	}

	private void OnNextTask(object sender, EventArgs e)
	{
		//idk how to send a message with no argument, so let it just send 'true' value
		WeakReferenceMessenger.Default.Send(new StartRenderNextTask(true));
		DismissAsync();
	}
}