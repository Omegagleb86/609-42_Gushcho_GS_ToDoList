using ToDoList.Core;
using ToDoList.Models;
using ToDoList.Tests.Helpers;
using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
[TestCategory("ViewModels")]
public sealed class CategoryDeletionTests
{
    [TestMethod]
    public async Task DeleteCategory_RemovesCategoryFromList()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, _) = await CreateAsync(new([category], []));

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.IsEmpty(vm.CategoryItems);
    }

    [TestMethod]
    public async Task DeleteCategory_PreservesNoCategoryOption()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, _) = await CreateAsync(new([category], []));

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.AreEqual(Category.None, vm.Categories.Single());
    }

    [TestMethod]
    public async Task DeleteCategory_ClearsNewTaskCategorySelection()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, _) = await CreateAsync(new([category], []));
        vm.SelectedCategory = category;

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.AreEqual(Category.None, vm.SelectedCategory);
    }

    [TestMethod]
    public async Task DeleteCategory_ClearsCategoryReferencesThroughoutTree()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, service) = await CreateAsync(initial);

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        TodoTask Clear(TodoTask task) => task with { CategoryId = null, Children = task.Children.Select(Clear).ToArray() };
        TodoFixtures.AssertStateEqual(new([], initial.Tasks.Select(Clear).ToArray()), service.SaveRequests.Single());
    }

    [TestMethod]
    public async Task DeleteCategory_PreservesUnrelatedCategorySelection()
    {
        var initial = TodoFixtures.CreateTree();
        var other = new Category(Guid.NewGuid(), "Личное");
        var deleted = initial.Categories[0];
        initial = initial with { Categories = [deleted, other], Tasks = [initial.Tasks[0], initial.Tasks[1] with { CategoryId = other.Id }] };
        var (vm, _) = await CreateAsync(initial);

        await vm.CategoryItems.Single(c => c.Name == deleted.Name).DeleteCommand.ExecuteAsync();

        Assert.AreEqual(other, vm.Tasks[1].SelectedCategory);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task DeleteCategory_ClearsSubtaskDraftCategory(int depth)
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, _) = await CreateAsync(initial);
        var target = depth == 0 ? vm.Tasks[0] : vm.Tasks[0].Children[0];
        target.NewChildCategory = initial.Categories[0];

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.AreEqual(Category.None, target.NewChildCategory);
    }

    [TestMethod]
    public async Task AddCategory_AfterDeletion_AllowsReusingName()
    {
        var category = new Category(Guid.NewGuid(), "Учёба");
        var (vm, _) = await CreateAsync(new([category], []));
        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();
        vm.NewCategoryName = category.Name;

        await vm.AddCategoryCommand.ExecuteAsync(vm.NewCategoryRequest);

        Assert.AreEqual(category.Name, vm.CategoryItems.Single().Name);
    }

    [TestMethod]
    public async Task DeleteCategory_ServiceFails_ShowsError()
    {
        var (vm, service) = await CreateAsync(TodoFixtures.CreateTree());
        service.SaveError = new IOException("Storage unavailable");

        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();

        Assert.IsTrue(vm.HasMessage);
    }

    [TestMethod]
    public async Task SaveCommand_AfterFailedCategoryDeletion_RetriesPendingSnapshot()
    {
        var initial = TodoFixtures.CreateTree();
        var (vm, service) = await CreateAsync(initial);
        service.SaveError = new IOException("Storage unavailable");
        await vm.CategoryItems.Single().DeleteCommand.ExecuteAsync();
        service.SaveError = null;
        service.SaveRequests.Clear();

        await vm.SaveCommand.ExecuteAsync();

        TodoTask Clear(TodoTask task) => task with { CategoryId = null, Children = task.Children.Select(Clear).ToArray() };
        TodoFixtures.AssertStateEqual(new([], initial.Tasks.Select(Clear).ToArray()), service.SaveRequests.Single());
    }

    private static async Task<(MainViewModel Vm, StubTodoService Service)> CreateAsync(TodoState initial)
    {
        var service = new StubTodoService { State = initial };
        var vm = new MainViewModel(service);
        await vm.LoadCommand.ExecuteAsync();
        return (vm, service);
    }
}
