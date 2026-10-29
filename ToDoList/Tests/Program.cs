using ToDoList.Data;
using ToDoList.Models;
using ToDoList.ViewModels;

static void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
    Console.WriteLine("PASS: " + scenario);
}

var repository = new InMemoryTodoRepository();
var vm = new MainViewModel(repository);
await vm.LoadCommand.ExecuteAsync();
vm.NewTaskTitle = "   ";
await vm.AddTaskCommand.ExecuteAsync();
Check(vm.Tasks.Count == 0 && vm.Message.Length > 0, "Blank task rejected");
vm.NewCategoryName = " Учёба ";
await vm.AddCategoryCommand.ExecuteAsync();
vm.NewCategoryName = "учёба";
await vm.AddCategoryCommand.ExecuteAsync();
Check(vm.Categories.Count == 2 && vm.Message.Length > 0, "Trimmed categories; case-insensitive duplicate rejected");
vm.NewTaskTitle = " Проект ";
await vm.AddTaskCommand.ExecuteAsync();
var parent = vm.Tasks.Single();
Check(parent.Title == "Проект" && parent.SelectedCategory?.Name == "Учёба", "Task created with category");
await parent.ShowChildCommand.ExecuteAsync();
parent.NewChildTitle = "Экран";
await parent.AddChildCommand.ExecuteAsync();
var child = parent.Children.Single();
await child.ShowChildCommand.ExecuteAsync();
child.NewChildTitle = "Кнопки";
await child.AddChildCommand.ExecuteAsync();
await child.ToggleCommand.ExecuteAsync();
Check(child.StatusIcon == "radio_active.png" && parent.StatusIcon == "radio_notactive.png", "Independent completion state");
await child.ToggleCommand.ExecuteAsync();
Check(child.StatusIcon == "radio_notactive.png", "Completion can be reversed");
parent.Title = " ";
await parent.RenameCommand.ExecuteAsync();
Check(parent.Title == "Проект", "Blank rename restores valid title");
parent.Title = " Новый проект ";
await parent.RenameCommand.ExecuteAsync();
parent.SelectedCategory = Category.None;
await vm.SaveCommand.ExecuteAsync();
var restored = new MainViewModel(repository);
await restored.LoadCommand.ExecuteAsync();
var restoredParent = restored.Tasks.Single();
Check(restoredParent.Title == "Новый проект" && restoredParent.SelectedCategory == Category.None && restoredParent.Children.Single().Children.Count == 1,
    "Repository round trip preserves renamed tree and cleared category");
await restoredParent.Children.Single().DeleteCommand.ExecuteAsync();
Check((await repository.LoadAsync()).Tasks.Single().Children.Count == 0, "Deleting child removes its descendants");
await restoredParent.DeleteCommand.ExecuteAsync();
Check((await repository.LoadAsync()).Tasks.Count == 0, "Deleting root persists removal");
var failing = new MainViewModel(new FailingRepository());
await failing.LoadCommand.ExecuteAsync();
failing.NewTaskTitle = "Не потерять";
await failing.AddTaskCommand.ExecuteAsync();
Check(failing.Tasks.Count == 1 && failing.Message.Contains("Не удалось"), "Storage errors surfaced; unsaved task retained");

sealed class FailingRepository : ITodoRepository
{
    public Task<TodoState> LoadAsync() => Task.FromResult(new TodoState([], []));
    public Task SaveAsync(TodoState state) => throw new IOException("Storage unavailable");
}
