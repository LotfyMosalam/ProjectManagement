namespace ProjectManagement.Shared.Responses;

public class ApiResponse<T>
{
    public bool Succeeded { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IEnumerable<string> Errors { get; init; } = [];

    public static ApiResponse<T> Success(T data, string? message = null) =>
        new() { Succeeded = true, Data = data, Message = message };

    public static ApiResponse<T> Failure(string message, IEnumerable<string>? errors = null) =>
        new() { Succeeded = false, Message = message, Errors = errors ?? [] };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success(string? message = null) =>
        new() { Succeeded = true, Message = message };

    public static new ApiResponse Failure(string message, IEnumerable<string>? errors = null) =>
        new() { Succeeded = false, Message = message, Errors = errors ?? [] };
}
