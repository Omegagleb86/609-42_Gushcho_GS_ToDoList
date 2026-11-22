using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
[TestCategory("ViewModels")]
public sealed class AsyncCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_RejectedParameter_DoesNotInvokeAction()
    {
        var calls = 0;
        var command = new AsyncCommand(_ =>
        {
            calls++;
            return Task.CompletedTask;
        }, error => Assert.Fail(error.Message), value => value is CategoryCreationRequest);

        await command.ExecuteAsync("wrong type");

        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassesParameterToAction()
    {
        object? received = null;
        var parameter = new CategoryCreationRequest("Категория");
        var command = new AsyncCommand(value =>
        {
            received = value;
            return Task.CompletedTask;
        }, error => Assert.Fail(error.Message));

        await command.ExecuteAsync(parameter);

        Assert.AreSame(parameter, received);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhileRunning_BlocksReentry()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new AsyncCommand(async () =>
        {
            calls++;
            await completion.Task;
        }, error => Assert.Fail(error.Message));
        var execution = command.ExecuteAsync();
        try
        {
            await command.ExecuteAsync();

            Assert.AreEqual(1, calls);
        }
        finally
        {
            completion.SetResult();
            await execution;
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NotifiesAvailabilityAtStartAndFinish()
    {
        var command = new AsyncCommand(() => Task.CompletedTask, error => Assert.Fail(error.Message));
        var enabledStates = new List<bool>();
        command.CanExecuteChanged += (_, _) => enabledStates.Add(command.CanExecute(null));

        await command.ExecuteAsync();

        CollectionAssert.AreEqual(new[] { false, true }, enabledStates);
    }
}
