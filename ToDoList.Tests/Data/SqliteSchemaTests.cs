using Microsoft.EntityFrameworkCore;
using ToDoList.Data;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class SqliteSchemaTests
{
    [TestMethod]
    public void GenerateCreateScript_CreatesTaskTable()
    {
        using var context = CreateContext();

        var script = context.Database.GenerateCreateScript();

        StringAssert.Contains(script, "CREATE TABLE \"Tasks\"");
    }

    [TestMethod]
    public void GenerateCreateScript_CreatesParentForeignKey()
    {
        using var context = CreateContext();

        var script = context.Database.GenerateCreateScript();

        StringAssert.Contains(script, "FOREIGN KEY (\"ParentId\")");
    }

    private static TodoDbContext CreateContext() => new(
        new DbContextOptionsBuilder<TodoDbContext>().UseSqlite("Data Source=:memory:").Options);
}
