using OuttaContext.Core.Common;

namespace OuttaContext.Core.Episodes;

/// <summary>An episode's name: single-spaced, never empty, and usable as the built file's name.</summary>
public sealed class EpisodeName : SimpleValueObject<string>
{
    private EpisodeName(string name, string fileName)
        : base(name)
    {
        FileName = fileName;
    }

    /// <summary>The Windows-safe file name of the built episode, without extension.</summary>
    public string FileName { get; }

    public static Result<EpisodeName, Error> Create(string text)
    {
        string name = FileNames.CollapseSpaces(text);
        if (name.Length == 0)
        {
            return EpisodeErrors.EmptyName();
        }

        return FileNames.Sanitize(name)
            .ToResult(EpisodeErrors.NameHasNoFileName(text))
            .Map(fileName => new EpisodeName(name, fileName));
    }

    /// <summary>For names the app makes itself ("Episode 4") or reads back from the library.</summary>
    public static EpisodeName FromTrusted(string text) =>
        Create(text).GetValueOrDefault() ?? new EpisodeName(text, text);

    public override string ToString() => Value;
}
