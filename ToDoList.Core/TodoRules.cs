using ToDoList.Models;

namespace ToDoList.Core;

public static class TodoRules
{
    public const int MaxNameLength = 255;
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Введите непустое название.");
        var name = value.Trim();
        if (name.Length > MaxNameLength) throw new ArgumentException($"Название не должно превышать {MaxNameLength} символов.");
        return name;
    }

    public static void EnsureUniqueCategory(string name, IEnumerable<Category> categories)
    {
        if (categories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Категория с таким названием уже существует.");
    }

    // Validate the whole snapshot before starting a database transaction.
    public static void Validate(TodoState state)
    {
        var categories = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in state.Categories)
        {
            if (category.Id == Guid.Empty || !categories.Add(category.Id))
                throw new ArgumentException("Некорректный или повторяющийся идентификатор категории.");
            var name = NormalizeName(category.Name);
            if (name != category.Name || !names.Add(name) || name.Equals(Category.None.Name, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Некорректное или повторяющееся название категории.");
        }
        var taskIds = new HashSet<Guid>();
        void Visit(TodoTask task)
        {
            if (task.Id == Guid.Empty || !taskIds.Add(task.Id))
                throw new ArgumentException("Повторяющаяся задача или цикл в подзадачах.");
            if (NormalizeName(task.Title) != task.Title) throw new ArgumentException("Уберите пробелы по краям названия.");
            if (task.CategoryId is Guid categoryId && !categories.Contains(categoryId))
                throw new ArgumentException("Категория задачи не найдена.");
            foreach (var child in task.Children) Visit(child);
        }
        foreach (var task in state.Tasks) Visit(task);
    }
}
