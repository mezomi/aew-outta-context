using System.Globalization;
using OuttaContext.Core.Common;

namespace OuttaContext.Core.Settings;

/// <summary>The highest video height to download: one of 360, 480, 720, 1080, 1440 or 2160.</summary>
public sealed class VideoQuality : ComparableValueObject
{
    public static readonly VideoQuality Default = new(1080);

    public static readonly IReadOnlyList<int> All = [360, 480, 720, 1080, 1440, 2160];

    private VideoQuality(int height)
    {
        Height = height;
    }

    public int Height { get; }

    /// <summary>Reads <c>1080</c> or <c>1080p</c>.</summary>
    public static Result<VideoQuality, Error> Parse(string text)
    {
        string digits = text.Trim().ToLowerInvariant().TrimEnd('p');
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            ? FromHeight(height).MapError(_ => SettingsErrors.UnsupportedQuality(text))
            : SettingsErrors.UnsupportedQuality(text);
    }

    public static Result<VideoQuality, Error> FromHeight(int height) =>
        All.Contains(height) ? new VideoQuality(height) : SettingsErrors.UnsupportedQuality(height.ToString(CultureInfo.InvariantCulture));

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Height}p");

    protected override IEnumerable<IComparable> GetComparableEqualityComponents()
    {
        yield return Height;
    }
}
