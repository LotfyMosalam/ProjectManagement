using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Caching;
using ProjectManagement.Infrastructure.Identity;
using ProjectManagement.Infrastructure.Persistence;
using ProjectManagement.Infrastructure.Persistence.Repositories;
using ProjectManagement.Infrastructure.Services;
using StackExchange.Redis;

namespace ProjectManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- EF Core with SQL Server ---
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // --- ASP.NET Core Identity ---
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // --- JWT ---
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // --- Identity service (register/login orchestration) ---
        services.AddScoped<IIdentityService, IdentityService>();

        // --- Repositories ---
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // --- Unit of Work ---
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // --- Supporting services ---
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // --- Redis ---
        // abortConnect=false → the multiplexer retries in the background instead of
        // throwing at startup/first-use when Redis is temporarily unavailable.
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var redisConfig     = $"{redisConnection},abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfig));

        services.AddStackExchangeRedisCache(options =>
            options.Configuration = redisConfig);

        services.AddSingleton<ICacheService, RedisCacheService>();

        // --- HttpContext accessor ---
        services.AddHttpContextAccessor();

        return services;
    }
}
