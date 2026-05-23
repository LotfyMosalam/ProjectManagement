using ProjectManagement.Domain.Common;
using ProjectManagement.Domain.Enums;
using TaskStatus = ProjectManagement.Domain.Enums.TaskStatus;

namespace ProjectManagement.Domain.Entities;

/// <summary>
/// Named TaskItem to avoid conflict with System.Threading.Tasks.Task.
/// </summary>
public sealed class TaskItem : AuditableEntity
{
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime? DueDate { get; private set; }
    public Guid ProjectId { get; private set; }

    public Project Project { get; private set; } = default!;

    private TaskItem() { }

    internal static TaskItem Create(
        Guid projectId, string title, string? description,
        TaskPriority priority, DateTime? dueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Description = description,
            Status = TaskStatus.ToDo,
            Priority = priority,
            DueDate = dueDate,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Updates mutable details of the task.</summary>
    public void UpdateDetails(string title, string? description, TaskPriority priority, DateTime? dueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Transitions the task to a new status. Done is a terminal state.</summary>
    public void UpdateStatus(TaskStatus newStatus)
    {
        if (Status == TaskStatus.Done && newStatus != TaskStatus.Done)
            throw new InvalidOperationException(
                $"Cannot transition a completed task to '{newStatus}'. Done tasks are locked.");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
