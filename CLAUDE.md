# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build ProjectManagement.slnx

# Run the API (http://localhost:5050 / https://localhost:7150)
dotnet run --project src/ProjectManagement.API

# Run all unit tests
dotnet test tests/ProjectManagement.Tests

# Run a single test by name fragment
dotnet test tests/ProjectManagement.Tests --filter "FullyQualifiedName~Handle_WithValidOwner"

# Add a migration (run from solution root)
dotnet ef migrations add <Name> --project src/ProjectManagement.Infrastructure --startup-project src/ProjectManagement.API
dotnet ef database update  --project src/ProjectManagement.Infrastructure --startup-project src/ProjectManagement.API
```

> **Migration gotcha:** `ApplicationDbContextFactory` (`Infrastructure/Persistence/`) has a hardcoded `(localdb)\mssqllocaldb` connection used exclusively by EF design-time tools. The running application reads from `appsettings.json` (`(localdb)\MSSQLLocalDB`). These are the same server but the casing must match — mismatches caused a "cannot open database" error in this project's history.

**Docker (run from solution root):**
```bash
docker-compose up --build        # build image and start all services
docker-compose up -d --build     # detached mode
docker-compose down -v           # stop, remove containers and sql_data volume
```

## Architecture

```
Shared  ←  Domain  ←  Application  ←  Infrastructure
                   ↑                         ↓
                   └──────────── API ────────┘
```

| Project | Role |
|---------|------|
| `ProjectManagement.Shared` | `ApiResponse<T>`, `PaginatedResponse<T>` — no dependencies |
| `ProjectManagement.Domain` | Entities, enums, domain interfaces. Zero NuGet dependencies. |
| `ProjectManagement.Application` | CQRS handlers, validators, pipeline behaviors, DTOs, service interfaces |
| `ProjectManagement.Infrastructure` | EF Core, Identity, JWT, Redis, repository/service implementations |
| `ProjectManagement.API` | Controllers, middleware, DI wiring |

Application never references Infrastructure. Domain never references Application.

## Key Patterns

### CQRS (MediatR 12)

Every handler returns `ApiResponse<T>`. Controllers call `mediator.Send(...)` and return `Ok(result)` directly — never double-wrap. `ApiResponse.Failure(...)` is produced by `ExceptionHandlingMiddleware`, not inside handlers.

- `IRequest<ApiResponse<YourDto>>` — commands/queries that return data
- `IRequest<ApiResponse<string>>` — Update and Delete operations (return a message)

**Pipeline behaviors** (registered in `Application/DependencyInjection.cs`, order matters):
1. `LoggingBehavior<,>` — structured log with `RequestName`, `Timestamp`, `UserId`, `ElapsedMs`; injects `ICurrentUserService`
2. `ValidationBehavior<,>` — runs all matching `IValidator<TRequest>`; throws `ValidationException` on failure

### Authorization (RBAC + Resource-Based, two layers)

**Layer 1 — controller attribute** blocks the wrong role before the handler runs.  
**Layer 2 — handler ownership check** is always enforced regardless of role.

Handler ownership pattern (used in every guarded handler):
```csharp
var isAdmin = currentUserService.IsAdmin;
if (!isAdmin && resource.UserId != userId)
    throw new ForbiddenException();
```

**Route and role matrix:**

| Method | Route | Roles | Handler rule |
|--------|-------|-------|--------------|
| POST | `/api/v1/auth/register` | public | — |
| POST | `/api/v1/auth/login` | public | — |
| GET | `/api/v1/projects` | `Admin,User` | Admin → all projects; User → own |
| GET | `/api/v1/projects/{id}` | `Admin,User` | Admin → any; User → own or 403 |
| POST | `/api/v1/projects` | `User` | — |
| PUT | `/api/v1/projects/{id}` | `Admin,User` | Admin → any; User → own or 403 |
| DELETE | `/api/v1/projects/{id}` | `Admin` | Admin → any |
| GET | `/api/v1/projects/{pid}/tasks` | `Admin,User` | Admin → any project; User → own or 403 |
| POST | `/api/v1/projects/{pid}/tasks` | `User` | — |
| PATCH | `/api/v1/projects/{pid}/tasks/{tid}/status` | `User` | User → own project or 403 |
| DELETE | `/api/v1/projects/{pid}/tasks/{tid}` | `Admin,User` | Admin → any; User → own or 403 |

`[Authorize(Roles = "...")]` is applied **per action**, not at the class level. The class-level `[ProducesResponseType(401)]` is the only class-level auth attribute.

### `ICurrentUserService`

Defined in `Domain/Interfaces/`, implemented in `Infrastructure/Identity/CurrentUserService.cs`:

| Member | Source |
|--------|--------|
| `Guid? UserId` | `ClaimTypes.NameIdentifier` — null when unauthenticated |
| `bool IsAuthenticated` | `ClaimsPrincipal.Identity.IsAuthenticated` |
| `bool IsAdmin` | `ClaimsPrincipal.IsInRole("Admin")` |

All handlers resolve `userId` first (`?? throw new UnauthorizedAccessException(...)`) then check `IsAdmin`.

### Domain Entities (DDD-style)

- Private setters, `private Entity() {}` for EF, static factory methods (`Project.Create(...)`, `User.Create(...)`).
- `TaskItem.Create(...)` is `internal` — tasks are created only through `Project.AddTask(...)`.
- `TaskItem` avoids conflict with `System.Threading.Tasks.Task`.
- `TaskItem.UpdateStatus(newStatus)` — `Done` is terminal. Any non-`Done` target from `Done` throws `InvalidOperationException` (→ 400). `Done → Done` is a no-op.
- `BaseEntity`: `Id` (Guid, auto-generated), `CreatedAt`. `AuditableEntity`: adds `UpdatedAt`.

> **EF Core tracking gotcha:** After calling `project.AddTask(task)`, you **must** also call `await taskRepository.AddAsync(task, ct)` to explicitly register the new `TaskItem` with EF Core's change tracker as `Added`. EF Core 9 does not reliably detect additions to `private readonly List<T>` collection backing fields via snapshot-based change detection — without the explicit call, `SaveChangesAsync` generates an UPDATE (0 rows affected) instead of INSERT, throwing `DbUpdateConcurrencyException`. See `CreateTaskCommandHandler` for the canonical pattern.

### Repository / Unit of Work

- `IRepository<T>`: `AddAsync`, `GetByIdAsync`, `GetAllAsync`, `FindAsync`, `Update`, `Delete`.
- Specialized interfaces in `Domain/Interfaces/`: `IProjectRepository`, `ITaskRepository`, `IUserRepository`.
- All writes go through `IUnitOfWork.SaveChangesAsync()` — never call `DbContext.SaveChangesAsync` directly.
- **Task ownership** is verified through the `Project`, not the `TaskItem` alone. Use `ITaskRepository.GetByIdWithProjectAsync` (single query with `Include(t => t.Project)`) then check `task.Project.UserId`.
- **Admin all-projects:** `IProjectRepository.GetAllWithTasksAsync()` — all projects across all users with tasks included.

### Caching (Redis / Cache-Aside)

`ICacheService` (`Application/Interfaces/`) backed by `RedisCacheService` (`Infrastructure/Caching/`). Default TTL: 5 minutes.

| Cache key | Contains | Invalidated by |
|-----------|----------|----------------|
| `projects_user_{userId}` | `List<ProjectDto>` for one user | Create / Update / Delete any project |
| `projects_all` | `List<ProjectDto>` across all users (Admin path) | Create / Update / Delete any project |
| `project_{id}` | `ProjectDto` | Update / Delete that project |
| `tasks_project_{id}` | `List<TaskDto>` | Create / Update status / Delete any task in that project |

Important rules:
- Every project mutation invalidates **both** `projects_user_{ownerId}` (using `project.UserId`, not the current user's id) **and** `projects_all`.
- Ownership is re-enforced on cache hits for `GetProjectById` (`!isAdmin && dto.UserId != userId`).
- For tasks, ownership is checked against the project **before** the cache lookup — cached lists are never served to non-owners.
- `GetAllProjectsQueryHandler` caches the full list and paginates in-memory; one Redis key covers all page requests.

### Identity: Two-User Model

`ApplicationUser : IdentityUser` (Infrastructure) handles authentication. `User` entity (Domain) stores business data. Both share the same `Guid` Id.

- `POST /auth/register` — creates both `ApplicationUser` + domain `User`. Returns a message. No token.
- `POST /auth/login` — returns `AuthResponseDto { Token, Email, Roles }`.
- `RegisterAsync` assigns the `"User"` role automatically. `"Admin"` role must be assigned manually or via seeding.
- JWT claims include `ClaimTypes.NameIdentifier` (userId), `ClaimTypes.Email`, and one `ClaimTypes.Role` per role. `ClockSkew = TimeSpan.Zero`.

> **Critical:** `Projects.UserId` is a FK referencing `Users.Id` (the domain table), **not** `AspNetUsers.Id`. Any code path that creates a user (seeder, register, admin tooling) must create **both** rows with the same Guid. Creating only the `ApplicationUser` via `UserManager` without the matching domain `User` row causes FK violation 547 on every `POST /projects`.

### Data Seeding

Startup sequence (in `Program.cs`, after `MigrateAsync`):
1. `app.SeedRolesAsync()` — creates `"Admin"` and `"User"` `IdentityRole` rows (idempotent).
2. `app.SeedDefaultUsersAsync()` — creates default accounts (idempotent):

| Email | Password | Role |
|-------|----------|------|
| `admin@pm.com` | `Admin123!` | Admin |
| `user@pm.com` | `User123!` | User |

To add a seeded user: add a `SeedUser` record to `DefaultUsers` in `API/Configurations/DataSeedExtension.cs`.

`SeedDefaultUsersAsync` creates **two rows per account** — an `ApplicationUser` (via `UserManager`) and a matching domain `User` entity (via `IUserRepository` + `IUnitOfWork`), both sharing the same Guid Id. Both steps are required; the seeder is idempotent (checks each table independently before inserting).

### Structured Logging (Serilog)

Packages: `Serilog.AspNetCore 8.0.3` + `Serilog.Sinks.File 6.0.0` in the API project. `appsettings.Development.json` only contains Microsoft log-level overrides and is ignored by Serilog — all Serilog config lives in `appsettings.json` under `"Serilog"`.

Sinks: Console (human-readable) + File (`logs/app-<date>.log`, rolling daily, 14-day retention).

**Log events emitted:**
- Every MediatR request: `LoggingBehavior` logs start (with `UserId`) and completion (with `ElapsedMs`); `LogError` on any exception.
- Every HTTP request: `UseSerilogRequestLogging` middleware.
- Exception middleware: `LogError` for 5xx, `LogWarning` for 401/403, `LogInformation` for 400/404. Fields: `UserId`, `Action` (method + path), `StatusCode`.
- Business events: `LoginCommandHandler` (attempt / failure / success), `CreateProjectCommandHandler` (project created), `CreateTaskCommandHandler` (task created), `UpdateTaskStatusCommandHandler` (status transition with From/To).

### Adding a New Feature Checklist

1. Create `Features/<Area>/Commands/<Name>/` with `<Name>Command.cs`, `<Name>CommandHandler.cs`, `<Name>CommandValidator.cs`.
2. Handler: resolve `userId` → `IsAdmin` check → ownership guard → business logic → `SaveChangesAsync` → cache invalidation → business-event log.
3. **For new entities**: always call `await repository.AddAsync(entity, ct)` explicitly before `SaveChangesAsync`. Never rely on EF Core's snapshot-based collection change detection (see the EF Core tracking gotcha in Domain Entities above).
4. Cache: invalidate with `Task.WhenAll(...)` when data changes. For project mutations, always include `"projects_all"` and `"projects_user_{project.UserId}"` (use the entity's `UserId`, not the current user's id).
5. Controller: no class-level `[Authorize]`; apply `[Authorize(Roles = "...")]` per action. Class-level: `[ApiVersion("1.0")]`, versioned route, `[ProducesResponseType(401)]`.
6. Never double-wrap — return `Ok(result)` where `result` is the `ApiResponse<T>` from the handler.
7. `NotFoundException("EntityName", id)` for missing resources; `ForbiddenException()` for ownership violations.

## Docker

**Services:** `api` (port 8080) ← `sqlserver` 2022 (health-checked, `sql_data` volume) + `redis:7`.

Local dev uses Windows auth (`Trusted_Connection=True`); Docker uses SQL auth. `docker-compose.yml` overrides `appsettings.json` via environment variables with double-underscore notation (`ConnectionStrings__DefaultConnection`, etc.).

Container startup: `MigrateAsync()` → `SeedRolesAsync()` → `SeedDefaultUsersAsync()` → `app.Run()`. `depends_on: condition: service_healthy` makes the API wait for SQL Server's `SELECT 1` health check (~20–30 s on first start).

## Configuration

```json
"ConnectionStrings": { "DefaultConnection": "...", "Redis": "localhost:6379" },
"JwtSettings": { "Issuer", "Audience", "Secret" (≥32 chars), "ExpiryInMinutes" },
"Serilog": { "MinimumLevel": {...}, "WriteTo": [Console, File], "Enrich": ["FromLogContext"] }
```

## Unit Tests

`tests/ProjectManagement.Tests` — xUnit + Moq + FluentAssertions. **50 tests, 0 failures.**

Folders: `Auth/`, `Projects/` (5 handlers), `Tasks/` (4 handlers), `Helpers/DomainFactory.cs`.

Key patterns:
- Handlers are instantiated directly via `CreateHandler()` — no MediatR pipeline.
- `Mock<ICurrentUserService>` returns `false` for `IsAdmin` by default. For admin-path tests: `.Setup(x => x.IsAdmin).Returns(true)`.
- `TaskItem` must be created via `DomainFactory.CreateTaskWithProject(project)` because `TaskItem.Create` is `internal`. The `Project` navigation property is set via reflection.
- `ILogger<T>` is mocked with `new Mock<ILogger<T>>()` — no setup needed.
- `CreateTaskCommandHandler` requires `Mock<ITaskRepository>` (added after the EF tracking fix). Its `AddAsync` does not need a setup — the default `Mock` no-op is sufficient.

## Exception → HTTP Status

| Exception | Status |
|-----------|--------|
| `ValidationException` | 400 |
| `InvalidOperationException` | 400 |
| `UnauthorizedAccessException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| anything else | 500 |
