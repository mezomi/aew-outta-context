using System.Text.RegularExpressions;
using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>The name of a configured storage: letters, numbers, - and _, up to 32 characters.</summary>
public sealed partial class StorageName : SimpleValueObject<string>
{
    private StorageName(string value)
        : base(value)
    {
    }

    public static Result<StorageName, Error> Create(string text)
    {
        string name = text.Trim();
        return StorageNameRegex().IsMatch(name) ? new StorageName(name) : StorageErrors.InvalidName(text);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$")]
    private static partial Regex StorageNameRegex();
}
