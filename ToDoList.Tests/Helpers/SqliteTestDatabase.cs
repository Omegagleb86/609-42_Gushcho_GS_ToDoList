using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToDoList.Data;

namespace ToDoList.Tests.Helpers;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
    private readonly DbContextOptions<TodoDbContext> options;
    public EfTodoRepository Repository { get; }

    private SqliteTestDatabase()
    {
        options = new DbContextOptionsBuilder<TodoDbContext>().UseSqlite(connection).Options;
        Repository = new EfTodoRepository(CreateContext);
    }

    public TodoDbContext CreateContext() => new(options);

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var database = new SqliteTestDatabase();
        try
        {
            await database.connection.OpenAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
