namespace ToDoList;

public partial class AppShell : Shell
{
    public AppShell(MainPage page)
    {
        InitializeComponent();
        MainContent.Content = page;
    }
}
