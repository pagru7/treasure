namespace Treasury.App.Common
{
    public record Result<T>
    {
        public bool IsSuccess { get; init; }
        public T? Value { get; init; }
        public string? ErrorMessage { get; init; }
        public static Result<T> Success(T value) => new Result<T> { IsSuccess = true, Value = value };
        public static Result<T> Failure(string errorMessage) => new Result<T> { IsSuccess = false, ErrorMessage = errorMessage };
    }

    public record Result<T, TError>
    {
        public bool IsSuccess { get; init; }
        public T? Value { get; init; }
        public TError? Error { get; init; }
        public static Result<T, TError> Success(T value) => new Result<T, TError> { IsSuccess = true, Value = value };
        public static Result<T, TError> Failure(TError error) => new Result<T, TError> { IsSuccess = false, Error = error };
    }
}