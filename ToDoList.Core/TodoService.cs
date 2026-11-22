using ToDoList.Models;

namespace ToDoList.Core;

// Application rules do not depend on EF, a database provider, or MAUI.
public sealed class TodoService(ITodoRepository repository) : ITodoService
{
    public async Task<TodoState> LoadAsync()
    {
        var state = await repository.LoadAsync();
        TodoRules.Validate(state);
        return state;
    }
    public Task SaveAsync(TodoState state)
    {
        TodoRules.Validate(state);
        return repository.SaveAsync(state);
    }
}
