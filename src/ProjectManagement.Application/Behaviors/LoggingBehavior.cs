using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that emits a structured log entry for every request,
/// including elapsed time and the current user's identity.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId      = currentUserService.UserId?.ToString() ?? "anonymous";
        var sw          = Stopwatch.StartNew();

        logger.LogInformation(
            "Handling {RequestName} | Timestamp: {Timestamp:o} | UserId: {UserId}",
            requestName, DateTime.UtcNow, userId);

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Request {RequestName} failed | UserId: {UserId} | Elapsed: {ElapsedMs} ms",
                requestName, userId, sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();
        logger.LogInformation(
            "Handled  {RequestName} | UserId: {UserId} | Status: Success | Elapsed: {ElapsedMs} ms",
            requestName, userId, sw.ElapsedMilliseconds);

        return response;
    }
}
