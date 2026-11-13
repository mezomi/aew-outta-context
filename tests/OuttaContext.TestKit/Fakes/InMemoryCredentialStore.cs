using OuttaContext.Core.Storage;

namespace OuttaContext.TestKit.Fakes;

/// <summary>A keychain kept in memory.</summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    public bool Available { get; set; } = true;

    public (bool Available, string Name) Describe() => (Available, "in-memory");

    public string? Get(string key) => Secrets.GetValueOrDefault(key);

    public void Set(string key, string secret) => Secrets[key] = secret;

    public void Delete(string key) => Secrets.Remove(key);
}
