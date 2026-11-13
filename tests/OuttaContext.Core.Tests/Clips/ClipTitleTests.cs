using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.TestKit.Assertions;

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
    public void Create_Title_HasWindowsSafeFileName(string title, string expected)
    {
        ClipTitle.Create(title).ShouldSucceed().FileName.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "clip.title_empty")]
    [InlineData("   ", "clip.title_empty")]
    [InlineData("???", "clip.title_unusable")]
    [InlineData("...", "clip.title_unusable")]
    public void Create_NoUsableCharacters_Fails(string title, string code)
    {
        ClipTitle.Create(title).ShouldFailWith(code);
    }

    [Fact]
    public void Create_VeryLongTitle_FileNameIsCutToTheLimit()
    {
        ClipTitle.Create(new string('x', 500)).ShouldSucceed().FileName.Length.ShouldBe(FileNames.MaxLength);
    }

    [Fact]
    public void Create_ExtraSpaces_AreCollapsed()
    {
        ClipTitle.Create(" Table   spot ").ShouldSucceed().Value.ShouldBe("Table spot");
    }
}
