using OuttaContext.Core.Common;

namespace OuttaContext.TestKit.Assertions;

/// <summary>
/// Shouldly-style checks for <c>Result</c>s, so tests read like the rest:
/// <c>VideoTime.Parse("1:30").ShouldSucceed().Seconds.ShouldBe(90);</c>
/// <c>ClipTimeSpan.Parse("2-1").ShouldFailWith("range.end_before_start");</c>
/// </summary>
public static class ResultAssertions
{
    /// <summary>Fails the test if the result failed; otherwise returns its value.</summary>
    public static T ShouldSucceed<T>(this Result<T, Error> result)
    {
        if (result.IsFailure)
        {
            throw new ShouldAssertException($"Expected success but got {result.Error.Code}: {result.Error.Message}");
        }

        return result.Value;
    }

    public static void ShouldSucceed(this UnitResult<Error> result)
    {
        if (result.IsFailure)
        {
            throw new ShouldAssertException($"Expected success but got {result.Error.Code}: {result.Error.Message}");
        }
    }

    /// <summary>Fails the test unless the result failed with this error code; returns the error.</summary>
    public static Error ShouldFailWith<T>(this Result<T, Error> result, string code)
    {
        if (result.IsSuccess)
        {
            throw new ShouldAssertException($"Expected error {code} but it succeeded with {result.Value}");
        }

        result.Error.Code.ShouldBe(code, result.Error.Message);
        return result.Error;
    }

    public static Error ShouldFailWith(this UnitResult<Error> result, string code)
    {
        if (result.IsSuccess)
        {
            throw new ShouldAssertException($"Expected error {code} but it succeeded");
        }

        result.Error.Code.ShouldBe(code, result.Error.Message);
        return result.Error;
    }

    /// <summary>The value, or null when there is none (to compare with an expected <c>double?</c> etc.).</summary>
    public static T? OrNull<T>(this Maybe<T> maybe)
        where T : struct => maybe.HasValue ? maybe.Value : null;

    /// <summary>The value, or null when there is none.</summary>
    public static T? OrNullReference<T>(this Maybe<T> maybe)
        where T : class => maybe.HasValue ? maybe.Value : null;

    /// <summary>Fails the test if the maybe is empty; otherwise returns its value.</summary>
    public static T ShouldHaveValue<T>(this Maybe<T> maybe)
    {
        if (maybe.HasNoValue)
        {
            throw new ShouldAssertException($"Expected a {typeof(T).Name} but there was none");
        }

        return maybe.Value;
    }
}
