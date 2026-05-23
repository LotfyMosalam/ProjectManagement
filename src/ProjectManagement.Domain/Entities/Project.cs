using ProjectManagement.Domain.Common;
using ProjectManagement.Domain.Enums;

namespace ProjectManagement.Domain.Entities;

public sealed class Project : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid UserId { get; private set; }

    public User User { get; private set; } = default!;

    private readonly List<TaskItem> _tasks = [];
    public IReadOnlyCollection<TaskItem> Tasks => _tasks.AsReadOnly();

    private Project() { }

    public static Project Create(string name, string? description, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Project
        {
            Name = name,
            Description = description,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Adds a new task to this project and returns the created TaskItem.</summary>
    public TaskItem AddTask(string title, string? description, TaskPriority priority, DateTime? dueDate)
    {
        var task = TaskItem.Create(Id, title, description, priority, dueDate);
        _tasks.Add(task);
        return task;
    }

    public void RemoveTask(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _tasks.Remove(task);
    }
}
