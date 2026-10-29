using ToDoList.ViewModels;

namespace ToDoList.Views;

public partial class TaskItemView : ContentView
{
    public TaskItemView() => InitializeComponent();

    // View-only event forwarding: Entry has no UnfocusedCommand.
    private void OnTitleUnfocused(object? sender, FocusEventArgs e)
    {
        if (BindingContext is TaskViewModel task) task.RenameCommand.Execute(null);
    }
}
