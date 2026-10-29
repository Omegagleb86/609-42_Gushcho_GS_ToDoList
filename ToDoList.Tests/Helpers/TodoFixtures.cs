using System.Text.Json;
using ToDoList.Models;

namespace ToDoList.Tests.Helpers;

internal static class TodoFixtures
{
    public static TodoState CreateTree()
    {
        var categoryId = Guid.NewGuid();
        return new([new Category(categoryId, "Учёба")], [
            new(Guid.NewGuid(), "Родитель", false, categoryId, [
                new(Guid.NewGuid(), "Подзадача", true, null, [
                    new(Guid.NewGuid(), "Третий уровень", false, categoryId, [])])]),
            new(Guid.NewGuid(), "Вторая задача", false, null, [])]);
    }

    // Compare all persisted fields and collection order, not record/array reference equality.
    public static void AssertStateEqual(TodoState expected, TodoState actual) =>
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
}
