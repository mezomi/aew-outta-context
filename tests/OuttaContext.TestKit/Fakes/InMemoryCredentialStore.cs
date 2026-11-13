using OuttaContext.Core.Storage;

namespace OuttaContext.TestKit.Fakes;

/// <summary>A keychain kept in memory.</summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    public bool Available { get; set; } = true;

    public KeychainInfo Describe() => new(Available, "in-memory");

    public Maybe<string> Get(string key) => Secrets.TryGetValue(key, out string? secret) ? secret : Maybe<string>.None;

    public void Set(string key, string secret) => Secrets[key] = secret;

    public void Delete(string key) => Secrets.Remove(key);
}
