using Microsoft.Maui.Controls.Shapes;

namespace QzLangProg
{
	public partial class MainPage : ContentPage
	{
		public int Percentage { get; set; } = 0;
		public int CorrectAnswers { get; set; } = 11;
		public int IncorrectAnswers { get; set; } = 2;
		public int HalfCorrectAnswers { get; set; } = 3;
		public int TotalQuestions { get; set; } = 20;

		public MainPage()
		{
			InitializeComponent();
		}

		private void OnCounterClicked(object sender, EventArgs e)
		{
			ProgressBarCorrect.WidthRequest += 50;
		}
	}
}