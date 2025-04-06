using GoogleGson;
using JetBrains.Annotations;
using Kotlin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Android.Content.Res.Resources;
using static Android.Graphics.ImageDecoder;

namespace QzLangProg
{
	public static class FileReader
	{
		readonly public static List<string> Lines = []; //List of all of the file lines; is filled in GetLines()
		public static int TaskAmount = 0;               //The number of tasks (excluding lesson materials)

		//Array of supported task and lesson material types
		readonly static string[] TaskTypes = ["tr", "fg", "ft", "aq", "am", "sw", "sp", "as", "tf"];

		//Code for FileReader, the file reading and writing methods. Updated with each major modification
		readonly static string FRVersionCode = "L01";

		//OBSOLETE
		//Progress; the next lesson to be completed
		//		public static (int UnitID, int LessonID, string Course, int XP, int Asyqs) ProgressObsolete;

		//Progress; the next lesson to be completed
		//	!!!	Title within NextLesson is negligible
		public static (Dictionary<string, Lesson> NextLesson, int XP, int Asyqs, Theme ThemeOptions) Progress;

		//List of units containing all of the lessons and information thereof
		public readonly static List<Unit> Units =
		[
			new Unit (1, "Научитесь приветствоваться, представляться и указывать людей", [
				new Lesson(1, 1, "rq", "Приветствование"),
				new Lesson(1, 2, "rq", "Представляться"),
				new Lesson(1, 3, "rq", "Он/она/оно")
			]),
			new Unit(2, "Первые основы казахской грамматики. Вы научитесь рассказывать кто ваши родственники", [
				new Lesson(2, 1, "rq", "Мягкие и твердые гласные"),
				new Lesson(2, 2, "rq", "Притяжательные: Часть I"),
				new Lesson(2, 3, "rq", "Притяжательные: Часть II"),
				new Lesson(2, 4, "rq", "Профессии")
			]),
			new Unit(3, "Научитесь разговаривать вежливо, а также описывать вещи", [
				new Lesson(3, 1, "rq", "Вежливое приветствие"),
				new Lesson(3, 2, "rq", "Вежливые притяжательные"),
				new Lesson(3, 3, "rq", "Дом и прилагательные")
			])
		];

		//Currently open page; is used in TaskPage to load the lesson information
		public static Lesson CurrentlyOpen;

		//Used to call the methods on MainPage
		public static MainPage MainPageSample;

		public static (float SuccessRate, int Mistakes, int Streak, int XP, int Asyqs) LessonResults;


		//Get all lines (lesson code, title, tasks with lesson materials) from a .qlp file
		public static async void GetLines(int UnitID, int LessonID, string Course)
		{
			Lines.Clear();
			TaskAmount = 0;
			string Line;

			try
			{
				using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync($"Lessons/{Course}{UnitID}-{LessonID}.qlp");
				using StreamReader reader = new (fileStream);
				
				while ((Line = reader.ReadLine()) != null)
				{
					Lines.Add(Line);
					if (TaskTypes.Contains(Line[..2])) TaskAmount++;
				}
			}
			catch 
			{
				Lines.Add("GR|You are not supposed to see this!");
			}
		}


		//Get title of the lesson. This task is used in MainPage
		public static async Task<(string lCode, string tit)> GetTitle(int UnitID, int LessonID, string Course)
		{
			string Line;
			string LessonCode, Title;

			try
			{
				using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync($"Lessons/{Course}{UnitID}-{LessonID}.qlp");
				using StreamReader reader = new(fileStream);

				while ((Line = reader.ReadLine()) != null)
				{
					LessonCode = Line[..2];
					Title = Line[3..];
					return (LessonCode, Title);
				}
			}
			catch { }

			return ("NO", "Error, you are not supposed to see this");
		}


		//Save current progress
		//	!!!	Converting UnitID, LessonID, and Course to Lesson class-type is redundant

		//TODO:	Make ThemeOptions optional
		public static void WriteProgress(int UnitID, int LessonID, string Course, int XP, int Asyqs)
		{
			var path = FileSystem.Current.AppDataDirectory;
			var fullPath = Path.Combine(path, "Progress/LessonProgress.qlpp");

			//char themeCode = Progress.ThemeOptions.GetCode();
			char themeCode = 'a';

			//$"{UnitID}:{LessonID}:{Course}|{XP}:{Asyqs}"
			string progressString = $"{FRVersionCode}:{XP}:{Asyqs}:{themeCode}";
			List<string> writeLines = [];

			string[] readLines = File.ReadAllText(fullPath).Split(Environment.NewLine);
			bool isFound = false;

			if (readLines.Length > 1)
			{
				for (int i = 1; i < readLines.Length; i++)
				{
					if (readLines[i][..2] == Course)
					{
						isFound = true;
						writeLines.Add($"{Course}:{UnitID}:{LessonID}");
					}
					else
						writeLines.Add(readLines[i]);
				}
			}

			if (!isFound)
				writeLines.Add($"{Course}:{UnitID}:{LessonID}");

			foreach (string writeLine in writeLines)
			{
				progressString += $"\n{writeLine}";
			}

			File.WriteAllText(fullPath, progressString);
		}
		public static void WriteProgress((int UnitID, int LessonID) lesson, string Course, int XP, int Asyqs)
		{
			WriteProgress(lesson.UnitID, lesson.LessonID, Course, XP, Asyqs);
		}

		public static void WriteEmptyProgress()
		{
			var path = FileSystem.Current.AppDataDirectory;
			var fullPath = Path.Combine(path, "Progress/LessonProgress.qlpp");

			string progressString = $"{FRVersionCode}:0:100:a\nrq:1:1";

			var directoryPath = Path.Combine(path, "Progress");
			if (!Directory.Exists(directoryPath))
				Directory.CreateDirectory(directoryPath);

			File.WriteAllText(fullPath, progressString);
		}

		public static void WriteProgress(Theme ThemeOptions)
		{
			var path = FileSystem.Current.AppDataDirectory;
			var fullPath = Path.Combine(path, "Progress/LessonProgress.qlpp");

			//$"{UnitID}:{LessonID}:{Course}|{XP}:{Asyqs}"
			
			List<string> writeLines = [];

			string[] readLines = File.ReadAllText(fullPath).Split(Environment.NewLine);
			string[] progressDetails = readLines[0].Split(':');


			string progressString = $"{progressDetails[0]}:{progressDetails[1]}:{progressDetails[2]}:{ThemeOptions.GetCode()}";

			if (readLines.Length > 1)
			{
				for (int i = 1; i < readLines.Length; i++)
				{
					writeLines.Add(readLines[i]);
				}
			}

			foreach (string writeLine in writeLines)
			{
				progressString += $"\n{writeLine}";
			}

			File.WriteAllText(fullPath, progressString);
		}

		//TODO:	Change save location
		//	Supposedly, whenever app gets updated, so do all of the files, resulting in restoration of progress
		//TODO:	Encrypt the progress files
		public static (Dictionary<string, Lesson> NextLessons, int XP, int Asyq, Theme ThemeOptions) GetProgress()
		{
			var path = FileSystem.Current.AppDataDirectory;
			var fullPath = Path.Combine(path, "Progress/LessonProgress.qlpp");

			/*bool progressFileExists = CheckFileExistence("Progress/LessonProgress.qlpp").Result;
			if (!progressFileExists)
			{
				WriteEmptyProgress();
			}*/

			var directoryPath = Path.Combine(path, "Progress");
			if (!Directory.Exists(directoryPath))
				Directory.CreateDirectory(directoryPath);
			if (!File.Exists(fullPath))
				WriteEmptyProgress();

			string progressString = File.ReadAllText(fullPath);

			Dictionary<string, Lesson> nextLessons = [];

			try
			{
				string[] lines = progressString.Split(Environment.NewLine);
				string[] progressDetails = [];
				bool isValid = false;

				string[] stats = ["0", "0", "a"];       //XP, Asyqs
				if (lines.Length > 0)
					stats = lines[0].Split(":")[1..];

				if (lines.Length > 1)
				{
					for (int i = 1; i < lines.Length; i++)
					{
						progressDetails = lines[i].Split(':');
					}

					string Course = progressDetails[0];
					int UnitID;
					int LessonID;
					bool isUnitValid = int.TryParse(progressDetails[1], out UnitID);
					bool isLessonValid = int.TryParse(progressDetails[2], out LessonID);
					isValid = isUnitValid && isLessonValid;

					if (isValid)
					{
						Lesson nextLesson = new(UnitID, LessonID, Course, "");
						nextLessons.Add(Course, nextLesson);
					}
				}

				if (nextLessons.Count > 0)
				{
					Progress = (nextLessons, int.Parse(stats[0]), int.Parse(stats[1]), Theme.ReadCode(stats[2]));
					return Progress;
				}
			}
			catch { }

			return (new Dictionary<string, Lesson>{ { "rq", new Lesson(-1, -2, progressString, "")} }, 0, 100, new Theme(true, true));
		}
		//TODO:	Optimise this
		public static Theme GetTheme()
		{
			return GetProgress().ThemeOptions;
		}

		public static async Task<bool> CheckFileExistence(string path) => await FileSystem.AppPackageFileExistsAsync("Progress/LessonProgress.qlpp");


		public static string GetRawProgress()
		{
			var path = FileSystem.Current.AppDataDirectory;
			var fullPath = Path.Combine(path, "Progress/LessonProgress.qlpp");

			return File.ReadAllText(fullPath);
		}
	}

	public class Unit(int unit, string unitDescription, List<Lesson> lessons)
	{
		public int UnitID = unit;
		public string UnitDescription = unitDescription;
		public List<Lesson> Lessons = lessons;
	}

	public class Theme(bool isAuto, bool isLight)
	{
		public bool IsAuto = isAuto;
		public bool IsLight = isLight;

		public char GetCode()
		{
			if (IsAuto) return 'a';
			else if (IsLight) return 'l';
			return 'd';
		}

		public static Theme ReadCode(char code)
		{
			return code switch {
				'a' => new Theme(true, true),
				'l' => new Theme(false, true),
				'd' => new Theme(false, false),
				_	=> new Theme(true, false)
			};
		}

		public static Theme ReadCode(string code)
		{
			return ReadCode(code[0]);
		}
	}
}
