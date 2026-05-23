using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Infrastructure.Persistence;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);

    public void Dispose() => context.Dispose();
}
