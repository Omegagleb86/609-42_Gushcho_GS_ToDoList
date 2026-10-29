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

        // Development configuration: do not commit real credentials to the repository.
        var connection = Environment.GetEnvironmentVariable("TODOLIST_MYSQL_CONNECTION");
        var useMySql = !string.IsNullOrWhiteSpace(connection);
        builder.Services.AddSingleton<ITodoRepository>(_ => useMySql
            ? MySqlTodoRepositoryFactory.Create(connection!)
            : new InMemoryTodoRepository());
        builder.Services.AddSingleton<TodoService>();
        builder.Services.AddSingleton(provider => new MainViewModel(
            provider.GetRequiredService<TodoService>(), useMySql
                ? "Задачи сохраняются в MySQL."
                : "MySQL не настроен. Задачи сохраняются в памяти до закрытия приложения."));
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
