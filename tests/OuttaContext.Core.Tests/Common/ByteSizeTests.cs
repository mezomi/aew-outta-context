using OuttaContext.Core.Common;

namespace OuttaContext.Core.Tests.Common;

public sealed class ByteSizeTests
{
    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(12_900_000, "12.3 MB")]
    public void ToString_Size_UsesTheLargestFittingUnit(long bytes, string expected)
    {
        ByteSize.FromBytes(bytes).ToString().ShouldBe(expected);
    }

    [Fact]
    public void FromBytes_Negative_ThrowsBecauseItIsABug()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ByteSize.FromBytes(-1));
    }
}
