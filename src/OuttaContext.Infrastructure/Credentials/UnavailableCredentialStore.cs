using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Credentials;

/// <summary>Used where there is no supported keychain: nothing is stored.</summary>
/// <remarks>
/// Callers check <see cref="Describe"/> before saving a secret, so <see cref="Set"/> being called
/// anyway is a bug and throws.
/// </remarks>
public sealed class UnavailableCredentialStore : ICredentialStore
{
    public KeychainInfo Describe() => new(false, "not supported on this system");

    public Maybe<string> Get(string key) => Maybe<string>.None;

    public void Set(string key, string secret) =>
        throw StorageErrors.KeychainUnavailable("not supported on this system").ToException();

    public void Delete(string key)
    {
    }

    public static ICredentialStore ForThisSystem() =>
        OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600) ? new WindowsCredentialStore() : new UnavailableCredentialStore();
}
