using ToDoList.Core;
using ToDoList.Data;
using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("ViewModels")]
public sealed class CreationCommandTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t ")]
    public async Task AddCommands_BlankInput_AreDisabledAndDoNotExecute(string? name)
    {
        var repository = new InMemoryTodoRepository();
        var initial = TodoFixtures.CreateTree();
        await repository.SaveAsync(initial);
        var vm = new MainViewModel(new TodoService(repository));
        await vm.LoadCommand.ExecuteAsync();
        var parent = vm.Tasks[0];
        vm.NewTaskTitle = name!;
        vm.NewCategoryName = name!;
        parent.NewChildTitle = name!;

        Assert.IsFalse(vm.AddTaskCommand.CanExecute(vm.NewTaskRequest));
        Assert.IsFalse(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));
        Assert.IsFalse(parent.AddChildCommand.CanExecute(parent.NewChildRequest));
        await vm.AddTaskCommand.ExecuteAsync(vm.NewTaskRequest);
        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);
        await parent.AddChildCommand.ExecuteAsync(parent.NewChildRequest);

        Assert.IsFalse(vm.HasMessage);
        TodoFixtures.AssertStateEqual(initial, await repository.LoadAsync());
    }

    [TestMethod]
    public async Task CategoryNameChanged_NotifiesBindingsAndCommand_AndClearingDisablesCommand()
    {
        var vm = new MainViewModel(new TodoService(new InMemoryTodoRepository()));
        await vm.LoadCommand.ExecuteAsync();
        var events = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.NewCategoryRequest)) events.Add("parameter");
        };
        vm.AddCategoryCommand.CanExecuteChanged += (_, _) => events.Add("command");
        Assert.IsFalse(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));

        vm.NewCategoryName = "Учёба";

        CollectionAssert.AreEqual(new[] { "parameter", "command" }, events);
        Assert.IsTrue(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));
        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);
        Assert.HasCount(1, vm.CategoryItems);
        Assert.AreEqual("", vm.NewCategoryName);
        Assert.IsFalse(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));
    }

    [TestMethod]
    public async Task LoadCommand_NotifiesCreationCommands_WhenDataBecomesReady()
    {
        var vm = new MainViewModel(new TodoService(new InMemoryTodoRepository()))
        { NewTaskTitle = "Задача", NewCategoryName = "Категория" };
        var taskEvents = 0;
        var categoryEvents = 0;
        vm.AddTaskCommand.CanExecuteChanged += (_, _) => taskEvents++;
        vm.AddCategoryCommand.CanExecuteChanged += (_, _) => categoryEvents++;
        Assert.IsFalse(vm.AddTaskCommand.CanExecute(vm.NewTaskRequest));
        Assert.IsFalse(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));

        await vm.LoadCommand.ExecuteAsync();

        Assert.IsTrue(vm.AddTaskCommand.CanExecute(vm.NewTaskRequest));
        Assert.IsTrue(vm.AddCategoryCommand.CanExecute(vm.NewCategoryRequest));
        Assert.AreEqual(1, taskEvents);
        Assert.AreEqual(1, categoryEvents);
    }

    [TestMethod]
    public async Task AddCommands_UseProvidedRequest_InsteadOfCurrentFormFields()
    {
        var repository = new InMemoryTodoRepository();
        var vm = new MainViewModel(new TodoService(repository));
        await vm.LoadCommand.ExecuteAsync();
        vm.NewCategoryName = "Другое название";
        await vm.AddCategoryCommand.ExecuteAsync(new CategoryCreationRequest(" Из параметра "));
        var category = vm.Categories.Single(c => c.Id != Guid.Empty);
        Assert.AreEqual("Из параметра", category.Name);
        vm.NewTaskTitle = "Другая задача";
        vm.SelectedCategory = Category.None;

        await vm.AddTaskCommand.ExecuteAsync(new TaskCreationRequest(" Задача из параметра ", category));
        var parent = vm.Tasks.Single();
        parent.NewChildTitle = "Другая подзадача";
        parent.NewChildCategory = Category.None;
        await parent.AddChildCommand.ExecuteAsync(new TaskCreationRequest(" Подзадача из параметра ", category));

        var saved = (await repository.LoadAsync()).Tasks.Single();
        Assert.AreEqual("Задача из параметра", saved.Title);
        Assert.AreEqual(category.Id, saved.CategoryId);
        Assert.AreEqual("Подзадача из параметра", saved.Children.Single().Title);
        Assert.AreEqual(category.Id, saved.Children.Single().CategoryId);
        Assert.IsFalse(vm.AddTaskCommand.CanExecute(vm.NewTaskRequest));
        Assert.IsFalse(parent.AddChildCommand.CanExecute(parent.NewChildRequest));
    }

    [TestMethod]
    public async Task AddCommands_MissingOrWrongParameter_DoNotExecute()
    {
        var vm = new MainViewModel(new TodoService(new InMemoryTodoRepository()));
        await vm.LoadCommand.ExecuteAsync();
        vm.NewCategoryName = "Корректное название";
        vm.NewTaskTitle = "Корректное название";

        foreach (var command in new[] { vm.AddTaskCommand, vm.AddCategoryCommand })
        {
            Assert.IsFalse(command.CanExecute(null));
            Assert.IsFalse(command.CanExecute("wrong type"));
            await command.ExecuteAsync();
            await command.ExecuteAsync("wrong type");
        }

        Assert.IsEmpty(vm.Tasks);
        Assert.IsEmpty(vm.CategoryItems);
        Assert.IsFalse(vm.HasMessage);
    }
}
