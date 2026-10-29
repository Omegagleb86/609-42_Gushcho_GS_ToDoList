using ToDoList.Models;

namespace ToDoList.Core;

// Storage contract only. ORM implementations belong in ToDoList.Data.
// SaveAsync must atomically save the entire tree, including deletions.
public interface ITodoRepository
{
    Task<TodoState> LoadAsync();
    Task SaveAsync(TodoState state);
}
