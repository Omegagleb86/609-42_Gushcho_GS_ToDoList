using ToDoList.Models;

namespace ToDoList.Core;

public interface ITodoService
{
    Task<TodoState> LoadAsync();
    Task SaveAsync(TodoState state);
}
