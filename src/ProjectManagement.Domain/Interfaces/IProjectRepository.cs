using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetByIdWithTasksAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all projects across all users, including their tasks. Used by the Admin role.</summary>
    Task<IEnumerable<Project>> GetAllWithTasksAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Project>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Project>> GetPagedByUserAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
