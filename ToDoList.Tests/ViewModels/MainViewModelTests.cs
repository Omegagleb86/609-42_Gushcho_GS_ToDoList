using ToDoList.Core;
using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
[TestCategory("ViewModels")]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task AddTaskCommand_BlankName_DoesNotShowError()
    {
        var (vm, _) = await CreateAsync();
        vm.NewTaskTitle = "   ";

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        Assert.IsFalse(vm.HasMessage);
    }

    [TestMethod]
    public async Task AddCategoryCommand_TrimsCategoryName()
    {
        var (vm, service) = await CreateAsync();
        vm.NewCategoryName = " Учёба ";

        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);

        Assert.AreEqual("Учёба", service.SaveRequests.Single().Categories.Single().Name);
    }

    [TestMethod]
    public async Task AddCategoryCommand_DuplicateIgnoringCase_DoesNotAddCategory()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, service) = await CreateAsync(new([category], []));
        vm.NewCategoryName = "учёба";

        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);

        Assert.HasCount(1, vm.CategoryItems);
        Assert.IsEmpty(service.SaveRequests);
    }

    [TestMethod]
    public async Task AddTaskCommand_TrimsTaskTitle()
    {
        var (vm, service) = await CreateAsync();
        vm.NewTaskTitle = " Проект ";

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        Assert.AreEqual("Проект", service.SaveRequests.Single().Tasks.Single().Title);
    }

    [TestMethod]
    public async Task AddTaskCommand_AssignsSelectedCategory()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, service) = await CreateAsync(new([category], []));
        vm.NewTaskTitle = "Проект";
        vm.SelectedCategory = category;

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        Assert.AreEqual(category.Id, service.SaveRequests.Single().Tasks.Single().CategoryId);
    }

    [TestMethod]
    public async Task AddTaskCommand_ClearsTitleInput()
    {
        var (vm, _) = await CreateAsync();
        vm.NewTaskTitle = "Проект";

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        Assert.AreEqual("", vm.NewTaskTitle);
    }

    [TestMethod]
    public async Task ToggleCommand_ChildCompletion_DoesNotChangeParentStatus()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());

        await vm.Tasks[0].Children[0].ToggleCommand.ExecuteAsync();

        Assert.IsFalse(service.SaveRequests.Single().Tasks[0].IsCompleted);
    }

    [TestMethod]
    public async Task ToggleCommand_MarksTaskCompleted()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());

        await vm.Tasks[0].ToggleCommand.ExecuteAsync();

        Assert.IsTrue(service.SaveRequests.Single().Tasks[0].IsCompleted);
    }

    [TestMethod]
    public async Task ToggleCommand_ToggledTwice_RestoresIncompleteState()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());
        var task = vm.Tasks[0];

        await task.ToggleCommand.ExecuteAsync();
        await task.ToggleCommand.ExecuteAsync();

        Assert.AreEqual("radio_notactive.png", task.StatusIcon);
        Assert.IsFalse(service.State.Tasks[0].IsCompleted);
    }

    [TestMethod]
    public async Task RenameCommand_BlankName_RestoresPreviousName()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, _) = await CreateAsync(initial);
        vm.Tasks[0].Title = " ";

        await vm.Tasks[0].RenameCommand.ExecuteAsync();

        Assert.AreEqual(initial.Tasks[0].Title, vm.Tasks[0].Title);
    }

    [TestMethod]
    public async Task RenameCommand_BlankName_ShowsError()
    {
        var (vm, _) = await CreateAsync(TodoFixtures.CreateTree());
        vm.Tasks[0].Title = " ";

        await vm.Tasks[0].RenameCommand.ExecuteAsync();

        Assert.IsTrue(vm.HasMessage);
    }

    [TestMethod]
    public async Task RenameCommand_BlankName_DoesNotSubmitChanges()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());
        vm.Tasks[0].Title = " ";

        await vm.Tasks[0].RenameCommand.ExecuteAsync();

        Assert.IsEmpty(service.SaveRequests);
    }

    [TestMethod]
    public async Task RenameCommand_ValidName_SubmitsTrimmedTitle()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());
        var parent = vm.Tasks[0];
        parent.Title = " Новый проект ";

        await parent.RenameCommand.ExecuteAsync();

        Assert.AreEqual("Новый проект", service.SaveRequests.Single().Tasks[0].Title);
    }

    [TestMethod]
    public async Task SelectedCategory_Cleared_SubmitsTaskWithoutCategory()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, service) = await CreateAsync(initial);

        vm.Tasks[0].SelectedCategory = Category.None;

        var expectedParent = initial.Tasks[0] with { CategoryId = null };
        TodoFixtures.AssertStateEqual(new(initial.Categories, [expectedParent, initial.Tasks[1]]), service.SaveRequests.Single());
    }

    [TestMethod]
    public async Task DeleteCommand_Child_SubmitsTreeWithoutDeletedBranch()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, service) = await CreateAsync(initial);

        await vm.Tasks[0].Children.Single().DeleteCommand.ExecuteAsync();

        TodoFixtures.AssertStateEqual(new(initial.Categories,
            [initial.Tasks[0] with { Children = [] }, initial.Tasks[1]]), service.State);
    }

    [TestMethod]
    public async Task DeleteCommand_Root_SubmitsTreeWithoutDeletedRoot()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, service) = await CreateAsync(initial);

        await vm.Tasks[0].DeleteCommand.ExecuteAsync();

        TodoFixtures.AssertStateEqual(new(initial.Categories, [initial.Tasks[1]]), service.State);
    }

    [TestMethod]
    public async Task AddTaskCommand_StorageFails_RetainsUnsavedTask()
    {
        var vm = new MainViewModel(new StubTodoService { SaveError = new IOException("Storage unavailable") });
        await vm.LoadCommand.ExecuteAsync();
        vm.NewTaskTitle = "Не потерять";

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        Assert.AreEqual("Не потерять", vm.Tasks.Single().Title);
    }

    [TestMethod]
    public async Task AddTaskCommand_StorageFails_ShowsError()
    {
        var vm = new MainViewModel(new StubTodoService { SaveError = new IOException("Storage unavailable") });
        await vm.LoadCommand.ExecuteAsync();
        vm.NewTaskTitle = "Не потерять";

        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);

        StringAssert.Contains(vm.Message, "Не удалось");
    }

    private static async Task<(MainViewModel Vm, StubTodoService Service)> CreateAsync(TodoState? initial = null)
    {
        var service = new StubTodoService();
        if (initial is not null) service.State = initial;
        var vm = new MainViewModel(service);
        await vm.LoadCommand.ExecuteAsync();
        return (vm, service);
    }
}
