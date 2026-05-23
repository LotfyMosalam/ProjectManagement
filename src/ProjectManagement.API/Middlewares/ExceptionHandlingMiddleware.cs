using System.Net;
using System.Text.Json;
using FluentValidation;
using ProjectManagement.Application.Exceptions;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.API.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                ApiResponse.Failure(
                    "Validation failed.",
                    validationEx.Errors.Select(e => e.ErrorMessage))),

            NotFoundException => (
                HttpStatusCode.NotFound,
                ApiResponse.Failure(exception.Message)),

            ForbiddenException => (
                HttpStatusCode.Forbidden,
                ApiResponse.Failure(exception.Message)),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                ApiResponse.Failure(exception.Message)),

            InvalidOperationException => (
                HttpStatusCode.BadRequest,
                ApiResponse.Failure(exception.Message)),

            _ => (
                HttpStatusCode.InternalServerError,
                ApiResponse.Failure("An unexpected error occurred."))
        };

        // Structured log — severity depends on HTTP status
        LogException(context, exception, (int)statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private void LogException(HttpContext context, Exception exception, int statusCode)
    {
        var userId = context.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        var logProps = new
        {
            Timestamp  = DateTime.UtcNow,
            UserId     = userId,
            Action     = $"{context.Request.Method} {context.Request.Path}",
            StatusCode = statusCode,
            ExType     = exception.GetType().Name
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception,
                "Unhandled exception | UserId: {UserId} | Action: {Action} | Status: {StatusCode} | Type: {ExType}",
                logProps.UserId, logProps.Action, logProps.StatusCode, logProps.ExType);
        }
        else if (statusCode == 403 || statusCode == 401)
        {
            logger.LogWarning(
                "Auth failure | UserId: {UserId} | Action: {Action} | Status: {StatusCode} | Type: {ExType}",
                logProps.UserId, logProps.Action, logProps.StatusCode, logProps.ExType);
        }
        else
        {
            logger.LogInformation(
                "Handled exception | UserId: {UserId} | Action: {Action} | Status: {StatusCode} | Type: {ExType}",
                logProps.UserId, logProps.Action, logProps.StatusCode, logProps.ExType);
        }
    }
}
