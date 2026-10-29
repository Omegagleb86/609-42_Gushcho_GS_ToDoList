using ToDoList.Models;

namespace ToDoList.Data;

// A database implementation belongs in this layer, not in Views or ViewModels.
// SaveAsync must atomically save the entire tree, including deletions.
public interface ITodoRepository
{
    Task<TodoState> LoadAsync();
    Task SaveAsync(TodoState state);
}
