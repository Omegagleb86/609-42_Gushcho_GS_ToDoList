using System.Collections.ObjectModel;
using ToDoList.Core;
using ToDoList.Models;

namespace ToDoList.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ITodoService service;
    public string StorageDescription { get; }
    private readonly SemaphoreSlim storageLock = new(1, 1);
    private bool loaded;
    private bool changingCategories;
    private string newTaskTitle = "", newCategoryName = "", message = "";
    private Category? selectedCategory = Category.None;
    public ObservableCollection<Category> Categories { get; } = [Category.None];
    public ObservableCollection<TaskViewModel> Tasks { get; } = [];
    public ObservableCollection<CategoryViewModel> CategoryItems { get; } = [];
    public TaskCreationRequest NewTaskRequest => new(NewTaskTitle, SelectedCategory);
    public CategoryCreationRequest NewCategoryRequest => new(NewCategoryName);
    public string NewTaskTitle
    {
        get => newTaskTitle;
        set
        {
            if (!SetProperty(ref newTaskTitle, value)) return;
            OnPropertyChanged(nameof(NewTaskRequest));
            AddTaskCommand?.NotifyCanExecuteChanged();
        }
    }
    public string NewCategoryName
    {
        get => newCategoryName;
        set
        {
            if (!SetProperty(ref newCategoryName, value)) return;
            OnPropertyChanged(nameof(NewCategoryRequest));
            AddCategoryCommand?.NotifyCanExecuteChanged();
        }
    }
    public Category? SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (!SetProperty(ref selectedCategory, value)) return;
            OnPropertyChanged(nameof(NewTaskRequest));
            AddTaskCommand?.NotifyCanExecuteChanged();
        }
    }
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

    public MainViewModel(ITodoService service, string storageDescription = "Задачи сохраняются в памяти до закрытия приложения.")
    {
        this.service = service;
        StorageDescription = storageDescription;
        Categories.CollectionChanged += (_, _) =>
        {
            CategoryItems.Clear();
            foreach (var category in Categories.Where(c => c.Id != Guid.Empty))
                CategoryItems.Add(new CategoryViewModel(category.Name, Command(() => DeleteCategoryAsync(category))));
        };
        AddTaskCommand = Command(async parameter =>
        {
            var request = (TaskCreationRequest)parameter!;
            if (!ValidTitle(request.Title)) return;
            Tasks.Add(new TaskViewModel(this, Tasks, new(Guid.NewGuid(), request.Title.Trim(), false, CategoryId(request.Category), [])));
            NewTaskTitle = "";
            await SaveAsync();
        }, parameter => HasLoaded && parameter is TaskCreationRequest request && !string.IsNullOrWhiteSpace(request.Title));
        AddCategoryCommand = Command(async parameter =>
        {
            var request = (CategoryCreationRequest)parameter!;
            var name = request.Name.Trim();
            if (!ValidTitle(name)) return;
            TodoRules.EnsureUniqueCategory(name, Categories);
            var category = new Category(Guid.NewGuid(), name);
            Categories.Add(category);
            SelectedCategory = category;
            NewCategoryName = "";
            await SaveAsync();
        }, parameter => HasLoaded && parameter is CategoryCreationRequest request && !string.IsNullOrWhiteSpace(request.Name));
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
            AddTaskCommand.NotifyCanExecuteChanged();
            AddCategoryCommand.NotifyCanExecuteChanged();
            Message = "";
        });
        SaveCommand = Command(async () =>
        {
            if (!HasLoaded) await LoadCommand.ExecuteAsync();
            else await SaveAsync();
        });
    }

    public AsyncCommand Command(Func<Task> action) => new(action, HandleError);

    public AsyncCommand Command(Func<object?, Task> action, Func<object?, bool> canExecute) =>
        new(action, HandleError, canExecute);

    private void HandleError(Exception error) =>
        Message = error is ArgumentException ? error.Message
            : "Не удалось выполнить операцию. Повторите загрузку или сохранение. " + error.Message;

    public bool ValidTitle(string? title)
    {
        try { TodoRules.NormalizeName(title); Message = ""; return true; }
        catch (ArgumentException error) { Message = error.Message; return false; }
    }

    public static Guid? CategoryId(Category? category) => category is null || category.Id == Guid.Empty ? null : category.Id;

    private async Task DeleteCategoryAsync(Category category)
    {
        if (!HasLoaded || category.Id == Guid.Empty || !Categories.Contains(category)) return;
        // Pickers may change their selection when ItemsSource changes. Save the complete
        // update once, after all task references and form selections have been cleared.
        changingCategories = true;
        try
        {
            foreach (var task in Tasks) task.ClearCategory(category.Id);
            if (SelectedCategory?.Id == category.Id) SelectedCategory = Category.None;
            Categories.Remove(category);
        }
        finally { changingCategories = false; }
        await SaveAsync();
    }

    public void RequestSave()
    {
        if (!changingCategories) Command(SaveAsync).Execute(null);
    }

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
