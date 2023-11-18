using Microsoft.Extensions.Logging;

namespace QzLangProg
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("Montserrat-Medium.ttf", "MontserratMedium");
					fonts.AddFont("Montserrat-Black.ttf",  "MontserratBlack");
				});

#if DEBUG
		builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}