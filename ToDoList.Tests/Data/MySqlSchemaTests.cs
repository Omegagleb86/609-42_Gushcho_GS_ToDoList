using Microsoft.EntityFrameworkCore;
using ToDoList.Data;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class MySqlSchemaTests
{
    [TestMethod]
    public void GenerateCreateScript_MySqlProvider_CreatesTaskTableWithParentForeignKey()
    {
        // Schema generation never opens a connection to a server.
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseMySql("Server=localhost;Database=todolist_schema_check;User ID=test;Password=unused",
                new MySqlServerVersion(new Version(8, 0, 0))).Options;
        using var context = new TodoDbContext(options);

        var script = context.Database.GenerateCreateScript();

        StringAssert.Contains(script, "CREATE TABLE `Tasks`");
        StringAssert.Contains(script, "FOREIGN KEY (`ParentId`)");
    }
}
