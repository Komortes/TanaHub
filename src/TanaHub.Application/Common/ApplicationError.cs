namespace TanaHub.Application.Common;

public sealed record ApplicationError(string Code, string Message)
{
    public static ApplicationError None { get; } = new(string.Empty, string.Empty);

    public static ApplicationError Validation(string message)
    {
        return new("validation_error", message);
    }

    public static ApplicationError NotFound(string message)
    {
        return new("not_found", message);
    }

    public static ApplicationError ExternalService(string message)
    {
        return new("external_service_error", message);
    }
}
