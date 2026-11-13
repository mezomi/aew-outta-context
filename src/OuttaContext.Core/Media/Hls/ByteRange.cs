using OuttaContext.Core.Common;

namespace OuttaContext.Core.Media.Hls;

/// <summary>A byte range inside a file, both ends included: <c>First ≤ Last</c>, never negative.</summary>
public sealed class ByteRange : ValueObject
{
    private ByteRange(long first, long last)
    {
        First = first;
        Last = last;
    }

    public long First { get; }

    public long Last { get; }

    public long Length => Last - First + 1;

    /// <summary>The end as an exclusive offset (what yt-dlp's fragment list expects).</summary>
    public long EndExclusive => Last + 1;

    /// <summary>From HLS's <c>LENGTH@OFFSET</c> form.</summary>
    public static Result<ByteRange, Error> FromLength(long offset, long length) =>
        offset >= 0 && length > 0 ? new ByteRange(offset, offset + length - 1) : MediaErrors.BadByteRange($"{length}@{offset}");

    public override string ToString() => $"{First}-{Last}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return First;
        yield return Last;
    }
}
