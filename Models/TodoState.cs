namespace ToDoList.Models;

// Plain data contracts: no MAUI or ORM dependencies.
public sealed record Category(Guid Id, string Name)
{
    public static Category None { get; } = new(Guid.Empty, "Без категории");
}
public sealed record TodoTask(Guid Id, string Title, bool IsCompleted, Guid? CategoryId,
    IReadOnlyList<TodoTask> Children);
public sealed record TodoState(IReadOnlyList<Category> Categories, IReadOnlyList<TodoTask> Tasks);
