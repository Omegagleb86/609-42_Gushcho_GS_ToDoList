using ToDoList.Core;
using ToDoList.Models;

namespace ToDoList.Tests.Helpers;

// Controlled test double: returns configured data and captures submissions.
// No business validation, ORM, files, or production persistence implementation.
internal sealed class StubTodoService : ITodoService
{
    public TodoState State { get; set; } = new([], []);
    public Exception? SaveError { get; set; }
    public List<TodoState> SaveRequests { get; } = [];
    public Task<TodoState> LoadAsync() => Task.FromResult(State);
    public Task SaveAsync(TodoState state)
    {
        SaveRequests.Add(state);
        if (SaveError is not null) throw SaveError;
        State = state;
        return Task.CompletedTask;
    }
}
