using Microsoft.EntityFrameworkCore;

namespace ToDoList.Data;

public static class MySqlTodoRepositoryFactory
{
    // Explicit version avoids a network connection while constructing the app.
    public static EfTodoRepository Create(string connectionString, Version? serverVersion = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Не задана строка подключения MySQL.");
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(serverVersion ?? new Version(8, 0, 0)))
            .Options;
        return new EfTodoRepository(() => new TodoDbContext(options));
    }
}
