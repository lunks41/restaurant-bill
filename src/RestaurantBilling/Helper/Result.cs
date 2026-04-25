namespace Helper;

public enum ResultStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Unauthorized,
    Conflict,
    Error
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public ResultStatus Status { get; }

    protected Result(bool isSuccess, string? error, ResultStatus status)
    {
        IsSuccess = isSuccess;
        Error = error;
        Status = status;
    }

    public static Result Success() => new(true, null, ResultStatus.Success);
    public static Result Failure(string error, ResultStatus status = ResultStatus.Error) => new(false, error, status);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? error, ResultStatus status)
        : base(isSuccess, error, status)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultStatus.Success);
    public static new Result<T> Failure(string error, ResultStatus status = ResultStatus.Error) => new(false, default, error, status);
}

