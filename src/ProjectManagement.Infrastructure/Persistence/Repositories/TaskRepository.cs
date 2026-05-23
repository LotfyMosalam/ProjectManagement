using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Infrastructure.Persistence.Repositories;

public class TaskRepository(ApplicationDbContext context)
    : BaseRepository<TaskItem>(context), ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<TaskItem?> GetByIdWithProjectAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
}
