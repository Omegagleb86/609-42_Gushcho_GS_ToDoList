using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
[TestCategory("ViewModels")]
public sealed class CreationCommandTests
{
    [TestMethod]
    [DataRow("Task", null)]
    [DataRow("Task", "")]
    [DataRow("Task", " \t ")]
    [DataRow("Category", null)]
    [DataRow("Category", "")]
    [DataRow("Category", " \t ")]
    [DataRow("Child", null)]
    [DataRow("Child", "")]
    [DataRow("Child", " \t ")]
    public async Task AddCommand_BlankInput_IsDisabled(string kind, string? name)
    {
        var target = await CreateTargetAsync(kind);
        target.SetTitle(name);

        var enabled = target.Command.CanExecute(target.Request());

        Assert.IsFalse(enabled);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Category")]
    [DataRow("Child")]
    public async Task AddCommand_BlankInput_DoesNotExecute(string kind)
    {
        var target = await CreateTargetAsync(kind);
        target.SetTitle(" ");

        await target.Command.ExecuteAsync(target.Request());

        Assert.IsEmpty(target.Service.SaveRequests);
    }

    [TestMethod]
    [DataRow("Task", false)]
    [DataRow("Task", true)]
    [DataRow("Category", false)]
    [DataRow("Category", true)]
    [DataRow("Child", false)]
    [DataRow("Child", true)]
    public async Task AddCommand_InvalidParameter_DoesNotExecute(string kind, bool wrongType)
    {
        var target = await CreateTargetAsync(kind);
        target.SetTitle("Корректное название");
        object? parameter = wrongType ? "wrong type" : null;

        await target.Command.ExecuteAsync(parameter);

        Assert.IsEmpty(target.Service.SaveRequests);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Category")]
    [DataRow("Child")]
    public async Task AddCommand_UsesNameFromParameter(string kind)
    {
        var target = await CreateTargetAsync(kind);
        target.SetTitle("Название из поля");
        object parameter = kind == "Category"
            ? new CategoryCreationRequest(" Из параметра ")
            : new TaskCreationRequest(" Из параметра ", Category.None);

        await target.Command.ExecuteAsync(parameter);

        var saved = target.Service.SaveRequests.Single();
        var actualName = kind switch
        {
            "Category" => saved.Categories.Last().Name,
            "Task" => saved.Tasks.Last().Title,
            _ => saved.Tasks[0].Children[0].Children.Last().Title
        };
        Assert.AreEqual("Из параметра", actualName);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Child")]
    public async Task AddCommand_UsesCategoryFromParameter(string kind)
    {
        var target = await CreateTargetAsync(kind);
        var category = target.Service.State.Categories[0];
        target.Vm.SelectedCategory = Category.None;
        target.Vm.Tasks[0].Children[0].NewChildCategory = Category.None;

        await target.Command.ExecuteAsync(new TaskCreationRequest("Задача", category));

        var saved = target.Service.SaveRequests.Single();
        var actual = kind == "Task" ? saved.Tasks.Last() : saved.Tasks[0].Children[0].Children.Last();
        Assert.AreEqual(category.Id, actual.CategoryId);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Category")]
    [DataRow("Child")]
    public async Task AddCommand_AfterSubmission_IsDisabledForClearedInput(string kind)
    {
        var target = await CreateTargetAsync(kind);
        target.SetTitle("Новое название");

        await target.Command.ExecuteAsync(target.Request());

        Assert.IsFalse(target.Command.CanExecute(target.Request()));
    }

    [TestMethod]
    public async Task CategoryNameChanged_NotifiesParameterBindingBeforeCommand()
    {
        var target = await CreateTargetAsync("Category");
        var events = new List<string>();
        target.Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.NewCategoryRequest)) events.Add("parameter");
        };
        target.Command.CanExecuteChanged += (_, _) => events.Add("command");

        target.SetTitle("Учёба");

        CollectionAssert.AreEqual(new[] { "parameter", "command" }, events);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Category")]
    public void AddCommand_BeforeLoad_IsDisabled(string kind)
    {
        var vm = new MainViewModel(new StubTodoService()) { NewTaskTitle = "Задача", NewCategoryName = "Категория" };
        var command = kind == "Task" ? vm.AddTaskCommand : vm.AddCategoryCommand;
        object request = kind == "Task" ? vm.NewTaskRequest : vm.NewCategoryRequest;

        var enabled = command.CanExecute(request);

        Assert.IsFalse(enabled);
    }

    [TestMethod]
    [DataRow("Task")]
    [DataRow("Category")]
    public async Task LoadCommand_EnablesCreationCommand(string kind)
    {
        var vm = new MainViewModel(new StubTodoService()) { NewTaskTitle = "Задача", NewCategoryName = "Категория" };
        var command = kind == "Task" ? vm.AddTaskCommand : vm.AddCategoryCommand;

        await vm.LoadCommand.ExecuteAsync();

        object request = kind == "Task" ? vm.NewTaskRequest : vm.NewCategoryRequest;
        Assert.IsTrue(command.CanExecute(request));
    }

    private static async Task<Target> CreateTargetAsync(string kind)
    {
        var service = new StubTodoService { State = TodoFixtures.CreateTree() };
        var vm = new MainViewModel(service);
        await vm.LoadCommand.ExecuteAsync();
        var child = vm.Tasks[0].Children[0];
        return kind switch
        {
            "Task" => new(vm, service, vm.AddTaskCommand, () => vm.NewTaskRequest, name => vm.NewTaskTitle = name!),
            "Category" => new(vm, service, vm.AddCategoryCommand, () => vm.NewCategoryRequest, name => vm.NewCategoryName = name!),
            _ => new(vm, service, child.AddChildCommand, () => child.NewChildRequest, name => child.NewChildTitle = name!)
        };
    }

    private sealed record Target(MainViewModel Vm, StubTodoService Service, AsyncCommand Command,
        Func<object> Request, Action<string?> SetTitle);
}
