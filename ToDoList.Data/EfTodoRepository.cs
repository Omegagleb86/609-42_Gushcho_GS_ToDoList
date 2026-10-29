using System.Data;
using Microsoft.EntityFrameworkCore;
using ToDoList.Core;
using ToDoList.Data.Entities;
using ToDoList.Models;

namespace ToDoList.Data;

// A repository instance represents one application's complete task list.
// Every operation gets its own context; no EF entities escape this class.
public sealed class EfTodoRepository(Func<TodoDbContext> createContext, bool createDatabase = true) : ITodoRepository
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;

    private async Task InitializeAsync(TodoDbContext db)
    {
        if (initialized) return;
        if (createDatabase) await db.Database.EnsureCreatedAsync();
        initialized = true;
    }

    public async Task<TodoState> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            await using var db = createContext();
            await InitializeAsync(db);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Position).ToListAsync();
            var tasks = await db.Tasks.AsNoTracking().OrderBy(t => t.Position).ToListAsync();
            var byParent = tasks.Where(t => t.ParentId.HasValue).ToLookup(t => t.ParentId!.Value);
            var visited = new HashSet<Guid>();
            TodoTask Build(TaskEntity task)
            {
                if (!visited.Add(task.Id)) throw new InvalidDataException("Обнаружен цикл в подзадачах.");
                return new(task.Id, task.Title, task.IsCompleted, task.CategoryId,
                    byParent[task.Id].Select(Build).ToArray());
            }
            var state = new TodoState(categories.Select(c => new Category(c.Id, c.Name)).ToArray(),
                tasks.Where(t => t.ParentId is null).Select(Build).ToArray());
            if (visited.Count != tasks.Count) throw new InvalidDataException("Нарушены связи родителей и подзадач.");
            TodoRules.Validate(state);
            await transaction.CommitAsync();
            return state;
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(TodoState state)
    {
        TodoRules.Validate(state);
        await gate.WaitAsync();
        try
        {
            await using var db = createContext();
            await InitializeAsync(db);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var storedTasks = await db.Tasks.ToDictionaryAsync(t => t.Id);
            var storedCategories = await db.Categories.ToDictionaryAsync(c => c.Id);
            var flat = new List<TaskEntity>();
            void Flatten(IReadOnlyList<TodoTask> tasks, Guid? parentId)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    flat.Add(new TaskEntity { Id = task.Id, Title = task.Title, IsCompleted = task.IsCompleted,
                        CategoryId = task.CategoryId, ParentId = parentId, Position = i });
                    Flatten(task.Children, task.Id);
                }
            }
            Flatten(state.Tasks, null);
            var taskIds = flat.Select(t => t.Id).ToHashSet();
            var categoryIds = state.Categories.Select(c => c.Id).ToHashSet();

            // Detach relationships first, so reparenting and subtree deletion never violate FKs.
            // All intermediate steps are hidden inside the same transaction.
            foreach (var task in storedTasks.Values) { task.ParentId = null; task.CategoryId = null; }
            await db.SaveChangesAsync();
            db.Tasks.RemoveRange(storedTasks.Values.Where(t => !taskIds.Contains(t.Id)));
            db.Categories.RemoveRange(storedCategories.Values.Where(c => !categoryIds.Contains(c.Id)));
            await db.SaveChangesAsync();

            for (var i = 0; i < state.Categories.Count; i++)
            {
                var category = state.Categories[i];
                if (!storedCategories.TryGetValue(category.Id, out var entity))
                { entity = new CategoryEntity { Id = category.Id }; db.Categories.Add(entity); }
                entity.Name = category.Name;
                entity.Position = i;
            }
            await db.SaveChangesAsync();
            foreach (var task in flat)
            {
                if (!storedTasks.TryGetValue(task.Id, out var entity))
                { entity = new TaskEntity { Id = task.Id }; db.Tasks.Add(entity); storedTasks.Add(task.Id, entity); }
                entity.Title = task.Title;
                entity.IsCompleted = task.IsCompleted;
                entity.CategoryId = task.CategoryId;
                entity.Position = task.Position;
            }
            await db.SaveChangesAsync();
            foreach (var task in flat) storedTasks[task.Id].ParentId = task.ParentId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        finally { gate.Release(); }
    }
}
