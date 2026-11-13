using System.Runtime.Versioning;
using Meziantou.Framework.Win32;
using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Credentials;

/// <summary>Windows Credential Manager. Entries are named "outta-context:KEY".</summary>
[SupportedOSPlatform("windows5.1.2600")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string Prefix = "outta-context:";

    public KeychainInfo Describe() => new(true, "Windows Credential Manager");

    public Maybe<string> Get(string key) => Maybe.From(CredentialManager.ReadCredential(Prefix + key)?.Password);

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
