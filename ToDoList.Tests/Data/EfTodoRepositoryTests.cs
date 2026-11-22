using Microsoft.EntityFrameworkCore;
using ToDoList.Data.Entities;
using ToDoList.Models;
using ToDoList.Tests.Helpers;

namespace ToDoList.Tests.Data;

[TestClass]
[TestCategory("Data")]
public sealed class EfTodoRepositoryTests
{
    private SqliteTestDatabase database = null!;
    private TodoState initial = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        database = await SqliteTestDatabase.CreateAsync();
        initial = TodoFixtures.CreateTree();
    }

    [TestCleanup]
    public async Task CleanupAsync() => await database.DisposeAsync();

    [TestMethod]
    public async Task SaveAsync_NewCategory_InsertsCategory()
    {
        var category = initial.Categories.Single();

        await database.Repository.SaveAsync(new([category], []));

        await using var context = database.CreateContext();
        Assert.AreEqual(category.Name, (await context.Categories.SingleAsync()).Name);
    }

    [TestMethod]
    public async Task SaveAsync_NewTask_InsertsTask()
    {
        var task = new TodoTask(Guid.NewGuid(), "Новая задача", false, null, []);

        await database.Repository.SaveAsync(new([], [task]));

        await using var context = database.CreateContext();
        Assert.AreEqual(task.Title, (await context.Tasks.SingleAsync()).Title);
    }

    [TestMethod]
    public async Task LoadAsync_ReturnsCategories()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        CollectionAssert.AreEqual(initial.Categories.ToArray(), state.Categories.ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_ReturnsOnlyRootTasksAtTopLevel()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        CollectionAssert.AreEquivalent(initial.Tasks.Select(t => t.Id).ToArray(), state.Tasks.Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_PreservesRootOrder()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        CollectionAssert.AreEqual(initial.Tasks.Select(t => t.Id).ToArray(), state.Tasks.Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_PreservesTitle()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        Assert.AreEqual(initial.Tasks[0].Title, state.Tasks[0].Title);
    }

    [TestMethod]
    public async Task LoadAsync_ReconstructsNestedChildren()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        Assert.AreEqual(initial.Tasks[0].Children[0].Children[0].Id, state.Tasks[0].Children.Single().Children.Single().Id);
    }

    [TestMethod]
    public async Task LoadAsync_PreservesTaskCategory()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        Assert.AreEqual(initial.Categories[0].Id, state.Tasks[0].CategoryId);
    }

    [TestMethod]
    public async Task LoadAsync_PreservesCompletionState()
    {
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        Assert.IsTrue(state.Tasks[0].Children[0].IsCompleted);
    }

    [TestMethod]
    public async Task LoadAsync_PreservesSiblingOrder()
    {
        var siblings = new TodoTask[] {
            new(Guid.NewGuid(), "Первая", false, null, []),
            new(Guid.NewGuid(), "Вторая", false, null, []) };
        initial = initial with { Tasks = [initial.Tasks[0] with { Children = siblings }] };
        await SeedAsync();

        var state = await database.Repository.LoadAsync();

        CollectionAssert.AreEqual(siblings.Select(t => t.Id).ToArray(), state.Tasks[0].Children.Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public async Task SaveAsync_ChangedTitle_UpdatesTitle()
    {
        await SeedAsync();
        var updated = initial with { Tasks = [initial.Tasks[0] with { Title = "Обновлено" }, initial.Tasks[1]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.AreEqual("Обновлено", (await context.Tasks.SingleAsync(t => t.Id == initial.Tasks[0].Id)).Title);
    }

    [TestMethod]
    public async Task SaveAsync_ChangedCompletion_UpdatesCompletion()
    {
        await SeedAsync();
        var updated = initial with { Tasks = [initial.Tasks[0] with { IsCompleted = true }, initial.Tasks[1]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.IsTrue((await context.Tasks.SingleAsync(t => t.Id == initial.Tasks[0].Id)).IsCompleted);
    }

    [TestMethod]
    public async Task SaveAsync_ChangedCategory_UpdatesCategoryReference()
    {
        var category = new Category(Guid.NewGuid(), "Личное");
        initial = initial with { Categories = [initial.Categories[0], category] };
        await SeedAsync();
        var updated = initial with { Tasks = [initial.Tasks[0] with { CategoryId = category.Id }, initial.Tasks[1]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.AreEqual(category.Id, (await context.Tasks.SingleAsync(t => t.Id == initial.Tasks[0].Id)).CategoryId);
    }

    [TestMethod]
    public async Task SaveAsync_ClearedCategory_RemovesCategoryReference()
    {
        await SeedAsync();
        var updated = initial with { Tasks = [initial.Tasks[0] with { CategoryId = null }, initial.Tasks[1]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.IsNull((await context.Tasks.SingleAsync(t => t.Id == initial.Tasks[0].Id)).CategoryId);
    }

    [TestMethod]
    public async Task SaveAsync_ReparentedChild_UpdatesParentReference()
    {
        await SeedAsync();
        var child = initial.Tasks[0].Children[0];
        var updated = initial with { Tasks = [initial.Tasks[0] with { Children = [] }, initial.Tasks[1] with { Children = [child] }] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.AreEqual(initial.Tasks[1].Id, (await context.Tasks.SingleAsync(t => t.Id == child.Id)).ParentId);
    }

    [TestMethod]
    public async Task SaveAsync_MissingTask_DeletesThatTask()
    {
        await SeedAsync();
        var updated = initial with { Tasks = [initial.Tasks[0]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.IsFalse(await context.Tasks.AnyAsync(t => t.Id == initial.Tasks[1].Id));
    }

    [TestMethod]
    public async Task SaveAsync_RemovedParent_DeletesDescendants()
    {
        await SeedAsync();
        var descendantIds = new List<Guid> { initial.Tasks[0].Children[0].Id, initial.Tasks[0].Children[0].Children[0].Id };
        var updated = initial with { Tasks = [initial.Tasks[1]] };

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.IsFalse(await context.Tasks.AnyAsync(t => descendantIds.Contains(t.Id)));
    }

    [TestMethod]
    public async Task SaveAsync_RemovedCategory_DeletesCategory()
    {
        await SeedAsync();
        TodoTask Clear(TodoTask task) => task with { CategoryId = null, Children = task.Children.Select(Clear).ToArray() };
        var updated = new TodoState([], initial.Tasks.Select(Clear).ToArray());

        await database.Repository.SaveAsync(updated);

        await using var context = database.CreateContext();
        Assert.AreEqual(0, await context.Categories.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_EmptySnapshot_DeletesAllTasks()
    {
        await SeedAsync();

        await database.Repository.SaveAsync(new([], []));

        await using var context = database.CreateContext();
        Assert.AreEqual(0, await context.Tasks.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_EmptySnapshot_DeletesAllCategories()
    {
        await SeedAsync();

        await database.Repository.SaveAsync(new([], []));

        await using var context = database.CreateContext();
        Assert.AreEqual(0, await context.Categories.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_InsertFails_RollsBackEntireTransaction()
    {
        await SeedAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_write BEFORE INSERT ON Tasks WHEN NEW.Title = 'FAIL' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        }

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            database.Repository.SaveAsync(new([], [new(Guid.NewGuid(), "FAIL", false, null, [])])));

        TodoFixtures.AssertStateEqual(initial, await database.Repository.LoadAsync());
    }

    [TestMethod]
    public async Task SaveAsync_UnknownCategory_ThrowsArgumentException()
    {
        await SeedAsync();
        var invalid = new TodoState([], [new(Guid.NewGuid(), "Задача", false, Guid.NewGuid(), [])]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => database.Repository.SaveAsync(invalid));
    }

    [TestMethod]
    public async Task SaveAsync_UnknownCategory_PreservesExistingData()
    {
        await SeedAsync();
        var invalid = new TodoState([], [new(Guid.NewGuid(), "Задача", false, Guid.NewGuid(), [])]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => database.Repository.SaveAsync(invalid));

        TodoFixtures.AssertStateEqual(initial, await database.Repository.LoadAsync());
    }

    // Arrange through EF directly, so LoadAsync/SaveAsync is not used to seed its own tests.
    private async Task SeedAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();
        context.Categories.AddRange(initial.Categories.Select((c, index) =>
            new CategoryEntity { Id = c.Id, Name = c.Name, Position = index }));
        void AddTasks(IReadOnlyList<TodoTask> tasks, Guid? parentId)
        {
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                context.Tasks.Add(new TaskEntity { Id = task.Id, Title = task.Title,
                    IsCompleted = task.IsCompleted, CategoryId = task.CategoryId, ParentId = parentId, Position = i });
                AddTasks(task.Children, task.Id);
            }
        }
        AddTasks(initial.Tasks, null);
        await context.SaveChangesAsync();
    }
}
