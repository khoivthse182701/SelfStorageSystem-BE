namespace SelfStorageSystem.Domain.Exceptions;

using SelfStorageSystem.Domain.Common;

public interface IHasAppError
{
    Error Error { get; }
}

public class AppException : Exception, IHasAppError
{
    public Error Error { get; }

    public AppException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }

    public static Exception FromError(Error error, Exception? innerException = null)
    {
        return error.Type switch
        {
            ErrorType.Unauthorized => new AppUnauthorizedException(error, innerException),
            ErrorType.Forbidden    => new AppForbiddenException(error, innerException),
            ErrorType.NotFound     => new AppNotFoundException(error, innerException),
            ErrorType.Validation   => new AppValidationException(error, innerException),
            ErrorType.Conflict     => new AppConflictException(error, innerException),
            _                     => new AppException(error, innerException)
        };
    }
}

public class AppUnauthorizedException : UnauthorizedAccessException, IHasAppError
{
    public Error Error { get; }

    public AppUnauthorizedException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }
}

public class AppNotFoundException : KeyNotFoundException, IHasAppError
{
    public Error Error { get; }

    public AppNotFoundException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }
}

public class AppValidationException : ArgumentException, IHasAppError
{
    public Error Error { get; }

    public AppValidationException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }
}

public class AppConflictException : InvalidOperationException, IHasAppError
{
    public Error Error { get; }

    public AppConflictException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }
}

public class AppForbiddenException : UnauthorizedAccessException, IHasAppError
{
    public Error Error { get; }

    public AppForbiddenException(Error error, Exception? innerException = null)
        : base(error.Description, innerException)
    {
        Error = error;
    }
}
