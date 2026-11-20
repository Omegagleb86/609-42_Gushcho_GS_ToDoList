using ToDoList.Models;

namespace ToDoList.ViewModels;

// Immutable snapshots passed by the View through CommandParameter/ReturnCommandParameter.
public sealed record TaskCreationRequest(string Title, Category? Category);
public sealed record CategoryCreationRequest(string Name);
