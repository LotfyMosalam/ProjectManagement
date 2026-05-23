namespace ProjectManagement.Domain.Common;

public abstract class BaseEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
