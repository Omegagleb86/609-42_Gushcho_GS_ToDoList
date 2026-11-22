using Microsoft.Extensions.Logging;
using ToDoList.Data;
using ToDoList.Core;
using ToDoList.ViewModels;

namespace ToDoList;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        // Store the database in the platform's writable application data directory.
        builder.Services.AddSingleton<ITodoRepository>(_ =>
            SqliteTodoRepositoryFactory.Create(Path.Combine(FileSystem.AppDataDirectory, "todolist.db3")));
        builder.Services.AddSingleton<ITodoService, TodoService>();
        builder.Services.AddSingleton(provider => new MainViewModel(
            provider.GetRequiredService<ITodoService>(), "Задачи сохраняются на этом устройстве в SQLite."));
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
