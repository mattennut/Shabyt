using CommunityToolkit.Mvvm.Messaging;

namespace QzLangProg;

public partial class ResultPage : ContentPage
{
	public ResultPage()
	{
		WeakReferenceMessenger.Default.Register<RedirectToResultsPage>(this, RenderResults);
		InitializeComponent();

		(float SuccessRate, int Mistakes, int Streak, int XP, int Asyqs) = FileReader.LessonResults;

		RenderResults(XPLabel, new RedirectToResultsPage(
			new LessonResults(SuccessRate, Mistakes, Streak, XP, Asyqs)));
	}

	protected override bool OnBackButtonPressed()
	{
		return true;
	}

	public void RenderResults(object recipient, RedirectToResultsPage message)
	{
		SuccessRateLabel.Text	= $"{Math.Round(message.Value.SuccessRate * 100, 1)}%";
		MistakesLabel.Text		= $"{message.Value.Mistakes}";
		StreakLabel.Text		= $"{message.Value.Streak}";

		XPLabel.Text = $"+{message.Value.XP}";
		AsyqsLabel.Text = $"+{message.Value.Asyq}";
	}

	private async void ToMainPageClicked(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//MainPage");
	}
}