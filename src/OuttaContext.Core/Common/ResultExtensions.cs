namespace OuttaContext.Core.Common;

/// <summary>Small helpers on top of CSharpFunctionalExtensions.</summary>
public static class ResultExtensions
{
    /// <summary>Keeps only whether it worked: drops the value, keeps the error.</summary>
    public static UnitResult<Error> DropValue<T>(this Result<T, Error> result) =>
        result.IsSuccess ? UnitResult.Success<Error>() : result.Error;

    /// <summary>
    /// The value, or throws an <see cref="ErrorException"/>. Only for failures nothing can recover
    /// from (a corrupt data file); expected failures are returned, not thrown.
    /// </summary>
    public static T ValueOrThrow<T>(this Result<T, Error> result) =>
        result.IsSuccess ? result.Value : throw result.Error.ToException();

    /// <summary>Like <see cref="ValueOrThrow{T}(Result{T, Error})"/>, with the exception of your choice.</summary>
    public static T ValueOrThrow<T>(this Result<T, Error> result, Func<Error, Exception> exception) =>
        result.IsSuccess ? result.Value : throw exception(result.Error);

    /// <summary>The value if it worked, otherwise none (the error is not needed).</summary>
    public static Maybe<T> ValueOrNone<T>(this Result<T, Error> result) =>
        result.IsSuccess ? result.Value : Maybe<T>.None;
}
