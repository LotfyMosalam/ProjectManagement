using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IEnumerable<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdWithProjectAsync(Guid taskId, CancellationToken cancellationToken = default);
}
