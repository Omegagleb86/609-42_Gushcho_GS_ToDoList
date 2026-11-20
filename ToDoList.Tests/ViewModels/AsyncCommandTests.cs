using ToDoList.ViewModels;

namespace ToDoList.Tests.ViewModels;

[TestClass]
[TestCategory("ViewModels")]
public sealed class AsyncCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_RespectsPredicateAndPassesParameter()
    {
        object? received = null;
        Exception? failure = null;
        var parameter = new CategoryCreationRequest("Категория");
        var command = new AsyncCommand(value =>
        {
            received = value;
            return Task.CompletedTask;
        }, error => failure = error, value => value is CategoryCreationRequest);

        await command.ExecuteAsync("wrong type");
        Assert.IsNull(received);
        await command.ExecuteAsync(parameter);

        Assert.AreSame(parameter, received);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhileRunning_BlocksReentryAndNotifiesAvailability()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var notifications = 0;
        Exception? failure = null;
        var command = new AsyncCommand(async () =>
        {
            calls++;
            await completion.Task;
        }, error => failure = error);
        command.CanExecuteChanged += (_, _) => notifications++;

        var execution = command.ExecuteAsync();
        try
        {
            Assert.IsFalse(command.CanExecute(null));
            await command.ExecuteAsync();
            Assert.AreEqual(1, calls);
        }
        finally
        {
            completion.SetResult();
            await execution;
        }

        Assert.IsTrue(command.CanExecute(null));
        Assert.AreEqual(2, notifications);
        Assert.IsNull(failure);
    }
}
