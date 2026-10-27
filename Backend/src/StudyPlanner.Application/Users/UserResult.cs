namespace StudyPlanner.Application.Users;

public enum UserErrorType
{
    Validation = 0
}

public sealed record UserError(UserErrorType Type, string Message);

public class UserResult<T>
{
    public T? Value { get; set; }
    public UserError? Error { get; set; }

    public bool IsSuccess => Error == null;

    public static UserResult<T> Success(T value) => new(value, null);

    public static UserResult<T> Failure(UserError error) => new( default, error);


    private UserResult(T? value, UserError? e)
    {
        Value = value;
        Error = e;
    }
}
