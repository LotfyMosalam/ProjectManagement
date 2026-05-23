using System.Reflection;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Enums;

namespace ProjectManagement.Tests.Helpers;

/// <summary>
/// Creates real domain objects for use in unit tests.
/// TaskItem.Create is internal, so tasks must be created through Project.AddTask().
/// TaskItem.Project has a private setter — EF Core sets it at runtime; tests use reflection.
/// </summary>
public static class DomainFactory
{
    public static Project CreateProject(
        string name = "Test Project",
        string? description = "Test Description",
        Guid? userId = null) =>
        Project.Create(name, description, userId ?? Guid.NewGuid());

    /// <summary>
    /// Creates a TaskItem via Project.AddTask and wires the Project navigation
    /// property using reflection (mirrors what EF Core does via Include).
    /// </summary>
    public static TaskItem CreateTaskWithProject(
        Project project,
        string title = "Test Task",
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        var task = project.AddTask(title, null, priority, dueDate);

        typeof(TaskItem)
            .GetProperty(nameof(TaskItem.Project))!
            .SetValue(task, project);

        return task;
    }
}
