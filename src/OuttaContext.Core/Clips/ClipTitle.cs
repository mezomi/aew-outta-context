using OuttaContext.Core.Common;

namespace OuttaContext.Core.Clips;

/// <summary>
/// A clip's title: trimmed, single-spaced, never empty, and always usable as a file name.
/// The title is also the clip's file name (<see cref="FileName"/>), so titles must be unique.
/// </summary>
public sealed class ClipTitle : SimpleValueObject<string>
{
    private ClipTitle(string title, string fileName)
        : base(title)
    {
        FileName = fileName;
    }

    /// <summary>The Windows-safe file name, without extension (illegal characters become <c>_</c>).</summary>
    public string FileName { get; }

    public static Result<ClipTitle, Error> Create(string text)
    {
        string title = FileNames.CollapseSpaces(text);
        if (title.Length == 0)
        {
            return ClipErrors.EmptyTitle();
        }

        return FileNames.Sanitize(title)
            .ToResult(ClipErrors.TitleHasNoFileName(text))
            .Map(fileName => new ClipTitle(title, fileName));
    }

    /// <summary>For titles read back from the library, which were checked when saved.</summary>
    public static ClipTitle FromStored(string text) =>
        Create(text).GetValueOrDefault() ?? new ClipTitle(text, text);

    public override string ToString() => Value;
}
