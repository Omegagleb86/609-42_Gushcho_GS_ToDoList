using Microsoft.EntityFrameworkCore;
using ToDoList.Models;
using ToDoList.Tests.Helpers;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class EfTodoRepositoryTests
{
    [TestMethod]
    public async Task SaveAndLoad_NestedTasks_PreservesAllFieldsAndOrder()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var expected = TodoFixtures.CreateTree();

        await database.Repository.SaveAsync(expected);
        var actual = await database.Repository.LoadAsync();

        TodoFixtures.AssertStateEqual(expected, actual);
    }

    [TestMethod]
    public async Task SaveAsync_UpdatedSnapshot_RemovesMissingRowsAndReparentsChild()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var initial = TodoFixtures.CreateTree();
        await database.Repository.SaveAsync(initial);
        var updated = new TodoState([], [
            new(initial.Tasks[1].Id, "Обновлено", true, null, [
                new(initial.Tasks[0].Children[0].Id, "Перенесено", false, null, [])])]);

        await database.Repository.SaveAsync(updated);
        var actual = await database.Repository.LoadAsync();

        TodoFixtures.AssertStateEqual(updated, actual);
        await using var context = database.CreateContext();
        Assert.AreEqual(0, await context.Categories.CountAsync());
        Assert.AreEqual(2, await context.Tasks.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_InsertFails_RollsBackEntireTransaction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var initial = TodoFixtures.CreateTree();
        await database.Repository.SaveAsync(initial);
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_write BEFORE INSERT ON Tasks WHEN NEW.Title = 'FAIL' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        }
        var rejected = new TodoState([], [new(Guid.NewGuid(), "FAIL", false, null, [])]);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => database.Repository.SaveAsync(rejected));

        TodoFixtures.AssertStateEqual(initial, await database.Repository.LoadAsync());
    }

    [TestMethod]
    public async Task SaveAsync_UnknownCategory_RejectsSnapshotWithoutChangingDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var initial = TodoFixtures.CreateTree();
        await database.Repository.SaveAsync(initial);
        var invalid = new TodoState([], [new(Guid.NewGuid(), "Нет категории", false, Guid.NewGuid(), [])]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => database.Repository.SaveAsync(invalid));

        TodoFixtures.AssertStateEqual(initial, await database.Repository.LoadAsync());
    }

    [TestMethod]
    public async Task SaveAsync_EmptySnapshot_DeletesAllTasksAndCategories()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await database.Repository.SaveAsync(TodoFixtures.CreateTree());
        var empty = new TodoState([], []);

        await database.Repository.SaveAsync(empty);

        TodoFixtures.AssertStateEqual(empty, await database.Repository.LoadAsync());
    }
}
