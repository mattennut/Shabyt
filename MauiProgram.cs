using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using The49.Maui.BottomSheet;

namespace QzLangProg
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit()
				.UseMauiCommunityToolkitMarkup()
				.UseBottomSheet()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("Montserrat-Black.ttf",	"MontserratBlack");
					fonts.AddFont("Satoshi-Bold.otf",		"SatoshiBold");
					fonts.AddFont("Geologica-Light.ttf",	"GeologicaLight");
					fonts.AddFont("Geologica-Bold.ttf",		"GeologicaBold");
				});

			builder.Services.AddSingleton<TaskPage>();
			builder.Services.AddSingleton(AudioManager.Current);
			builder.Services.AddTransient<MainPage>();
			builder.Services.AddTransient<TaskPage>();

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}