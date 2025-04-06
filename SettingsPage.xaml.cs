using IntelliJ.Lang.Annotations;

namespace QzLangProg;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();
		AppTheme currentTheme = Application.Current.RequestedTheme;
		//ManualThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;

		//TODO:	Themes are still to be worked on
		Theme themeOptions = FileReader.GetTheme();
		AutoThemeRadio.IsChecked = themeOptions.IsAuto;
		ManualThemeRadio.IsChecked = !themeOptions.IsAuto;
		ManualThemeSwitch.IsEnabled = !themeOptions.IsAuto;
		ManualThemeSwitch.IsToggled = !themeOptions.IsLight;

		Application.Current.RequestedThemeChanged += (s, a) =>
		{
			if (!ManualThemeSwitch.IsEnabled)
			{
				AppTheme currentTheme = Application.Current.RequestedTheme;
				Application.Current.UserAppTheme = currentTheme;
				ManualThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;
				OnAutoTheme(ManualThemeSwitch, new EventArgs());
			}
		};
	}

	protected override bool OnBackButtonPressed()
	{
		Shell.Current.GoToAsync("//MainPage");
		return true;
	}

	private async void RedirectToMainPage(object sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//MainPage");
	}

	private void ClearProgress(object sender, EventArgs e)
	{
		FileReader.WriteProgress(1, 1, FileReader.MainPageSample.language, 0, 100);
		FileReader.MainPageSample.LoadProgress();
	}

	private void GetRawProgressClicked(object sender, EventArgs e)
	{
		ProgressLabel.Text = FileReader.GetRawProgress();

		//ProgressLabel.Text += $"\n\nThere are {FileReader.Progress.NextLesson.Count} nextLessons";
		//ProgressLabel.Text += $"\n{FileReader.Progress.NextLesson[0].UnitID}:{FileReader.Progress.NextLesson[0].LessonID}";
	}

	private void OnAutoTheme(object sender, EventArgs e)
	{
		Application.Current.UserAppTheme = AppTheme.Unspecified;
		ManualThemeSwitch.IsEnabled = false;
		AppTheme currentTheme = Application.Current.RequestedTheme;
		ManualThemeSwitch.IsToggled = currentTheme == AppTheme.Dark;

		FileReader.WriteProgress(new Theme(true, true));
	}

	private void OnManualTheme(object sender, EventArgs e) 
	{
		AppTheme currentTheme = Application.Current.RequestedTheme;
		ManualThemeSwitch.IsEnabled = true;
		Application.Current.UserAppTheme = currentTheme;

		bool isLight = currentTheme == AppTheme.Light;
		FileReader.WriteProgress(new Theme(false, isLight));
	}

	private void ChangeTheme(object sender, EventArgs e)
	{
		if (ManualThemeSwitch.IsToggled)
			Application.Current.UserAppTheme = AppTheme.Dark;
		else
			Application.Current.UserAppTheme = AppTheme.Light;

		AppTheme currentTheme = Application.Current.UserAppTheme;

		bool isLight = currentTheme == AppTheme.Light;
		FileReader.WriteProgress(new Theme(false, isLight));
	}

	private void OnSetProgress(object sender, EventArgs e)
	{
		string[] setProgressCode = SetProgressEntry.Text.Split();

		FileReader.WriteProgress(int.Parse(setProgressCode[0]), int.Parse(setProgressCode[1]), "rq",
			FileReader.Progress.XP, FileReader.Progress.Asyqs);

		FileReader.MainPageSample.LoadProgress();
	}
}