using System.Collections.ObjectModel;
using ToDoList.Core;
using ToDoList.Models;

namespace ToDoList.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly TodoService service;
    public string StorageDescription { get; }
    private readonly SemaphoreSlim storageLock = new(1, 1);
    private bool loaded;
    private string newTaskTitle = "", newCategoryName = "", message = "";
    private Category? selectedCategory = Category.None;
    public ObservableCollection<Category> Categories { get; } = [Category.None];
    public ObservableCollection<TaskViewModel> Tasks { get; } = [];
    public string NewTaskTitle { get => newTaskTitle; set => SetProperty(ref newTaskTitle, value); }
    public string NewCategoryName { get => newCategoryName; set => SetProperty(ref newCategoryName, value); }
    public Category? SelectedCategory { get => selectedCategory; set => SetProperty(ref selectedCategory, value); }
    public string Message
    {
        get => message;
        private set { if (SetProperty(ref message, value)) OnPropertyChanged(nameof(HasMessage)); }
    }
    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool HasLoaded => loaded;
    public AsyncCommand AddTaskCommand { get; }
    public AsyncCommand AddCategoryCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand LoadCommand { get; }

    public MainViewModel(TodoService service, string storageDescription = "Задачи сохраняются в памяти до закрытия приложения.")
    {
        this.service = service;
        StorageDescription = storageDescription;
        AddTaskCommand = Command(async () =>
        {
            if (!HasLoaded || !ValidTitle(NewTaskTitle)) return;
            Tasks.Add(new TaskViewModel(this, Tasks, new(Guid.NewGuid(), NewTaskTitle.Trim(), false, CategoryId(SelectedCategory), [])));
            NewTaskTitle = "";
            await SaveAsync();
        });
        AddCategoryCommand = Command(async () =>
        {
            if (!HasLoaded) return;
            var name = NewCategoryName.Trim();
            if (!ValidTitle(name)) return;
            TodoRules.EnsureUniqueCategory(name, Categories);
            var category = new Category(Guid.NewGuid(), name);
            Categories.Add(category);
            SelectedCategory = category;
            NewCategoryName = "";
            await SaveAsync();
        });
        LoadCommand = Command(async () =>
        {
            if (loaded) return;
            var state = await service.LoadAsync();
            Categories.Clear();
            Categories.Add(Category.None);
            foreach (var category in state.Categories) Categories.Add(category);
            Tasks.Clear();
            foreach (var task in state.Tasks) Tasks.Add(new TaskViewModel(this, Tasks, task));
            loaded = true;
            OnPropertyChanged(nameof(HasLoaded));
            Message = "";
        });
        SaveCommand = Command(async () =>
        {
            if (!HasLoaded) await LoadCommand.ExecuteAsync();
            else await SaveAsync();
        });
    }

    public AsyncCommand Command(Func<Task> action) => new(action, error =>
        Message = error is ArgumentException ? error.Message
            : "Не удалось выполнить операцию. Повторите загрузку или сохранение. " + error.Message);

    public bool ValidTitle(string? title)
    {
        try { TodoRules.NormalizeName(title); Message = ""; return true; }
        catch (ArgumentException error) { Message = error.Message; return false; }
    }

    public static Guid? CategoryId(Category? category) => category is null || category.Id == Guid.Empty ? null : category.Id;

    public void RequestSave() => Command(SaveAsync).Execute(null);

    public async Task SaveAsync()
    {
        await storageLock.WaitAsync();
        try
        {
            await service.SaveAsync(new TodoState(Categories.Where(c => c.Id != Guid.Empty).ToArray(), Tasks.Select(t => t.ToModel()).ToArray()));
            Message = "";
        }
        finally { storageLock.Release(); }
    }
}
