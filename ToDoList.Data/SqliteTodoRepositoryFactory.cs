using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ToDoList.Data;

public static class SqliteTodoRepositoryFactory
{
    public static EfTodoRepository Create(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            // Each operation owns and closes its connection, including the native file handle.
            Pooling = false
        }.ToString();
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseSqlite(connectionString).Options;
        return new EfTodoRepository(() => new TodoDbContext(options));
    }
}
