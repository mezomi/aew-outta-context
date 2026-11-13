using OuttaContext.Core.Storage;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Storage;

public sealed class StorageProviderTests
{
    [Theory]
    [InlineData("gdrive", "Google Drive", true)]
    [InlineData("LOCAL", "Local folder", false)]
    [InlineData("b2", "Backblaze B2", true)]
    public void Parse_KnownProvider_HasLabelAndRcloneUse(string text, string label, bool usesRclone)
    {
        StorageProvider provider = StorageProvider.Parse(text).ShouldSucceed();

        provider.Label.ShouldBe(label);
        provider.UsesRclone.ShouldBe(usesRclone);
    }

    [Fact]
    public void Parse_UnknownProvider_FailsListingTheChoices()
    {
        StorageProvider.Parse("icloud").ShouldFailWith("storage.unknown_provider").Hint.ShouldContain("gdrive");
    }

    [Fact]
    public void SignsInWithBrowser_Backblaze_UsesAKeyInstead()
    {
        StorageProvider.Backblaze.SignsInWithBrowser.ShouldBeFalse();
    }
}
