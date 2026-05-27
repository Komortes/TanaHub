namespace TanaHub.Application.Common;

public sealed record Result<T>
{
    private Result(T? value, ApplicationError error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ApplicationError Error { get; }

    public static Result<T> Success(T value)
    {
        return new(value, ApplicationError.None, true);
    }

    public static Result<T> Failure(ApplicationError error)
    {
        if (error == ApplicationError.None)
        {
            throw new ArgumentException("Failure results require an error.", nameof(error));
        }

        return new(default, error, false);
    }
}
