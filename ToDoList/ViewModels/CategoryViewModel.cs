namespace ToDoList.ViewModels;

public sealed record CategoryViewModel(string Name, AsyncCommand DeleteCommand);
