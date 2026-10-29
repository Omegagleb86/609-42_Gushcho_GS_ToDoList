using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToDoList.Data;
using ToDoList.Core;
using ToDoList.Models;
using ToDoList.ViewModels;

static void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
    Console.WriteLine("PASS: " + scenario);
}

var repository = new InMemoryTodoRepository();
var vm = new MainViewModel(new TodoService(repository));
await vm.LoadCommand.ExecuteAsync();
vm.NewTaskTitle = "   ";
await vm.AddTaskCommand.ExecuteAsync();
Check(vm.Tasks.Count == 0 && vm.Message.Length > 0, "Blank task rejected");
vm.NewCategoryName = " Учёба ";
await vm.AddCategoryCommand.ExecuteAsync();
vm.NewCategoryName = "учёба";
await vm.AddCategoryCommand.ExecuteAsync();
Check(vm.Categories.Count == 2 && vm.Message.Length > 0, "Trimmed categories; case-insensitive duplicate rejected");
vm.NewTaskTitle = " Проект ";
await vm.AddTaskCommand.ExecuteAsync();
var parent = vm.Tasks.Single();
Check(parent.Title == "Проект" && parent.SelectedCategory?.Name == "Учёба", "Task created with category");
await parent.ShowChildCommand.ExecuteAsync();
parent.NewChildTitle = "Экран";
await parent.AddChildCommand.ExecuteAsync();
var child = parent.Children.Single();
await child.ShowChildCommand.ExecuteAsync();
child.NewChildTitle = "Кнопки";
await child.AddChildCommand.ExecuteAsync();
await child.ToggleCommand.ExecuteAsync();
Check(child.StatusIcon == "radio_active.png" && parent.StatusIcon == "radio_notactive.png", "Independent completion state");
await child.ToggleCommand.ExecuteAsync();
Check(child.StatusIcon == "radio_notactive.png", "Completion can be reversed");
parent.Title = " ";
await parent.RenameCommand.ExecuteAsync();
Check(parent.Title == "Проект", "Blank rename restores valid title");
parent.Title = " Новый проект ";
await parent.RenameCommand.ExecuteAsync();
parent.SelectedCategory = Category.None;
await vm.SaveCommand.ExecuteAsync();
var restored = new MainViewModel(new TodoService(repository));
await restored.LoadCommand.ExecuteAsync();
var restoredParent = restored.Tasks.Single();
Check(restoredParent.Title == "Новый проект" && restoredParent.SelectedCategory == Category.None && restoredParent.Children.Single().Children.Count == 1,
    "Repository round trip preserves renamed tree and cleared category");
await restoredParent.Children.Single().DeleteCommand.ExecuteAsync();
Check((await repository.LoadAsync()).Tasks.Single().Children.Count == 0, "Deleting child removes its descendants");
await restoredParent.DeleteCommand.ExecuteAsync();
Check((await repository.LoadAsync()).Tasks.Count == 0, "Deleting root persists removal");
var failing = new MainViewModel(new TodoService(new FailingRepository()));
await failing.LoadCommand.ExecuteAsync();
failing.NewTaskTitle = "Не потерять";
await failing.AddTaskCommand.ExecuteAsync();
Check(failing.Tasks.Count == 1 && failing.Message.Contains("Не удалось"), "Storage errors surfaced; unsaved task retained");

// Relational checks run on a temporary in-memory database, never on user data.
await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<TodoDbContext>().UseSqlite(connection).Options;
var relational = new EfTodoRepository(() => new TodoDbContext(options));
var categoryId = Guid.NewGuid();
var rootId = Guid.NewGuid();
var childId = Guid.NewGuid();
var leafId = Guid.NewGuid();
var otherId = Guid.NewGuid();
var initial = new TodoState([new Category(categoryId, "Учёба")], [
    new(rootId, "Родитель", false, categoryId, [
        new(childId, "Подзадача", true, null, [new(leafId, "Третий уровень", false, categoryId, [])])]),
    new(otherId, "Вторая задача", false, null, [])]);
await relational.SaveAsync(initial);
var fromDb = await relational.LoadAsync();
Check(fromDb.Tasks[0].Children[0].Children[0].Id == leafId && fromDb.Tasks[0].Children[0].IsCompleted,
    "ORM round trip preserves tree, category, status and order");
var afterUpdate = new TodoState([], [new(otherId, "Обновлено", true, null, [new(childId, "Перенесено", false, null, [])])]);
await relational.SaveAsync(afterUpdate);
fromDb = await relational.LoadAsync();
Check(fromDb.Categories.Count == 0 && fromDb.Tasks.Single().Id == otherId && fromDb.Tasks[0].Children[0].Id == childId,
    "ORM update handles deleted categories, deleted descendants and reparenting");

await using (var db = new TodoDbContext(options))
{
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_write BEFORE INSERT ON Tasks WHEN NEW.Title = 'FAIL' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
}
var rejected = false;
try { await relational.SaveAsync(new([], [new(Guid.NewGuid(), "FAIL", false, null, [])])); }
catch (DbUpdateException) { rejected = true; }
fromDb = await relational.LoadAsync();
Check(rejected && fromDb.Tasks.Single().Title == "Обновлено" && fromDb.Tasks[0].Children.Count == 1,
    "ORM rolls back all deletions and updates if a later write fails");
rejected = false;
try { await relational.SaveAsync(new([], [new(Guid.NewGuid(), "Нет категории", false, Guid.NewGuid(), [])])); }
catch (ArgumentException) { rejected = true; }
Check(rejected && (await relational.LoadAsync()).Tasks.Count == 1, "Invalid category rejected before changing database");
rejected = false;
try { TodoRules.NormalizeName(new string('x', TodoRules.MaxNameLength + 1)); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "Business rules reject overlong names");
var repeated = new TodoTask(Guid.NewGuid(), "Повтор", false, null, []);
rejected = false;
try { TodoRules.Validate(new([], [repeated, repeated])); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "Business rules reject duplicate task IDs");
var mysqlOptions = new DbContextOptionsBuilder<TodoDbContext>()
    .UseMySql("Server=localhost;Database=todolist_schema_check;User ID=test;Password=unused", new MySqlServerVersion(new Version(8, 0, 0))).Options;
await using (var mysql = new TodoDbContext(mysqlOptions))
{
    var script = mysql.Database.GenerateCreateScript();
    Check(script.Contains("CREATE TABLE `Tasks`") && script.Contains("FOREIGN KEY (`ParentId`)"), "MySQL provider generates table schema with parent FK");
}
await relational.SaveAsync(new([], []));
Check((await relational.LoadAsync()).Tasks.Count == 0, "ORM saves an empty list after deleting all tasks");

sealed class FailingRepository : ITodoRepository
{
    public Task<TodoState> LoadAsync() => Task.FromResult(new TodoState([], []));
    public Task SaveAsync(TodoState state) => throw new IOException("Storage unavailable");
}
