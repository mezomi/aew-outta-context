using OuttaContext.Core.Errors;
using OuttaContext.Core.Results;

namespace OuttaContext.Core.Tests.Results;

public sealed class ToolResultTests
{
    private static readonly ToolError Error = new("yt-dlp", "Video unavailable", "Check the link.");

    [Fact]
    public void Success_HasValueAndNoError()
    {
        var result = ToolResult<int>.Success(42);

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBe(42);
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_HasErrorAndNoValue()
    {
        ToolResult<int> result = Error;

        result.Failed.ShouldBeTrue();
        result.Error.ShouldBe(Error);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ValueOrThrow_Failure_ThrowsOuttaExceptionWithMessageAndHint()
    {
        ToolResult<int> result = Error;

        var exception = Should.Throw<ToolFailedException>(() => result.ValueOrThrow());

        exception.ShouldBeAssignableTo<OuttaException>();
        exception.Message.ShouldBe("Video unavailable");
        exception.Hint.ShouldBe("Check the link.");
    }

    [Fact]
    public void Map_Success_TransformsValue_Failure_PassesErrorThrough()
    {
        ToolResult<int>.Success(2).Map(n => n * 10).Value.ShouldBe(20);
        ToolResult<int>.Failure(Error).Map(n => n * 10).Error.ShouldBe(Error);
    }
}
