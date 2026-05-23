using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Application.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Project> Projects { get; }
    IQueryable<TaskItem> Tasks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
