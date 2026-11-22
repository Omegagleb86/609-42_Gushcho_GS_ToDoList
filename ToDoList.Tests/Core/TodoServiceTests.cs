using ToDoList.Core;
using ToDoList.Models;
using ToDoList.Tests.Helpers;

namespace ToDoList.Tests.Core;

[TestClass]
[TestCategory("Unit")]
[TestCategory("Core")]
public sealed class TodoServiceTests
{
    [TestMethod]
    public async Task LoadAsync_ValidSnapshot_ReturnsRepositoryResult()
    {
        var expected = TodoFixtures.CreateTree();
        var repository = new RepositoryStub { Result = expected };
        var service = new TodoService(repository);

        var actual = await service.LoadAsync();

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, repository.LoadCalls);
    }

    [TestMethod]
    public async Task LoadAsync_InvalidSnapshot_RejectsInvalidRepositoryData()
    {
        var repository = new RepositoryStub { Result = InvalidState() };
        var service = new TodoService(repository);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.LoadAsync());
    }

    [TestMethod]
    public async Task LoadAsync_RepositoryFails_PropagatesError()
    {
        var error = new IOException("Test read failure");
        var service = new TodoService(new RepositoryStub { LoadError = error });

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => service.LoadAsync());

        Assert.AreSame(error, actual);
    }

    [TestMethod]
    public async Task SaveAsync_ValidSnapshot_ForwardsSameSnapshotOnce()
    {
        var repository = new RepositoryStub();
        var service = new TodoService(repository);
        var snapshot = TodoFixtures.CreateTree();

        await service.SaveAsync(snapshot);

        Assert.AreEqual(1, repository.SaveCalls);
        Assert.AreSame(snapshot, repository.Submitted);
    }

    [TestMethod]
    public async Task SaveAsync_InvalidSnapshot_DoesNotCallRepository()
    {
        var repository = new RepositoryStub();
        var service = new TodoService(repository);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SaveAsync(InvalidState()));

        Assert.AreEqual(0, repository.SaveCalls);
        Assert.IsNull(repository.Submitted);
    }

    [TestMethod]
    public async Task SaveAsync_RepositoryFails_PropagatesError()
    {
        var error = new IOException("Test write failure");
        var repository = new RepositoryStub { SaveError = error };
        var service = new TodoService(repository);

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => service.SaveAsync(TodoFixtures.CreateTree()));

        Assert.AreSame(error, actual);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    private static TodoState InvalidState() =>
        new([], [new(Guid.NewGuid(), "Задача", false, Guid.NewGuid(), [])]);

    private sealed class RepositoryStub : ITodoRepository
    {
        public TodoState Result { get; init; } = new([], []);
        public Exception? LoadError { get; init; }
        public Exception? SaveError { get; init; }
        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public TodoState? Submitted { get; private set; }
        public Task<TodoState> LoadAsync()
        {
            LoadCalls++;
            if (LoadError is not null) throw LoadError;
            return Task.FromResult(Result);
        }
        public Task SaveAsync(TodoState state)
        {
            SaveCalls++;
            if (SaveError is not null) throw SaveError;
            Submitted = state;
            return Task.CompletedTask;
        }
    }
}
