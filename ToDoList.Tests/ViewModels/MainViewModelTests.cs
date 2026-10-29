using ToDoList.Core;
using ToDoList.Data;
using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("ViewModels")]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task AddTaskCommand_BlankName_DoesNotAddTaskAndShowsError()
    {
        var (vm, repository) = await CreateAsync();
        vm.NewTaskTitle = "   ";

        await vm.AddTaskCommand.ExecuteAsync();

        Assert.IsEmpty(vm.Tasks);
        Assert.IsTrue(vm.HasMessage);
        Assert.IsEmpty((await repository.LoadAsync()).Tasks);
    }

    [TestMethod]
    public async Task AddCategoryCommand_DuplicateIgnoringCase_RejectsDuplicateAndTrimsName()
    {
        var (vm, repository) = await CreateAsync();
        vm.NewCategoryName = " Учёба ";
        await vm.AddCategoryCommand.ExecuteAsync();
        vm.NewCategoryName = "учёба";

        await vm.AddCategoryCommand.ExecuteAsync();

        Assert.HasCount(2, vm.Categories); // Includes the UI-only "Без категории".
        Assert.AreEqual("Учёба", vm.Categories[1].Name);
        Assert.IsTrue(vm.HasMessage);
        Assert.HasCount(1, (await repository.LoadAsync()).Categories);
    }

    [TestMethod]
    public async Task AddTaskCommand_SelectedCategory_CreatesTrimmedTaskWithCategory()
    {
        var (vm, repository) = await CreateAsync();
        vm.NewCategoryName = "Учёба";
        await vm.AddCategoryCommand.ExecuteAsync();
        vm.NewTaskTitle = " Проект ";

        await vm.AddTaskCommand.ExecuteAsync();

        var task = vm.Tasks.Single();
        Assert.AreEqual("Проект", task.Title);
        Assert.AreEqual(vm.SelectedCategory, task.SelectedCategory);
        Assert.AreEqual(vm.SelectedCategory!.Id, (await repository.LoadAsync()).Tasks.Single().CategoryId);
        Assert.AreEqual("", vm.NewTaskTitle);
    }

    [TestMethod]
    public async Task ToggleCommand_ChildCompletion_DoesNotChangeParentStatus()
    {
        var (vm, repository) = await CreateAsync();
        vm.NewTaskTitle = "Родитель";
        await vm.AddTaskCommand.ExecuteAsync();
        var parent = vm.Tasks.Single();
        await parent.ShowChildCommand.ExecuteAsync();
        parent.NewChildTitle = "Экран";
        await parent.AddChildCommand.ExecuteAsync();
        var child = parent.Children.Single();
        await child.ShowChildCommand.ExecuteAsync();
        child.NewChildTitle = "Кнопки";
        await child.AddChildCommand.ExecuteAsync();

        await child.ToggleCommand.ExecuteAsync();

        Assert.AreEqual("radio_active.png", child.StatusIcon);
        Assert.AreEqual("radio_notactive.png", parent.StatusIcon);
        var storedParent = (await repository.LoadAsync()).Tasks.Single();
        Assert.IsFalse(storedParent.IsCompleted);
        Assert.IsTrue(storedParent.Children.Single().IsCompleted);
        Assert.AreEqual("Кнопки", storedParent.Children.Single().Children.Single().Title);
    }

    [TestMethod]
    public async Task ToggleCommand_ToggledTwice_RestoresIncompleteState()
    {
        var (vm, repository) = await CreateAsync(TodoFixtures.CreateTree());
        var task = vm.Tasks[0];

        await task.ToggleCommand.ExecuteAsync();
        await task.ToggleCommand.ExecuteAsync();

        Assert.AreEqual("radio_notactive.png", task.StatusIcon);
        Assert.IsFalse((await repository.LoadAsync()).Tasks[0].IsCompleted);
    }

    [TestMethod]
    public async Task RenameCommand_BlankName_RestoresPreviousName()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, repository) = await CreateAsync(initial);
        var task = vm.Tasks[0];
        task.Title = " ";

        await task.RenameCommand.ExecuteAsync();

        Assert.AreEqual(initial.Tasks[0].Title, task.Title);
        Assert.IsTrue(vm.HasMessage);
        TodoFixtures.AssertStateEqual(initial, await repository.LoadAsync());
    }

    [TestMethod]
    public async Task SaveCommand_RenameAndClearCategory_ReloadsUpdatedTree()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, repository) = await CreateAsync(initial);
        var parent = vm.Tasks[0];
        parent.Title = " Новый проект ";
        await parent.RenameCommand.ExecuteAsync();
        parent.SelectedCategory = Category.None;
        await vm.SaveCommand.ExecuteAsync();
        var restored = new MainViewModel(new TodoService(repository));

        await restored.LoadCommand.ExecuteAsync();

        var expectedParent = initial.Tasks[0] with { Title = "Новый проект", CategoryId = null };
        TodoFixtures.AssertStateEqual(new(initial.Categories, [expectedParent, initial.Tasks[1]]),
            new(restored.Categories.Where(c => c.Id != Guid.Empty).ToArray(), restored.Tasks.Select(t => t.ToModel()).ToArray()));
        Assert.AreEqual(Category.None, restored.Tasks[0].SelectedCategory);
    }

    [TestMethod]
    public async Task DeleteCommand_Child_RemovesAllDescendantsAndPreservesParent()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, repository) = await CreateAsync(initial);

        await vm.Tasks[0].Children.Single().DeleteCommand.ExecuteAsync();

        Assert.IsEmpty(vm.Tasks[0].Children);
        TodoFixtures.AssertStateEqual(new(initial.Categories,
            [initial.Tasks[0] with { Children = [] }, initial.Tasks[1]]), await repository.LoadAsync());
    }

    [TestMethod]
    public async Task DeleteCommand_Root_RemovesEntireTreeAndPreservesOtherTasks()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, repository) = await CreateAsync(initial);

        await vm.Tasks[0].DeleteCommand.ExecuteAsync();

        Assert.HasCount(1, vm.Tasks);
        TodoFixtures.AssertStateEqual(new(initial.Categories, [initial.Tasks[1]]), await repository.LoadAsync());
    }

    [TestMethod]
    public async Task AddTaskCommand_StorageFails_RetainsUnsavedTaskAndShowsError()
    {
        var vm = new MainViewModel(new TodoService(new FailingRepository()));
        await vm.LoadCommand.ExecuteAsync();
        vm.NewTaskTitle = "Не потерять";

        await vm.AddTaskCommand.ExecuteAsync();

        Assert.AreEqual("Не потерять", vm.Tasks.Single().Title);
        StringAssert.Contains(vm.Message, "Не удалось");
    }

    private static async Task<(MainViewModel Vm, InMemoryTodoRepository Repository)> CreateAsync(TodoState? initial = null)
    {
        var repository = new InMemoryTodoRepository();
        if (initial is not null) await repository.SaveAsync(initial);
        var vm = new MainViewModel(new TodoService(repository));
        await vm.LoadCommand.ExecuteAsync();
        return (vm, repository);
    }

    private sealed class FailingRepository : ITodoRepository
    {
        public Task<TodoState> LoadAsync() => Task.FromResult(new TodoState([], []));
        public Task SaveAsync(TodoState state) => throw new IOException("Storage unavailable");
    }
}
