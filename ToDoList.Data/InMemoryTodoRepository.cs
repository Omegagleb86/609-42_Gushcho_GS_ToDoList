using ToDoList.Core;
using ToDoList.Models;

namespace ToDoList.Data;

// Temporary storage for the current app session. Replace through DI when adding an ORM.
public sealed class InMemoryTodoRepository : ITodoRepository
{
    private TodoState state = new([], []);
    public Task<TodoState> LoadAsync() => Task.FromResult(state);
    public Task SaveAsync(TodoState state)
    {
        this.state = state;
        return Task.CompletedTask;
    }
}
