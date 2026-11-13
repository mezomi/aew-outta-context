using OuttaContext.Core.Clips;
using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Tests.Clips;

public sealed class ClipTitleTests
{
    [Theory]
    [InlineData("He said what", "He said what")]
    [InlineData("a<b>c:d\"e/f\\g|h?i*j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("ends with dots...", "ends with dots")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("CON", "CON_")]
    [InlineData("com1.txt", "com1.txt_")]
    [InlineData("emoji 🔥 ok", "emoji 🔥 ok")]
    public void ToFileName_Title_ReturnsWindowsSafeName(string title, string expected)
    {
        ClipTitle.ToFileName(title).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("...")]
    public void ToFileName_NoUsableCharacters_ThrowsInvalidTitle(string title)
    {
        Should.Throw<InvalidTitleException>(() => ClipTitle.ToFileName(title));
    }

    [Fact]
    public void ToFileName_VeryLongTitle_IsCutToTheLimit()
    {
        ClipTitle.ToFileName(new string('x', 500)).Length.ShouldBe(ClipTitle.MaxFileNameLength);
    }

    [Fact]
    public void Clean_ExtraSpaces_AreCollapsed()
    {
        ClipTitle.Clean(" Table   spot ").ShouldBe("Table spot");
    }
}
