using System.Text.RegularExpressions;
using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Clips;

/// <summary>Clip titles and the Windows-safe file names made from them.</summary>
public static partial class ClipTitle
{
    public const int MaxFileNameLength = 120;

    private static readonly HashSet<string> ReservedWindowsNames = BuildReservedNames();

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1f]")]
    private static partial Regex IllegalFileNameCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>Trims and collapses spaces; an empty title is refused.</summary>
    public static string Clean(string title)
    {
        string cleaned = WhitespaceRegex().Replace(title, " ").Trim();
        return cleaned.Length == 0 ? throw new InvalidTitleException("The clip title is empty.") : cleaned;
    }

    /// <summary>File name (without extension) for a title: illegal characters become <c>_</c>.</summary>
    public static string ToFileName(string title)
    {
        string name = IllegalFileNameCharsRegex().Replace(Clean(title), "_");
        if (name.Length > MaxFileNameLength)
        {
            name = name[..MaxFileNameLength];
        }

        name = name.TrimEnd(' ', '.');
        if (name.Trim('_', ' ').Length == 0)
        {
            throw new InvalidTitleException($"\"{title}\" has no usable characters for a file name.");
        }

        string stem = name.Split('.')[0];
        return ReservedWindowsNames.Contains(stem.ToUpperInvariant()) ? name + "_" : name;
    }

    private static HashSet<string> BuildReservedNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "CON", "PRN", "AUX", "NUL" };
        for (int i = 1; i <= 9; i++)
        {
            names.Add($"COM{i}");
            names.Add($"LPT{i}");
        }

        return names;
    }
}
