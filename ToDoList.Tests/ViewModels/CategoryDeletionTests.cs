using ToDoList.Core;
using ToDoList.Data;
using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("ViewModels")]
public sealed class CategoryDeletionTests
{
    [TestMethod]
    public async Task DeleteCategory_EmptyCategory_RemovesItAndAllowsRecreatingName()
    {
        var repository = new InMemoryTodoRepository();
        var vm = new MainViewModel(new TodoService(repository));
        await vm.LoadCommand.ExecuteAsync();
        vm.NewCategoryName = "Учёба";
        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.IsEmpty(vm.CategoryItems);
        Assert.HasCount(1, vm.Categories);
        Assert.AreEqual(Category.None, vm.Categories.Single());
        Assert.AreEqual(Category.None, vm.SelectedCategory);
        Assert.IsEmpty((await repository.LoadAsync()).Categories);
        vm.NewCategoryName = "Учёба";
        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);
        Assert.HasCount(1, vm.CategoryItems);
        Assert.IsFalse(vm.HasMessage);
    }

    [TestMethod]
    public async Task DeleteCategory_UsedInNestedTasks_ClearsReferencesAndPreservesTreeInSqlite()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var initial = TodoFixtures.CreateTree();
        var otherCategory = new Category(Guid.NewGuid(), "Личное");
        initial = initial with
        {
            Categories = [initial.Categories.Single(), otherCategory],
            Tasks = [initial.Tasks[0], initial.Tasks[1] with { CategoryId = otherCategory.Id }]
        };
        await database.Repository.SaveAsync(initial);
        var vm = new MainViewModel(new TodoService(database.Repository));
        await vm.LoadCommand.ExecuteAsync();
        var deletedCategory = vm.Categories.Single(c => c.Id == initial.Categories[0].Id);
        vm.SelectedCategory = deletedCategory;
        var parent = vm.Tasks[0];
        await parent.ShowChildCommand.ExecuteAsync();
        var child = parent.Children.Single();
        child.NewChildCategory = deletedCategory;

        await vm.CategoryItems.Single(c => c.Name == deletedCategory.Name).DeleteCommand.ExecuteAsync();

        Assert.IsFalse(vm.HasMessage);
        Assert.HasCount(1, vm.CategoryItems);
        Assert.AreEqual(Category.None, vm.SelectedCategory);
        Assert.AreEqual(Category.None, parent.SelectedCategory);
        Assert.AreEqual(Category.None, parent.NewChildCategory);
        Assert.AreEqual(Category.None, child.NewChildCategory);
        Assert.AreEqual(Category.None, child.Children.Single().SelectedCategory);
        Assert.AreEqual(otherCategory, vm.Tasks[1].SelectedCategory);
        var expectedParent = initial.Tasks[0] with
        {
            CategoryId = null,
            Children = [initial.Tasks[0].Children[0] with
            {
                Children = [initial.Tasks[0].Children[0].Children[0] with { CategoryId = null }]
            }]
        };
        var expected = new TodoState([otherCategory], [expectedParent, initial.Tasks[1]]);
        TodoFixtures.AssertStateEqual(expected, await database.Repository.LoadAsync());

        var restored = new MainViewModel(new TodoService(database.Repository));
        await restored.LoadCommand.ExecuteAsync();
        Assert.AreEqual(Category.None, restored.Tasks[0].SelectedCategory);
        Assert.HasCount(1, restored.CategoryItems);
        Assert.IsFalse(restored.HasMessage);
    }

    [TestMethod]
    public async Task DeleteCategory_StorageFails_ShowsErrorAndSupportsRetry()
    {
        var initial = TodoFixtures.CreateTree();
        var repository = new RetryRepository(initial);
        var vm = new MainViewModel(new TodoService(repository));
        await vm.LoadCommand.ExecuteAsync();

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.IsTrue(vm.HasMessage);
        TodoFixtures.AssertStateEqual(initial, await repository.LoadAsync());
        repository.FailWrites = false;
        await vm.SaveCommand.ExecuteAsync();

        Assert.IsFalse(vm.HasMessage);
        Assert.IsEmpty((await repository.LoadAsync()).Categories);
        Assert.IsNull((await repository.LoadAsync()).Tasks[0].CategoryId);
        Assert.HasCount(2, (await repository.LoadAsync()).Tasks);
    }

    private sealed class RetryRepository(TodoState initial) : ITodoRepository
    {
        private TodoState state = initial;
        public bool FailWrites { get; set; } = true;
        public Task<TodoState> LoadAsync() => Task.FromResult(state);
        public Task SaveAsync(TodoState state)
        {
            if (FailWrites) throw new IOException("Storage unavailable");
            this.state = state;
            return Task.CompletedTask;
        }
    }
}
