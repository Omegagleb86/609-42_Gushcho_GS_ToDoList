using Microsoft.EntityFrameworkCore;
using ToDoList.Data;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class SqliteSchemaTests
{
    [TestMethod]
    public void GenerateCreateScript_SqliteProvider_CreatesTaskTableWithParentForeignKey()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var context = new TodoDbContext(options);

        var script = context.Database.GenerateCreateScript();

        StringAssert.Contains(script, "CREATE TABLE \"Tasks\"");
        StringAssert.Contains(script, "FOREIGN KEY (\"ParentId\")");
    }
}
