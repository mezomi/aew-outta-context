using OuttaContext.Core.Common;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Common;

public sealed class FileNamesTests
{
    [Theory]
    [InlineData("a:b", "a_b")]
    [InlineData("NUL", "NUL_")]
    [InlineData("trailing. ", "trailing")]
    public void Sanitize_Name_IsSafeOnWindows(string name, string expected)
    {
        FileNames.Sanitize(name).ShouldHaveValue().ShouldBe(expected);
    }

    [Theory]
    [InlineData("???")]
    [InlineData("")]
    public void Sanitize_NothingUsable_IsNone(string name)
    {
        FileNames.Sanitize(name).HasNoValue.ShouldBeTrue();
    }
}
