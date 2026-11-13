namespace OuttaContext.Core.Storage;

/// <summary>The system keychain (Windows Credential Manager). The only place secrets are kept.</summary>
public interface ICredentialStore
{
    /// <summary>Whether a keychain is usable here, and which one.</summary>
    KeychainInfo Describe();

    Maybe<string> Get(string key);

    void Set(string key, string secret);

    void Delete(string key);
}
