using Plugin.Maui.Audio;
using Microsoft.Maui.Controls.Shapes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.Maui.Extensions;

namespace QzLangProg;

public partial class TaskPage : ContentPage
{
	public string lessonText { get; set; } = "";

	int LineIndex = -1;

	readonly List<StackLayout> SLayouts;
	string currentAudioTrack = "";
	readonly List<string> CorrectAnswers = [];
	readonly List<string> MatchAudioList = []; //List of audios presented in AM|Audio Match
	bool isTask = false, isTextualTask = false, isSubmitClickable = true;
	int Unit, Lesson;
	string Language;
	string LessonTitle;

	List<string> LessonFlow = [];
	int LessonFlowTaskAmount = 0;       //Amount of tasks (non-informational) within 'LessonFlow'
	int CorrectAnswersAmount = 0, IncorrectAnswersAmount = 0, HalfCorrectAnswersAmount = 0;
	float SuccessRate, MistakeRate, OverallRate;
	/* Success Rate Calculations
	 * |Description															|	Variable names in the code
		n - denominator; overall attempts and left questions				|	LessonFlowTaskAmount
		l - amount of questions left										|	
		a - overall attempts; varies depending on the user-made mistakes	|	
		x - fully correct answers											|	CorrectAnswersAmount
		y - half-correct answers											|	HalfCorrectAnswersAmount
		m - mistakes														|	IncorrectAnswersAmount
		p - success rate													|	SuccessRate
		M - mistake rate													|	MistakeRate

		Formulas:
		n = a + l
		m = a - (x + y)
		p = 100%/n*(x+0.5y)
		M = m / n
	*/

	int Streak = 0, CurrentStreak = 0, XP, AsyqReward;

	readonly string[] informalSingular = ["Әбеке", "Балам❤️", "Анашым❤️", "Інім👎"];
	readonly string[] formalSingular = ["Доктор", "Бастық", "Ц.Ю. Амангелді"];
	readonly string[] informalPlural = ["Достарым", "Туыстар", "Балалар"];
	readonly string[] formalPlural = ["Әріптестер", "Конференция", "11A ата-аналары"];

	private readonly IAudioManager audioManager;
	readonly Random random = new();

	//Functions and Actions
	readonly Func<int, int, int[]> RangeBetween = (first, last) => Enumerable.Range(first, last).ToArray();
	readonly Func<int, int[]> RangeUntil = (last) => Enumerable.Range(0, last).ToArray();
	readonly Action<byte, string, string> WRMSendFeedBack = delegate (byte feedbackType, string inputText, string correctText)
	{ WeakReferenceMessenger.Default.Send(new OpenAnswerFeedback(new FeedbackMessage(feedbackType, inputText, correctText))); };

	readonly Action<float, int, int, int, int> WRMSendResults = 
		delegate (float SuccessRate, int Mistakes, int Streak, int XP, int Asyq) 
	{ WeakReferenceMessenger.Default.Send(new RedirectToResultsPage(new LessonResults(SuccessRate, Mistakes, Streak, XP, Asyq))); };

	//Note: method is fired only once, the first time
	public TaskPage(IAudioManager audioManager /*int unitID, int lessonID, string language*/)
	{
		InitializeComponent();

		this.audioManager = audioManager;
		WeakReferenceMessenger.Default.Register<StartRenderNextTask>(this, RenderNextTask);

		WeakReferenceMessenger.Default.Register<OpenLesson>(this, RenderLesson);

		LessonFlowTaskAmount = FileReader.TaskAmount;
		ProgressProportion.Text = $"0/{FileReader.TaskAmount}";
		ProgressBarCorrect.ScaleTo(0, 0, Easing.CubicInOut);
		ProgressBarIncorrect.ScaleTo(0, 0, Easing.CubicInOut);
		ProgressBarHalfCorrect.ScaleTo(0, 0, Easing.CubicInOut);
		LoadLessonInformation();

		SLayouts = [trStackLayout, fgStackLayout, spStackLayout, grStackLayout, tbStackLayout, tfStackLayout,
			swStackLayout, arStackLayout, aqStackLayout, asStackLayout, ftStackLayout, amStackLayout];

		LessonFlow = FileReader.Lines[1..];

		OnRender(asStackLayout, new());
		ResultsMenu.TranslateTo(0, 100, 0);
	}

	protected override bool OnBackButtonPressed()
	{
		OnExitClicked(asStackLayout, new());
		return true;
	}

	private void LoadLessonInformation()
	{
		Unit = FileReader.CurrentlyOpen.UnitID;
		Lesson = FileReader.CurrentlyOpen.LessonID;
		Language = FileReader.CurrentlyOpen.Language;
		LessonTitle = FileReader.CurrentlyOpen.Title;
	}

	//Note: method is fired each time the page is opened, except for the first time
	private void RenderLesson(object sender, OpenLesson message)
	{
		LineIndex = -1;
		Streak = 0; 
		CurrentStreak = 0;
		StreakLabel.Text = "";

		LessonFlowTaskAmount = FileReader.TaskAmount;
		ProgressProportion.Text = $"0/{FileReader.TaskAmount}";

		CorrectAnswersAmount = 0;
		IncorrectAnswersAmount = 0;
		HalfCorrectAnswersAmount = 0;

		CalculateSuccessRate();
		ProgressBarCorrect.ScaleTo((SuccessRate + MistakeRate) * ProgressBarFrame.Width * 0.1f, 0, Easing.CubicInOut);
		ProgressBarIncorrect.ScaleTo(MistakeRate * ProgressBarFrame.Width * 0.1f, 0, Easing.CubicInOut);
		ProgressBarHalfCorrect.ScaleTo((OverallRate + MistakeRate) * ProgressBarFrame.Width * 0.1f, 0, Easing.CubicInOut);

		LessonFlow = FileReader.Lines[1..];

		OnRender(asStackLayout, new System.EventArgs());
	}

	private void OnSubmitClicked(object sender, EventArgs e)
	{
		if (!isTask)
			//Material
			RenderNextTask(sender, new StartRenderNextTask(true));

		else if (isSubmitClickable && isTextualTask && string.IsNullOrEmpty(InputEntry.Text))
		{ }	//If textual task and the input field is empty
		else if (isSubmitClickable)
		{
			isSubmitClickable = false;

			if (!isTextualTask) //For non-textual questions
			{
				if (MatchAudioList.Count > 0)           
				{
					//AM|AudioMatch: correct
					if (currentAudioTrack == CorrectAnswers[0])
					{
						CallResults(0, null, null);
						CorrectAnswersAmount++;
						OnStreakUpdate(true);
					}
					//AM|AudioMatch: incorrect
					else
					{
						CallResults(1, null, null);
						LessonFlow.Add(LessonFlow[LineIndex]);
						LessonFlowTaskAmount++;
						IncorrectAnswersAmount++;
						OnStreakUpdate(false);
					}
				}
				else
				{
					//Currently impossible, since AM|AudioMatch is the only task type
					//	that is a task and non-textual that uses Empty Bottom Menu's submit button
					CallResults(0, null, null);
					CorrectAnswersAmount++;
					OnStreakUpdate(true);
				}
			}
			else if (CorrectAnswers.Contains(Standardise(InputEntry.Text)))
			{
				//If textual task and correct
				CallResults(0, null, null);
				CorrectAnswersAmount++;
				OnStreakUpdate(true);
			}
			else
			{
				//If textual task and correct
				if (CorrectAnswers.Count >= 1)	//For various possible answers
					CallResults(1, InputEntry.Text, CorrectAnswers[0]);
				else							//For a single possible answer
					CallResults(1, InputEntry.Text, null);
				LessonFlow.Add(LessonFlow[LineIndex]);
				LessonFlowTaskAmount++;
				IncorrectAnswersAmount++;
				OnStreakUpdate(false);
			}

			//Whenever the user clicks the button, the entry gets deselected
			InputEntry.IsEnabled = false;
			InputEntry.IsEnabled = true;

			CalculateSuccessRate();
		}
	}

	private void CallResults(byte feedbackType, string inputText, string correctText)
	{
		ResultsMenu.TranslateTo(0, -5, 250 , Easing.CubicInOut);
		switch (feedbackType)
		{
			case 0:
				FeedbackReaction.SetAppTheme(BackgroundColorProperty,
					GetColorFromResource("CorrectLight"),
					GetColorFromResource("CorrectDark"));
				FeedbackText.Text = "Правильно!";
				break;
			case 1:
				FeedbackReaction.SetAppTheme(BackgroundColorProperty,
					GetColorFromResource("IncorrectLight"),
					GetColorFromResource("IncorrectDark"));
				FeedbackText.Text = $"К сожалению! Правильный ответ: {correctText}";
				break;
			default:
				FeedbackText.Text = "Error: AnswerFeedback received an unknown feedback type";
				break;
		}
	}

	private void OnIncorrectClicked (object sender, EventArgs e)
	{
		if (isSubmitClickable)
		{
			isSubmitClickable = false;

			LessonFlow.Add(LessonFlow[LineIndex]);
			LessonFlowTaskAmount++;
			IncorrectAnswersAmount++;
			OnStreakUpdate(false);

			CalculateSuccessRate();

			/*
			 
						
						
						
						*/

			if (CorrectAnswers.Count == 1)
				//WRMSendFeedBack(1, null, CorrectAnswers[0]);
				CallResults(1, null, CorrectAnswers[0]);
			else
				//WRMSendFeedBack(1, null, null);
				CallResults(1, null, null);
		}
	}

	void CalculateSuccessRate()
	{
		MistakeRate = (float)IncorrectAnswersAmount / LessonFlowTaskAmount;
		SuccessRate = (CorrectAnswersAmount + (float)HalfCorrectAnswersAmount / 2) / LessonFlowTaskAmount;
		OverallRate = (float)(CorrectAnswersAmount + HalfCorrectAnswersAmount) / LessonFlowTaskAmount;

		ProgressBarCorrect.ScaleTo((SuccessRate + MistakeRate) * ProgressBarFrame.Width * 0.1f, 500, Easing.CubicInOut);
		ProgressBarIncorrect.ScaleTo(MistakeRate * ProgressBarFrame.Width * 0.1f, 500, Easing.CubicInOut);
		ProgressBarHalfCorrect.ScaleTo((OverallRate + MistakeRate) * ProgressBarFrame.Width * 0.1f, 500, Easing.CubicInOut);

		ProgressProportion.Text = $"{CorrectAnswersAmount + HalfCorrectAnswersAmount}/{FileReader.TaskAmount}";

		CorrectAnswerSpan.Text = CorrectAnswersAmount.ToString();
		IncorrectAnswerSpan.Text = IncorrectAnswersAmount.ToString();
		//HalfCorrectAnswerSpan.Text = HalfCorrectAnswersAmount.ToString();
	}

	public void OnStreakUpdate(bool isCorrect)
	{
		if (isCorrect)
		{
			CurrentStreak++;
			if (CurrentStreak > Streak) Streak = CurrentStreak;
		}
		else
			CurrentStreak = 0;

		if (CurrentStreak > 1)
			StreakLabel.Text = $"Стрик: {CurrentStreak}";
		else
			StreakLabel.Text = "";
	}

	private void OnRender(object sender, EventArgs e)
	{
		RenderNextTask(sender, new StartRenderNextTask(true));
		ResultsMenu.TranslateTo(0, 100, 250, Easing.CubicInOut);
	}

	public async void RenderNextTask(object sender, StartRenderNextTask message)
	{
		//Overkill: fired each render; although, only needed to be fired once
		LoadLessonInformation();

		LineIndex++;
		CorrectAnswers.Clear();
		MatchAudioList.Clear();
		isSubmitClickable = true;

		//Make each StackLayout invisible and add index in order to skip current task
		SLayouts.ToList().ForEach(SL => SL.IsVisible = false);
		spBottomMenu.IsVisible = emptyBottomMenu.IsVisible = entryBottomMenu.IsVisible = tfBottomMenu.IsVisible = false;

		MatchCollection matches;
		HeaderName.Text = string.Empty;
		int[] order;
		int divIndex, equalIndex;

		if (LineIndex >= LessonFlow.Count)
		{
			List<(int unit, int lesson)> ProgressLine = [];
			foreach (Unit unit in FileReader.Units)
			{
				foreach (Lesson lesson in unit.Lessons)
				{
					ProgressLine.Add((lesson.UnitID, lesson.LessonID));
				}
			}

			int currentlyOpenedIndex = ProgressLine.IndexOf((Unit, Lesson));
			int progressIndex = ProgressLine.IndexOf((FileReader.Progress.NextLesson[Language].UnitID,
				FileReader.Progress.NextLesson[Language].LessonID));

			bool isCourseComplete = (FileReader.Progress.NextLesson[Language].LessonID == -1
								&& FileReader.Progress.NextLesson[Language].UnitID == -1);

			if (currentlyOpenedIndex + 1 != ProgressLine.Count && !isCourseComplete) 
			{			//If not the last lesson
				if (currentlyOpenedIndex >= progressIndex)  //If new lesson
				{
					(XP, AsyqReward) = CalculateXPAndAsyqs(false);

					FileReader.WriteProgress(ProgressLine[currentlyOpenedIndex + 1],
						Language, FileReader.Progress.XP + XP, FileReader.Progress.Asyqs + AsyqReward);
				}
				else                                        //If repetition
				{
					(XP, AsyqReward) = CalculateXPAndAsyqs(true);
					FileReader.WriteProgress(ProgressLine[progressIndex],
						Language, FileReader.Progress.XP + XP, FileReader.Progress.Asyqs + AsyqReward);
				}
			}
			else
			{            //If is the last lesson
				if (isCourseComplete)
				{
					(XP, AsyqReward) = CalculateXPAndAsyqs(true);
					FileReader.WriteProgress(-1, -1, Language, FileReader.Progress.XP + XP,
						FileReader.Progress.Asyqs + AsyqReward);
				}
				else
				{
					(XP, AsyqReward) = CalculateXPAndAsyqs(false);
					FileReader.WriteProgress(-1, -1, Language, FileReader.Progress.XP + XP,
						FileReader.Progress.Asyqs + AsyqReward);
				}
			}

			
			FileReader.MainPageSample.LoadProgress();
			await Shell.Current.GoToAsync("//ResultPage");

			

			FileReader.LessonResults = (SuccessRate, IncorrectAnswersAmount, Streak, XP, AsyqReward);
			WRMSendResults(SuccessRate, IncorrectAnswersAmount, Streak, XP, AsyqReward);


			return;
		}
		else
		{
			string material = LessonFlow[LineIndex][3..];

			//GR TB AR			TR FG AQ FT BQ		AM SW SP AS TF
			switch (LessonFlow[LineIndex][..2])
			{
				case "gr":
					isTask = isTextualTask = false;

					grStackLayout.IsVisible = true;
					emptyBottomMenu.IsVisible = true;
					grLabelLayout.Children.Clear();

					matches = Regex.Matches(material, @"\[(.*?)\]");

					foreach (Match match in matches)
					{
						string messageText = match.Groups[1].Value;
						grLabelLayout.Children.Add(ChatMessage(messageText));
					}

					break;
				case "tb":
					isTask = isTextualTask = false;

					tbStackLayout.IsVisible = true;
					emptyBottomMenu.IsVisible = true;

					//Pretext
					tbPretextLayout.Children.Clear();

					string pretextParagraphs = material.Split('|')[1];
					matches = Regex.Matches(pretextParagraphs, @"\[(.*?)\]");

					foreach (Match match in matches)
					{
						string messageText = match.Groups[1].Value;
						if (!string.IsNullOrEmpty(messageText))
							tbPretextLayout.Children.Add(ChatMessage(messageText));
					}

					//Table itself
					//Creating rows

					string[] tableValues = material.Split('|')[2][1..^1].Split(';');
					int columnCount = int.Parse(material.Split('|')[0]);
					int rowCount = tableValues.Length / columnCount;

					// Create a new array to hold the flipped values
					string[] flippedArray = new string[tableValues.Length];

					int counter = 0;
					// Loop through the original array in reverse order for each row
					for (int x = 0; x < rowCount; x++)
					{
						for (int y = 0; y < columnCount; y++)
						{
							// Column * Rows + Row
							int originalIndex = y * rowCount + x;
							// Assign the value to the flipped array with flipped order
							flippedArray[originalIndex] = tableValues[counter];
							counter++;
						}
					}

					tbTableCollectionView.ItemsLayout = new GridItemsLayout(rowCount, ItemsLayoutOrientation.Horizontal);
					tbTableCollectionView.ItemsSource = flippedArray;


					//Posttext
					tbPosttextLayout.Children.Clear();

					string posttextParagraphs = material.Split('|')[3];
					matches = Regex.Matches(posttextParagraphs, @"\[(.*?)\]");
					foreach (Match match in matches)
					{
						string messageText = match.Groups[1].Value;
						if (!string.IsNullOrEmpty(messageText))
							tbPosttextLayout.Children.Add(ChatMessage(messageText));
					}

					break;

				case "ar":
					isTask = isTextualTask = false;

					currentAudioTrack = material.Split('|')[0];
					matches = Regex.Matches(material, @"\[(.*?)\]");
					arGiven.Text = matches[0].Groups[1].Value;
					arTranslation.Text = matches[1].Groups[1].Value;
					arExample.Text = matches[2].Groups[1].Value;
					arExampleTranslation.Text = matches[3].Groups[1].Value;
					arExampleStackLayout.IsVisible = (!string.IsNullOrWhiteSpace(arExample.Text) && !string.IsNullOrWhiteSpace(arExampleTranslation.Text));

					arStackLayout.IsVisible = true;
					emptyBottomMenu.IsVisible = true;
					break;

				case "tr":
					isTask = isTextualTask = true;

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					HeaderName.Text = HeaderNameDispenser(material[..2]);
					matches = Regex.Matches(material[(divIndex + 1)..], @"\{(.*?)\}");
					CorrectAnswers.AddRange(matches.Cast<Match>().Select(m => Standardise(m.Groups[1].Value)));
					trGiven.Text = material[3..equalIndex];
					trStackLayout.IsVisible = true;
					entryBottomMenu.IsVisible = true;
					break;

				case "fg":
					isTask = isTextualTask = true;

					matches = Regex.Matches(material[(material.IndexOf('=') + 1)..], @"\{(.*?)\}");
					CorrectAnswers.AddRange(matches.Cast<Match>().Select(m => Standardise(m.Groups[1].Value)));
					fgGiven.Text = material[..(material.IndexOf('='))];
					fgStackLayout.IsVisible = true;
					entryBottomMenu.IsVisible = true;
					break;

				case "aq":
					isTask = isTextualTask = true;

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					matches = Regex.Matches(material[(material.IndexOf('=') + 1)..], @"\{(.*?)\}");
					CorrectAnswers.AddRange(matches.Cast<Match>().Select(m => Standardise(m.Groups[1].Value)));
					aqQuestion.Text = material[..divIndex];
					currentAudioTrack = material[(divIndex + 1)..equalIndex];
					aqStackLayout.IsVisible = true;
					entryBottomMenu.IsVisible = true;
					break;

				case "ft":
					//ft|Given|TranslationWithGap={Answer}{AltAnswer}{AltAnswer}...
					isTask = isTextualTask = true;

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					matches = Regex.Matches(material[(material.IndexOf('=') + 1)..], @"\{(.*?)\}");
					CorrectAnswers.AddRange(matches.Cast<Match>().Select(m => Standardise(m.Groups[1].Value)));
					ftGiven.Text = material[..divIndex];
					ftGapped.Text = material[(divIndex + 1)..equalIndex];
					ftStackLayout.IsVisible = true;
					entryBottomMenu.IsVisible = true;
					break;

				case "am":
					//am|Given={CorrectAnswer}{IncorrectAnswer}{IncorrectAnswer}

					//Audio settings: MP3, Sample Rate: 16000 (for now, maybe 22050 later), Mono, Standard Quality (170-210 kbps)
					(isTask, isTextualTask) = (true, false);

					equalIndex = material.IndexOf('=');

					matches = Regex.Matches(material[(material.IndexOf('=') + 1)..], @"\{(.*?)\}");
					CorrectAnswers.Add(matches[0].Groups[1].Value);

					order = RangeUntil(matches.Count);
					order = RandomiseList(order);

					for (int i = 0; i < matches.Count; i++)
					{
						MatchAudioList.Add(matches[order[i]].Groups[1].Value);

						AbsoluteLayout AudioMessageAL = new()
						{
							WidthRequest = 200,
						};

						Button AudioMessageButton = new()
						{
							BorderWidth = 2.5,
							CornerRadius = 7,
							HeightRequest = 45,
							WidthRequest = 200,
							Margin = new Thickness(0, 5, 0, 5)
						};
						AudioMessageButton.Clicked += AudioMessage_Clicked;

						HorizontalStackLayout AudioMessageHSL = new();

						AbsoluteLayout.SetLayoutBounds(AudioMessageHSL, new Rect(15, 20, 200, 15));


						//	!!!	Changing ImageSource to IconTint shit is extremely difficult
						//		Like actually, just don't bother girl
						Image AudioMessageButtonImage = new()
						{
							Source = "Resources/Images/play_button_dark.png"
						};
						AudioMessageButtonImage.SetAppTheme<ImageSource>(Image.SourceProperty,
							"Resources/Images/play_button_light.png",
							"Resources/Images/play_button_dark.png");

						ProgressBar AudioMessageProgressBar = new()
						{
							WidthRequest = 150,
							Margin = new Thickness(10, 0, 0, 0)
						};

						AudioMessageHSL.Children.Add(AudioMessageButtonImage);
						AudioMessageHSL.Children.Add(AudioMessageProgressBar);
						AudioMessageAL.Children.Add(AudioMessageButton);
						AudioMessageAL.Children.Add(AudioMessageHSL);

						amAudioStackLayout.Children.Add(AudioMessageAL);
					}

					MatchAudioList.AddRange(matches.Cast<Match>().Select(m => m.Groups[1].Value));
					amGiven.Text = material[..equalIndex];
					amStackLayout.IsVisible = true;
					emptyBottomMenu.IsVisible = true;
					break;

				case "sw":
					(isTask, isTextualTask) = (true, false);

					equalIndex = material.IndexOf('=');

					swStackLayout.IsVisible = true;

					swEmojiGrid.Clear();

					matches = Regex.Matches(material[(equalIndex + 1)..], @"\{(.*?)\}");
					CorrectAnswers.Add(matches[0].Groups[1].Value.Split('|')[0]);
					swGiven.Text = material[..equalIndex];

					order = RangeUntil(matches.Count);
					order = RandomiseList(order);

					for (int i = 0; i < matches.Count; i++)
					{
						Frame emojiFrame = new()
						{
							WidthRequest = 110,
							BorderColor = Color.FromArgb("00000000"),
							BackgroundColor = Color.FromArgb("00000000")
						};
						emojiFrame.SetValue(Grid.ColumnProperty, i % 2);
						emojiFrame.SetValue(Grid.RowProperty, i / 2);

						AbsoluteLayout emojiLayout = new()
						{
							WidthRequest = 110,
							HeightRequest = 120
						};

						Button emojiButton = new()
						{
							CornerRadius = 10,
							VerticalOptions = LayoutOptions.Start,
							BorderWidth = 2.5,
							WidthRequest = 110,
							HeightRequest = 120,
						};
						if (matches[order[i]].Groups[1].Value.Split('|')[0] == CorrectAnswers[0])
							emojiButton.Clicked += OnSubmitClicked;
						else emojiButton.Clicked += OnIncorrectClicked;

						VerticalStackLayout imageLayout = new()
						{
							HorizontalOptions = LayoutOptions.Center,
							WidthRequest = 110,
							Padding = 20
						};
						Image emojiImage = new() { HeightRequest = 60 };
						emojiImage.Source = $"Resources/Images/Icons/no_image.png";
						try
						{
							emojiImage.Source = $"Resources/Images/Icons/{matches[order[i]].Groups[1].Value.Split('|')[1].Replace('-', '_')}.png";
						}
						catch { }
						Label emojiText = new() { 
							Text = $"{matches[order[i]].Groups[1].Value.Split('|')[0]}", 
							HorizontalOptions = LayoutOptions.Center,
							VerticalOptions = LayoutOptions.End
						};

						imageLayout.Children.Add(emojiImage);
						imageLayout.Children.Add(emojiText);

						emojiLayout.Children.Add(emojiButton);
						emojiLayout.Children.Add(imageLayout);

						emojiFrame.Content = emojiLayout;

						swEmojiGrid.Children.Add(emojiFrame);
					}

					break;

				case "sp":
					(isTask, isTextualTask) = (true, false);

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					spBMStackLayout.Children.Clear();
					spStackLayout.IsVisible = true;
					spBottomMenu.IsVisible = true;

					spGiven.Text = material[(divIndex + 1)..equalIndex];
					spQuestion.Text = material[..divIndex];

					matches = Regex.Matches(material[(equalIndex + 1)..], @"\{(.*?)\}");
					CorrectAnswers.Add(matches[0].Groups[1].Value.Split('|')[0]);

					order = RangeUntil(matches.Count);
					RandomiseList(order);

					for (int i = 0; i < order.Length; i++)
					{
						string buttonText = matches[order[i]].Groups[1].Value;
						bool isCorrect = buttonText == CorrectAnswers[0];
						Button answerButton = SelectButton(buttonText, isCorrect);
						spBMStackLayout.Children.Add(answerButton);
					}

					spScrollView.HeightRequest = 375 - spBottomMenu.Height;
					break;

				case "as":
					(isTask, isTextualTask) = (true, false);

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					spBMStackLayout.Children.Clear();
					asStackLayout.IsVisible = true;
					spBottomMenu.IsVisible = true;

					currentAudioTrack = material[(divIndex + 1)..equalIndex];
					asQuestion.Text = material[..divIndex];

					matches = Regex.Matches(material[(equalIndex + 1)..], @"\{(.*?)\}");
					CorrectAnswers.Add(matches[0].Groups[1].Value.Split('|')[0]);

					order = RangeUntil(matches.Count);
					order = RandomiseList(order);

					for (int i = 0; i < order.Length; i++)
					{
						string buttonText = matches[order[i]].Groups[1].Value;
						bool isCorrect = buttonText == CorrectAnswers[0];
						Button answerButton = SelectButton(buttonText, isCorrect);
						spBMStackLayout.Children.Add(answerButton);
					}

					asScrollView.HeightRequest = 375 - spBottomMenu.Height;
					break;

				case "tf":
					(isTask, isTextualTask) = (true, false);

					divIndex = material.IndexOf('|');
					equalIndex = material.IndexOf('=');

					tfStackLayout.IsVisible = true;
					tfBottomMenu.IsVisible = true;

					tfGiven.Text = $"{material[..divIndex]}";
					tfQuestion.Text = material[(divIndex + 1)..equalIndex];

					if (material[^1] == 't')
					{
						tfBMTrueButton.Clicked -= OnSubmitClicked;
						tfBMTrueButton.Clicked -= OnIncorrectClicked;
						tfBMFalseButton.Clicked -= OnIncorrectClicked;
						tfBMFalseButton.Clicked -= OnSubmitClicked;

						tfBMTrueButton.Clicked += OnSubmitClicked;
						tfBMFalseButton.Clicked += OnIncorrectClicked;
					}
					else
					{
						tfBMTrueButton.Clicked -= OnSubmitClicked;
						tfBMTrueButton.Clicked -= OnIncorrectClicked;
						tfBMFalseButton.Clicked -= OnIncorrectClicked;
						tfBMFalseButton.Clicked -= OnSubmitClicked;


						tfBMTrueButton.Clicked += OnIncorrectClicked;
						tfBMFalseButton.Clicked += OnSubmitClicked;
					}
					break;

				default:
					emptyBottomMenu.IsVisible = true;
					break;
			}
		}

		InputEntry.Text = null;
	}

	private (int XP, int AsyqReward) CalculateXPAndAsyqs(bool isRevision)
	{
		//IF First time:
		//100%		— 10xp	+5 for perfect;				25asyq
		//80–99%	— 9xp	+1 per 2streak(max of 4)	25asyq * percentage
		//60–79%	— 7xp	+1 per 2streak(max of 4)	25asyq * percentage
		//..–59%	— 5xp	+1 per 2streak(max of 4)	15asyq

		//IF Revision:
		//100%		— 7xp								15asyq
		//70–99%	— 4xp	+1 for 3streak(max of 1)	13asyq
		//..–69%	— 3xp	_							10asyq

		if (!isRevision)
		{
			int bonusXP = Streak / 2;
			if (bonusXP > 4) bonusXP = 4;
			(int, int) reward = SuccessRate switch
			{
				1					=> (15, 25),
				>= 0.8f and < 1		=> (9 + bonusXP, (int)(25f * SuccessRate)),
				>= 0.6f and < 0.8f	=> (7 + bonusXP, (int)(25f * SuccessRate)),
				_					=> (5 + bonusXP, 15)
			};
			return reward;
		}
		else
		{
			int bonusXP = (Streak > 2 ? 1 : 0);
			(int, int) reward = SuccessRate switch
			{
				1					=> (7, 15),
				>= 0.7f and < 1		=> (4 + bonusXP, 13),
				_					=> (3, 10)
			};
			return reward;
		}
	}

	private void AudioMessage_Clicked(object sender, EventArgs e)
	{
		int AudioIndex = 0;
		for (int i = 0; i < amAudioStackLayout.Children.Count; i++) {
			((Button)((AbsoluteLayout)amAudioStackLayout.Children[i]).Children[0]).
					SetAppTheme(Button.BorderColorProperty,
						GetColorFromResource("ButtonBorderLight"),
						GetColorFromResource("BorderDark"));
			if (amAudioStackLayout.Children[i] == ((Button)sender).Parent)
			{
				((Button)sender).SetAppTheme(Button.BorderColorProperty,
						GetColorFromResource("CorrectLight"),
						GetColorFromResource("CorrectDark"));
				AudioIndex = i;
			}
		}
		currentAudioTrack = MatchAudioList[AudioIndex];
		OnPlayAudio(((ProgressBar)((HorizontalStackLayout)((AbsoluteLayout)((Button)sender).Parent).Children[1]).Children[1]), e);
	}

	public static Color GetColorFromResource(string name)
	{
		Color color = null;
		if (App.Current.Resources.TryGetValue(name, out var colorvalue))
			color = (Color)colorvalue;
		return color;
	}

	private async void OnExitClicked(object sender, EventArgs e)
	{
		bool leaveLesson = await DisplayAlert("Вы уверены?", "Вы собираетесь выйти с урока. Ваш прогресс НЕ БУДЕТ сохранен", "Выйти", "Вернуться");
		if (leaveLesson)
			await Shell.Current.GoToAsync("//MainPage");
	}

	List<MessageSpan> getSpans(string text)
	{
		List<MessageSpan> separatedElements = [];

		bool insideNumberSign = false;
		string currentText = "";

		foreach (char character in text)
		{
			if (character == '№')
			{
				if (!string.IsNullOrWhiteSpace(currentText)) separatedElements.Add(new MessageSpan(currentText, insideNumberSign));
				insideNumberSign = !insideNumberSign;

				currentText = "";
			}
			else
			{
				currentText += character;
			}
		}
		if (!string.IsNullOrWhiteSpace(currentText)) separatedElements.Add(new MessageSpan(currentText, insideNumberSign));

		return separatedElements;
	}

	string Standardise(string text)
	{
		if (text != null)
		{
			string standardised = new string(text.Where(IsValidCharacter).ToArray()).Trim();
			string[] words = standardised.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			standardised = string.Join(" ", words);
			return standardised.ToLower();
		}
		return null;
	}

	bool IsValidCharacter(char textCharacter)
	{
		if (char.IsLetter(textCharacter) || textCharacter == ' ')
			return true;
		return false;
	}

	Border ChatMessage(string text)
	{
		List<MessageSpan> messageSpans = getSpans(text);

		Border paragraphBorder = new()
		{
			Padding = 10,
			Margin = new Thickness(0, 0, 0, 5),
			StrokeShape = new RoundRectangle()
			{
				CornerRadius = new CornerRadius(10, 10, 0, 10)
			},
			Stroke = Color.FromArgb("#00000000"),
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Start,
		};

		if (Resources.TryGetValue("MessageBorder", out object value) &&
		value is Style borderStyle)
			paragraphBorder.Style = borderStyle;

		Label paragraphLabel = new()
		{
			FontSize = 14,
			HorizontalOptions = LayoutOptions.Center
		};

		FormattedString formattedString = new();

		foreach (MessageSpan span in messageSpans)
		{
			Span paragraphSegment = new()
			{
				FontFamily = (span.IsBold ? "GeologicaBold" : "GeologicaLight"),
				Text = span.Text
			};
			formattedString.Spans.Add(paragraphSegment);
		}

		paragraphLabel.FormattedText = formattedString;

		paragraphBorder.Content = paragraphLabel;
		return paragraphBorder;
	}

	Button SelectButton(string text, bool isCorrect)
	{
		/*<Button BorderWidth="2.5" HeightRequest="35" Padding="-5"
						Text="SampleAnswer" />*/
		Button button = new()
		{
			BorderWidth = 2.5,
			HeightRequest = 35,
			Padding = -5,
			Text = text
		};
		if (isCorrect)
			button.Clicked += OnSubmitClicked;
		else
			button.Clicked += OnIncorrectClicked;
		return button;
	}

	string HeaderNameDispenser(string input)
	{
		Random random = new();
		return input switch
		{
			"is" => informalSingular[random.Next(informalSingular.Length)],
			"fs" => formalSingular[random.Next(formalSingular.Length)],
			"ip" => informalPlural[random.Next(informalPlural.Length)],
			"fp" => formalPlural[random.Next(formalPlural.Length)],
			"no" => null,
			_ => "Bozo",
		};
	}

	int[] RandomiseList(int[] list)
	{
		for (int i = list.Length - 1; i > 0; i--)
		{
			int j = random.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
		return list;
	}

	private async void OnPlayAudio(object sender, EventArgs e)
	{
		var player = audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync($"Audios/not_found.mp3"));
		
		string sourceFile = $"Audios/{currentAudioTrack}.mp3";
		string sourcePath = $"{FileSystem.Current.AppDataDirectory}/{sourceFile}";

		try
		{
			bool fileExists = await FileSystem.AppPackageFileExistsAsync(sourceFile);
			if (fileExists)
			{
				player = audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync($"Audios/{currentAudioTrack}.mp3"));
			}
		}
		catch 
		{

		}

		player.Play();

		switch (LessonFlow[LineIndex][..2])
		{
			case "ar":
				await arAudioProgressBar.ProgressTo(0, 0, Easing.Linear);
				await arAudioProgressBar.ProgressTo(1, (uint)(player.Duration * 1000), Easing.Linear);
				await arAudioProgressBar.ProgressTo(0, 200, Easing.CubicOut);
				break;
			case "aq":
				await aqAudioProgressBar.ProgressTo(0, 0, Easing.Linear);
				await aqAudioProgressBar.ProgressTo(1, (uint)(player.Duration * 1000), Easing.Linear);
				await aqAudioProgressBar.ProgressTo(0, 200, Easing.CubicOut);
				break;
			case "as":
				await asAudioProgressBar.ProgressTo(0, 0, Easing.Linear);
				await asAudioProgressBar.ProgressTo(1, (uint)(player.Duration * 1000), Easing.Linear);
				await asAudioProgressBar.ProgressTo(0, 200, Easing.CubicOut);
				break;
			case "am":
				await ((ProgressBar)sender).ProgressTo(0, 0, Easing.Linear);
				await ((ProgressBar)sender).ProgressTo(1, (uint)(player.Duration * 1000), Easing.Linear);
				await ((ProgressBar)sender).ProgressTo(0, 200, Easing.CubicOut);
				break;
		}



		//player.Dispose();
	}

	//Not implemented
	List<string> KazakhLetterIgnored(string input)
	{
		List<string> differences = [];
		foreach (string correct in CorrectAnswers)
		{
			char[] kazLetters = ['ә', 'і', 'і', 'ң', 'ғ', 'ү', 'ұ', 'қ', 'ө', 'һ'];
			char[] rusLetters = ['а', 'и', 'ы', 'н', 'г', 'у', 'у', 'к', 'о', 'х'];
			string difference = null;
			if (input.Length == correct.Length)
			{
				for (int i = 0; i < input.Length; i++)
				{
					if (input[i] == correct[i])
						difference += ' ';
					else if (rusLetters.Contains(input[i]) && kazLetters.Contains(correct[i]))
						difference += '#';
					else
					{
						differences.Add(String.Empty);
						break;
					}
				}
				differences.Add(difference);
			}
			else
				differences.Add(string.Empty);
		}
		return differences;
	}

	//Not implemented
	string GetClosestTypo(List<string> differences, bool isKLI)
	{
		string leastDifferent = differences[0];
		foreach (string difference in differences[1..])
			if (leastDifferent.Count(x => x == '#') > difference.Count(x => x == '#'))
				leastDifferent = difference;
		return leastDifferent;
	}
}

class MessageSpan(string text, bool isBold)
{
	public string Text = text;
	public bool IsBold = isBold;
}

public class OpenAnswerFeedback : ValueChangedMessage<FeedbackMessage>
{
	public OpenAnswerFeedback(FeedbackMessage value) : base(value) { }
}

public class StartRenderNextTask : ValueChangedMessage<bool>
{
	public StartRenderNextTask(bool value) : base(value) { }
}

public class RedirectToResultsPage : ValueChangedMessage<LessonResults>
{
	public RedirectToResultsPage(LessonResults value) : base(value) { }
}

public class FeedbackMessage(byte feedbackType, string inputAnswer, string correctAnswer)
{
	/*
	FeedbackType values:
		0   —	Correct
		1   —	Incorrect
		2   —	Half correct (typos)
		3	—	Half correct (Kazakh letters)
		...
		254	 —	Half correct (other reason)
	*/

	public byte FeedbackType = feedbackType; 
	public string InputAnswer = inputAnswer; 
	public string correctAnswer = correctAnswer;
}

public class LessonResults(float successRate, int mistakes, int streak, int xp, int asyq)
{
	public float SuccessRate = successRate;
	public int Mistakes = mistakes;
	public int Streak = streak;
	public int XP = xp;
	public int Asyq = asyq;
}