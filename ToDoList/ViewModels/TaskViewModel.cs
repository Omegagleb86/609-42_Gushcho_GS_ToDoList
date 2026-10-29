using System.Collections.ObjectModel;
using ToDoList.Models;

namespace ToDoList.ViewModels;

public sealed class TaskViewModel : ObservableObject
{
    private readonly MainViewModel owner;
    private readonly ObservableCollection<TaskViewModel> siblings;
    private readonly Guid id;
    private string savedTitle;
    private string title, newChildTitle = "";
    private bool completed, isAddingChild;
    private Category? selectedCategory, newChildCategory;
    public ObservableCollection<Category> Categories => owner.Categories;
    public ObservableCollection<TaskViewModel> Children { get; } = [];
    public string Title { get => title; set => SetProperty(ref title, value); }
    public string NewChildTitle { get => newChildTitle; set => SetProperty(ref newChildTitle, value); }
    public bool IsAddingChild { get => isAddingChild; set => SetProperty(ref isAddingChild, value); }
    public string StatusIcon => completed ? "radio_active.png" : "radio_notactive.png";
    public string StatusText => completed ? "Выполнена" : "Не выполнена";
    public Category? SelectedCategory
    {
        get => selectedCategory;
        set { if (SetProperty(ref selectedCategory, value)) owner.RequestSave(); }
    }
    public Category? NewChildCategory { get => newChildCategory; set => SetProperty(ref newChildCategory, value); }
    public AsyncCommand ToggleCommand { get; }
    public AsyncCommand DeleteCommand { get; }
    public AsyncCommand ShowChildCommand { get; }
    public AsyncCommand AddChildCommand { get; }
    public AsyncCommand RenameCommand { get; }

    public TaskViewModel(MainViewModel owner, ObservableCollection<TaskViewModel> siblings, TodoTask task)
    {
        this.owner = owner;
        this.siblings = siblings;
        id = task.Id;
        title = savedTitle = task.Title;
        completed = task.IsCompleted;
        selectedCategory = Categories.FirstOrDefault(c => c.Id == task.CategoryId) ?? Category.None;
        foreach (var child in task.Children) Children.Add(new TaskViewModel(owner, Children, child));
        ToggleCommand = owner.Command(async () =>
        {
            completed = !completed;
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
            await owner.SaveAsync();
        });
        DeleteCommand = owner.Command(async () => { siblings.Remove(this); await owner.SaveAsync(); });
        ShowChildCommand = owner.Command(() =>
        {
            IsAddingChild = !IsAddingChild;
            NewChildCategory = SelectedCategory;
            return Task.CompletedTask;
        });
        AddChildCommand = owner.Command(async () =>
        {
            if (!owner.ValidTitle(NewChildTitle)) return;
            Children.Add(new TaskViewModel(owner, Children, new(Guid.NewGuid(), NewChildTitle.Trim(), false, MainViewModel.CategoryId(NewChildCategory), [])));
            NewChildTitle = "";
            IsAddingChild = false;
            await owner.SaveAsync();
        });
        RenameCommand = owner.Command(async () =>
        {
            if (!owner.ValidTitle(Title)) { Title = savedTitle; return; }
            Title = Title.Trim();
            savedTitle = Title;
            await owner.SaveAsync();
        });
    }

    // Called as part of a category removal; the owner saves the entire change once.
    internal void ClearCategory(Guid categoryId)
    {
        if (selectedCategory?.Id == categoryId)
            SetProperty(ref selectedCategory, Category.None, nameof(SelectedCategory));
        if (newChildCategory?.Id == categoryId) NewChildCategory = Category.None;
        foreach (var child in Children) child.ClearCategory(categoryId);
    }

    public TodoTask ToModel() => new(id, savedTitle, completed, MainViewModel.CategoryId(SelectedCategory),
        Children.Select(c => c.ToModel()).ToArray());
}
