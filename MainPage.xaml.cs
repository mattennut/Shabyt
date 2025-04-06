using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using QzLangProg;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using static Android.Icu.Text.CaseMap;

namespace QzLangProg
{
	public partial class MainPage : ContentPage
	{
		int LineIndex = 0;		//The index of the task/lesson material. At index 0 there are lesson code and title

		//Selected lesson information. The lesson is selected by pressing on LessonsCollectionView's items
		Lesson SelectedLesson;

		//List of lessons whose lesson code is supported
		readonly List<List<LessonButtonInformation>> CertainLessons = [];

		public readonly string language = "rq";
		readonly string[] supportedLessonCodes = ["Lb"];

		private readonly IAudioManager audioManager;
		readonly Random random = new();

		//Shortcut functions for ranges
		readonly Func<int, int, int[]> RangeBetween = (first, last) => Enumerable.Range(first, last).ToArray();
		readonly Func<int, int[]> RangeUntil = (last) => Enumerable.Range(0, last).ToArray();

		//Shortcut action for sending data to TaskPage; it works, unless the page is unloaded
		readonly Action<int, int, string, string> WRMSend = delegate (int unitID, int lessonID, string language, string title)
		{ WeakReferenceMessenger.Default.Send(new OpenLesson(new Lesson(unitID, lessonID, language, title))); };

		//Lesson units, i.e., the lists of Lessons grouped into units
		readonly List<Unit> Units = FileReader.Units;

		//List of all lessons, ungrouped into units
		//Used only in instances where the units of lessons are irrelevant
		List<Lesson> AllLessons = new();


		readonly bool isNotCorrupt = true;

		public MainPage(IAudioManager audioManager)
		{
			InitializeComponent();
			
			this.audioManager = audioManager;

			FileReader.CurrentlyOpen = new Lesson(-1, -1, language, "none");
			FileReader.MainPageSample = this;

			Theme themeOptions = FileReader.GetTheme();
			if (!themeOptions.IsAuto)
			{
				if (themeOptions.IsLight) 
					Application.Current.UserAppTheme = AppTheme.Light;
				else
					Application.Current.UserAppTheme = AppTheme.Dark;
			}

			foreach (Unit unit in Units)
			{
				foreach (Lesson lesson in unit.Lessons)
				{
					AllLessons.Add(lesson);
				}
			}

			LoadProgress();
		}

		protected override bool OnBackButtonPressed()
		{
			OnExit();
			return true;
		}

		private async void OnExit()
		{
			bool leaveLesson = await DisplayAlert("Вы уверены?", "Вы собираетесь выйти с приложения", "Выйти", "Отмена");
			if (leaveLesson)
				Application.Current.Quit();
		}

		private void OnDebugClicked(object sender, EventArgs e)
		{
			
		}

		(bool isComplete, bool isLocked) CheckProgress(int UnitID, int LessonID, string Course)
		{
			if (FileReader.Progress.NextLesson[language].Language == language)
			{
				if (FileReader.Progress.NextLesson[language].UnitID == -1 && FileReader.Progress.NextLesson[language].LessonID == -1)
					return (true, false);

				List<(int unit, int lesson)> ProgressLine = []; 
				foreach (Unit unit in Units)
				{
					foreach (Lesson lesson in unit.Lessons)
					{
						ProgressLine.Add((lesson.UnitID, lesson.LessonID));
					}
				}

				bool inspectedFound = ProgressLine.Any(item => item.unit == UnitID && item.lesson == LessonID);
				bool progressFound = ProgressLine.Any(
					item => item.unit == FileReader.Progress.NextLesson[language].UnitID && 
					item.lesson == FileReader.Progress.NextLesson[language].LessonID);
				if (inspectedFound && progressFound)
				{
					//lessonFiles is obsolete, change it

					int inspectedIndex = ProgressLine.IndexOf((UnitID, LessonID));
					int progressIndex = ProgressLine.IndexOf((FileReader.Progress.NextLesson[language].UnitID, 
						FileReader.Progress.NextLesson[language].LessonID));

					if (inspectedIndex == progressIndex)
						return (false, false);
					else if (inspectedIndex > progressIndex) 
						return (false, true);
					else 
						return (true, false);
				}
			}
			return (false, false);
		}

		string GetStatusImage(int UnitID, int LessonID, string Course)
		{
			(bool isComplete, bool isLocked) status = CheckProgress(UnitID, LessonID, Course);
			if (!status.isComplete && !status.isLocked)
				return "Icons/next_track_button.png";
			else if (status.isComplete && !status.isLocked)
				return "Icons/check_mark_button.png";
			else if (!status.isComplete && status.isLocked)
				return "Icons/no_entry.png";
			return "Icons/person_standing.png";
		}

		private async void OnBottomSheet(object sender, EventArgs e)
		{
			//TODO:	Add frames to buttons

			FileReader.GetLines(SelectedLesson.UnitID, SelectedLesson.LessonID, language);
			WRMSend(SelectedLesson.UnitID, SelectedLesson.LessonID, language, SelectedLesson.Title);
			FileReader.CurrentlyOpen = 
				new Lesson(SelectedLesson.UnitID, SelectedLesson.LessonID, language, SelectedLesson.Title);
			await Shell.Current.GoToAsync("//TaskPage");
			
		}

		private void LessonSelected(object sender, SelectionChangedEventArgs e)
		{
			asd.Text = null;

			var lessonObjectsList = LessonsCollectionView.GetVisualTreeDescendants();
			Animation masterAnimation = new();

			LessonButtonInformation selected = (LessonButtonInformation)e.CurrentSelection.FirstOrDefault();
			if (selected == null)
				return;
			string selectedTitle = selected.Title;
			int selectedUnit = 1;

			for (int i = 12; i < lessonObjectsList.Count; i += 11)
            {
                if (lessonObjectsList[i] is not Grid || lessonObjectsList[i + 7] is not Label iLessonLabel)
                    continue;
                string iLessonText = iLessonLabel.Text;

                if (iLessonText == selectedTitle && lessonObjectsList[i + 5] is Image statusImage)
                {
                    if (statusImage.Source.ToString() == "File: Icons/no_entry.png")
                        continue;

                    foreach (Lesson li in AllLessons)
                    {
                        bool unitContainsLesson =
                            AllLessons.Any(lesson => lesson.Title == iLessonText);

                        //This may NOT check other units for some reason

                        if (unitContainsLesson)
                        {
                            SelectedLesson =
                                AllLessons.FirstOrDefault(lesson => lesson.Title == iLessonText);

                            selectedUnit = SelectedLesson.UnitID;

                            break;
                        }
                    }

                    if (lessonObjectsList[i + 1] is not Border)
                        continue;

                    var stack = (Border)lessonObjectsList[i + 1];
                    Animation Expand = new(v => stack.HeightRequest = v, stack.Height, 120);
                    masterAnimation.Add(0, 1, Expand);
                }
                else
                {
                    if (lessonObjectsList[i + 1] is Border stack)
                    {
                        asd.Text += $"{i}/{lessonObjectsList.Count} successful;\t {(i - 1) / 11}\n";

                        Animation Shrink = new(v => stack.HeightRequest = v, stack.Height, 52);
                        masterAnimation.Add(0, 0.5, Shrink);
                    }
                }
            }

            masterAnimation.Commit(this, "SomeRandomAssString", 16, 250, Easing.CubicInOut);


			int unitCVcounter = 1;
			for (int i = 1; i < lessonObjectsList.Count; i += 11)
            {
                if (lessonObjectsList[i] is Grid)
                    continue;

                asd.Text += $"This shit is NOT grid, slut. It is a {lessonObjectsList[i + 10].GetType()}\n";

                if (lessonObjectsList[i + 10] is CollectionView)
                {
                    unitCVcounter++;

                    if (unitCVcounter == selectedUnit)
                        continue;

                    CollectionView q = (CollectionView)lessonObjectsList[i + 10];

                    q.SelectedItem = null;
                }
            }
        }

		private void LoadProgressCatcher(object sender, EventArgs e) => LoadProgress();

		//TODO:	Save and load theme (light/dark theme, system/manual)
		public void LoadProgress(bool itemsLoaded = false)
		{
			LessonsCollectionView.ItemsSource = FileReader.Units;

			CertainLessons.Clear();
			
			var lessonObjectsList = LessonsCollectionView.GetVisualTreeDescendants();
			int unitCounter = 0;
			for ( int i = 1; i < lessonObjectsList.Count; i++ ) 
			{
				if (lessonObjectsList[i] is CollectionView UnitCV)
				{
					if (lessonObjectsList[i - 1] is Label UnitDescriptionLabel)
					{
						UnitDescriptionLabel.Text = Units[unitCounter].UnitDescription;
					}
					if (lessonObjectsList[i - 3] is Label UnitNumberLabel)
					{
						UnitNumberLabel.Text = $"Раздел #{Units[unitCounter].UnitID}";
					}

					(Dictionary<string, Lesson> nextLesson, int xp, int asyqs, Theme theme) progress = FileReader.GetProgress();
					XPLabel.Text = $"О: {progress.xp}";
					AsyqLabel.Text = $"А: {progress.asyqs}";

					if (progress.nextLesson[language].UnitID == -1 && progress.nextLesson[language].LessonID == -2)
					{
						DisplayAlert("Поврежденные файлы", "Ваш прогресс был сохранен неправильно, в результате чего невозможно востановить его", "Понятно");
						FileReader.WriteProgress(1, 1, language, 0, 100);
					}

					//j is the lesson counter within each unit, i.e., Units[unitCounter]

					CertainLessons.Add(new());

					for (int j = 0; j < Units[unitCounter].Lessons.Count; j++)
					{
						int unit = Units[unitCounter].Lessons[j].UnitID, lesson = Units[unitCounter].Lessons[j].LessonID;
						string lessonCode, title;
						var gotten = FileReader.GetTitle(unit, lesson, language);
						(lessonCode, title) = gotten.Result;

						if (supportedLessonCodes.Contains(lessonCode))
						{
							CertainLessons[unitCounter].Add(new LessonButtonInformation
							{
								UnitID = Units[unitCounter].Lessons[j].UnitID,
								LessonID = Units[unitCounter].Lessons[j].LessonID,
								Title = Units[unitCounter].Lessons[j].Title,
								StatusImage = GetStatusImage(Units[unitCounter].Lessons[j].UnitID,
								Units[unitCounter].Lessons[j].LessonID, language)
							});
						}
					}

					UnitCV.ItemsSource = CertainLessons[unitCounter];

					unitCounter++;
				}
			}

			if (!itemsLoaded) { 
				var timer = Application.Current.Dispatcher.CreateTimer();
				timer.Interval = TimeSpan.FromSeconds(0.1);
				timer.IsRepeating = false;
				timer.Tick += (s, e) => LoadProgress(true);
				timer.Start();
			}
		}

		private async void OpenSettings(object sender, EventArgs e)
		{
			await Shell.Current.GoToAsync("//SettingsPage");
		}
	}
}

public class OpenLesson : ValueChangedMessage<Lesson>
{
	public OpenLesson(Lesson value) : base(value) { }
}

public class Lesson(int unitID, int lessonID, string language, string title)
{
	public int UnitID = unitID;
	public int LessonID = lessonID;
	public string Language = language;
	public string Title = title;
}

/*
 
<!-- i -->
					<Grid>
						<!-- i + 1 -->
							<!-- i + 2 -->
						<Border Margin="5" HeightRequest="52" 
									StrokeShape="RoundRectangle 13.5"
									Stroke="Transparent"
									>
							
							<!-- i + 3 -->
							<StackLayout HorizontalOptions="FillAndExpand" Padding="5" Orientation="Vertical"
								BackgroundColor="{AppThemeBinding	
									Light={StaticResource ButtonLight}, 
									Dark={StaticResource PanelsDark}}">

								<!-- i + 4 -->
								<HorizontalStackLayout VerticalOptions="Start" HeightRequest="40">
									<!-- i + 5 -->
									<Image Source="{Binding StatusImage}" HeightRequest="40" HorizontalOptions="Start" />

									<!-- i + 6 -->
									<BoxView HeightRequest="30" WidthRequest="2.5" Color="#D3D0C9" 
												CornerRadius="1.25" Margin="5, 0, 10, 0" VerticalOptions="Center"/>
									<!-- i + 7 -->
									<Label VerticalOptions="Center" Text="{Binding Title}"/>
								</HorizontalStackLayout>

								<!-- i + 8 -->
								<StackLayout>
									<!-- i + 9 -->
									<Label Text="Тип урока: вокабуляр" Margin="60, 2.5, 5, 2.5"/>
									<!-- i + 10 -->
									<Button Text="Начать" Style="{StaticResource AccentButton}" TextColor="{StaticResource TextDark}"
													HorizontalOptions="End" VerticalOptions="End"
													Margin="0, 7.5, 2.5, 0"
													WidthRequest="150" HeightRequest="30" Padding="0"
													Pressed="OnBottomSheet"/>
								</StackLayout>
							</StackLayout>
						</Border>
					</Grid> 
 
*/