using ToDoList.Core;
using ToDoList.Models;

namespace ToDoList.Tests.Core;

[TestClass]
[TestCategory("Core")]
public sealed class TodoRulesTests
{
    [TestMethod]
    public void NormalizeName_NameExceedsLimit_ThrowsArgumentException()
    {
        var name = new string('x', TodoRules.MaxNameLength + 1);

        Assert.ThrowsExactly<ArgumentException>(() => TodoRules.NormalizeName(name));
    }

    [TestMethod]
    public void Validate_DuplicateTaskIds_ThrowsArgumentException()
    {
        var task = new TodoTask(Guid.NewGuid(), "Повтор", false, null, []);
        var state = new TodoState([], [task, task]);

        Assert.ThrowsExactly<ArgumentException>(() => TodoRules.Validate(state));
    }
}
