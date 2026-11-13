using OuttaContext.Core.Common;

namespace OuttaContext.Core.Tests.Common;

public sealed class ResultExtensionsTests
{
    private static readonly Error Problem = new("test.problem", "It broke.", "Fix it.");

    [Fact]
    public void ValueOrThrow_Failure_ThrowsErrorExceptionCarryingTheError()
    {
        Result<int, Error> result = Problem;

        var exception = Should.Throw<ErrorException>(() => result.ValueOrThrow());

        exception.Error.ShouldBe(Problem);
    }

    [Fact]
    public void ValueOrNone_Failure_IsNone()
    {
        Result<int, Error> result = Problem;

        result.ValueOrNone().HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void DropValue_Success_KeepsSuccess()
    {
        Result<int, Error> result = 5;

        result.DropValue().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void DropValue_Failure_KeepsTheError()
    {
        Result<int, Error> result = Problem;

        result.DropValue().Error.ShouldBe(Problem);
    }
}
