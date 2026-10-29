namespace ToDoList.Data.Entities;

public sealed class TaskEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ParentId { get; set; }
    public int Position { get; set; }
}
