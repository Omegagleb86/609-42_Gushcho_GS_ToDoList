using ToDoList.Data;
using ToDoList.Tests.Helpers;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class SqliteFilePersistenceTests
{
    [TestMethod]
    public async Task Create_NewRepositoryInstance_LoadsDataFromExistingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "todolist-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data", "todolist.db3");
        try
        {
            var state = TodoFixtures.CreateTree();
            var firstInstance = SqliteTodoRepositoryFactory.Create(path);
            await firstInstance.SaveAsync(state);

            var secondInstance = SqliteTodoRepositoryFactory.Create(path);
            var actual = await secondInstance.LoadAsync();

            Assert.IsTrue(File.Exists(path));
            TodoFixtures.AssertStateEqual(state, actual);
        }
        finally
        {
            // Only the directory created by this test is removed.
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
