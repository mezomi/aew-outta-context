using Meziantou.Framework.Win32;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Credentials;

/// <summary>Windows Credential Manager. Entries are named "outta-context:KEY".</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows5.1.2600")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string Prefix = "outta-context:";

    public (bool Available, string Name) Describe() => (true, "Windows Credential Manager");

    public string? Get(string key) => CredentialManager.ReadCredential(Prefix + key)?.Password;

    public void Set(string key, string secret) =>
        CredentialManager.WriteCredential(Prefix + key, key, secret, CredentialPersistence.LocalMachine);

    public void Delete(string key)
    {
        if (CredentialManager.ReadCredential(Prefix + key) is not null)
        {
            CredentialManager.DeleteCredential(Prefix + key);
        }
    }
}

/// <summary>Used where there is no supported keychain: nothing is stored, and saving explains why.</summary>
public sealed class UnavailableCredentialStore : ICredentialStore
{
    public (bool Available, string Name) Describe() => (false, "not supported on this system");

    public string? Get(string key) => null;

    public void Set(string key, string secret) =>
        throw new Core.Errors.OuttaException(
            "No system keychain is available to store this secret.", "Cloud storage logins need Windows Credential Manager.");

    public void Delete(string key)
    {
    }

    public static ICredentialStore ForThisSystem() =>
        OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600) ? new WindowsCredentialStore() : new UnavailableCredentialStore();
}
