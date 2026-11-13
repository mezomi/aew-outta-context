namespace OuttaContext.Core.Storage;

/// <summary>Which keychain is in use (or why there is none).</summary>
public sealed record KeychainInfo(bool Available, string Name);
