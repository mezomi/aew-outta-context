using OuttaContext.Cli.Composition;

namespace OuttaContext.Cli.Tests.Composition;

public sealed class GlobalOptionsTests
{
    [Theory]
    [InlineData(new[] { "-v", "clip", "url" }, new[] { "clip", "url", "-v" })]
    [InlineData(new[] { "--quiet", "-v", "doctor" }, new[] { "doctor", "--quiet", "-v" })]
    [InlineData(new[] { "clip", "url", "-v" }, new[] { "clip", "url", "-v" })]
    [InlineData(new[] { "-v" }, new[] { "-v" })]
    public void MoveAfterCommand_LeadingFlags_MovedToTheEnd(string[] args, string[] expected)
    {
        GlobalOptions.MoveAfterCommand(args).ShouldBe(expected);
    }
}
