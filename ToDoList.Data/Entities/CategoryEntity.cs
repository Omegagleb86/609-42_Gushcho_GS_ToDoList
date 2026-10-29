namespace ToDoList.Data.Entities;

public sealed class CategoryEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Position { get; set; }
}
