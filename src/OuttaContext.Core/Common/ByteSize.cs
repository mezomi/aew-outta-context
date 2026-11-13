using System.Globalization;

namespace OuttaContext.Core.Common;

/// <summary>A file size in bytes. Never negative. Shown as <c>12.3 MB</c>.</summary>
public sealed class ByteSize : ComparableValueObject
{
    public static readonly ByteSize Zero = new(0);

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    private ByteSize(long bytes)
    {
        Bytes = bytes;
    }

    public long Bytes { get; }

    public static ByteSize FromBytes(long bytes) =>
        bytes >= 0 ? new ByteSize(bytes) : throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "A size can't be negative.");

    public static ByteSize OfFile(string path) => FromBytes(new FileInfo(path).Length);

    public override string ToString()
    {
        double value = Bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Bytes} B")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {Units[unit]}");
    }

    protected override IEnumerable<IComparable> GetComparableEqualityComponents()
    {
        yield return Bytes;
    }
}
