using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Common;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Infrastructure.Identity;

namespace ProjectManagement.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    // DbSets used internally by repositories
    public DbSet<User>     UserSet    => Set<User>();
    public DbSet<Project>  ProjectSet => Set<Project>();
    public DbSet<TaskItem> TaskSet    => Set<TaskItem>();

    // IApplicationDbContext explicit implementation
    IQueryable<User>     IApplicationDbContext.Users    => UserSet;
    IQueryable<Project>  IApplicationDbContext.Projects => ProjectSet;
    IQueryable<TaskItem> IApplicationDbContext.Tasks    => TaskSet;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables first
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.CreatedAt == default))
        {
            entry.Entity.CreatedAt = now;
        }

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = now;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
