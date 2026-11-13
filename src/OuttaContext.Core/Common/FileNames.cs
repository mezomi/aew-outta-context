using System.Text.RegularExpressions;

namespace OuttaContext.Core.Common;

/// <summary>Turns names into file names that are safe on Windows (and everywhere else).</summary>
public static partial class FileNames
{
    public const int MaxLength = 120;

    private static readonly HashSet<string> ReservedWindowsNames = BuildReservedNames();

    /// <summary>Illegal characters become <c>_</c>; no trailing dots or spaces; reserved names get a <c>_</c>.</summary>
    /// <returns>The safe name, or <see cref="Maybe{T}.None"/> if nothing usable is left.</returns>
    public static Maybe<string> Sanitize(string name)
    {
        string safe = IllegalCharsRegex().Replace(name, "_");
        if (safe.Length > MaxLength)
        {
            safe = safe[..MaxLength];
        }

        safe = safe.TrimEnd(' ', '.');
        if (safe.Trim('_', ' ').Length == 0)
        {
            return Maybe<string>.None;
        }

        return ReservedWindowsNames.Contains(safe.Split('.')[0].ToUpperInvariant()) ? safe + "_" : safe;
    }

    /// <summary>Trims and collapses runs of whitespace to single spaces.</summary>
    public static string CollapseSpaces(string text) => WhitespaceRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1f]")]
    private static partial Regex IllegalCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

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
