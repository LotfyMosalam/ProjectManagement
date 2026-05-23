using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Infrastructure.Persistence.Repositories;

public class ProjectRepository(ApplicationDbContext context)
    : BaseRepository<Project>(context), IProjectRepository
{
    public async Task<Project?> GetByIdWithTasksAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(p => p.Tasks)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IEnumerable<Project>> GetAllWithTasksAsync(CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(p => p.Tasks)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Project>> GetPagedAsync(
        int pageNumber, int pageSize,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(cancellationToken);

    public async Task<IEnumerable<Project>> GetPagedByUserAsync(
        Guid userId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(p => p.UserId == userId)
            .Include(p => p.Tasks)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(p => p.UserId == userId, cancellationToken);

    public async Task<IEnumerable<Project>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(p => p.UserId == userId)
            .Include(p => p.Tasks)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
}
