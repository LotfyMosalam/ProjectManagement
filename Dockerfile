# ============================================================
# Stage 1 – Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first so NuGet restore is cached as a separate layer.
# Re-runs only when a .csproj changes, not on every source change.
COPY src/ProjectManagement.Shared/ProjectManagement.Shared.csproj               src/ProjectManagement.Shared/
COPY src/ProjectManagement.Domain/ProjectManagement.Domain.csproj               src/ProjectManagement.Domain/
COPY src/ProjectManagement.Application/ProjectManagement.Application.csproj     src/ProjectManagement.Application/
COPY src/ProjectManagement.Infrastructure/ProjectManagement.Infrastructure.csproj src/ProjectManagement.Infrastructure/
COPY src/ProjectManagement.API/ProjectManagement.API.csproj                     src/ProjectManagement.API/

RUN dotnet restore src/ProjectManagement.API/ProjectManagement.API.csproj

# Copy full source tree and publish in Release mode
COPY . .
RUN dotnet publish src/ProjectManagement.API/ProjectManagement.API.csproj \
        --configuration Release \
        --output /app/publish \
        --no-restore

# ============================================================
# Stage 2 – Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ProjectManagement.API.dll"]
