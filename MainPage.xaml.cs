namespace QzLangProg
{
	public partial class MainPage : ContentPage
	{
		int count = 0;

		public MainPage()
		{
			InitializeComponent();

		}

		private void OnCounterClicked(object sender, EventArgs e)
		{
			ProgressBarCorrect.WidthRequest += 50;

			SemanticScreenReader.Announce(CounterBtn.Text);
		}
	}
}