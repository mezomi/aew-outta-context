using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>A storage that needs a one-time sign-in (browser login or an API key).</summary>
public interface ISignInStorage : IStorageBackend
{
    /// <param name="askSecret">Asks the user for a secret (hidden input), e.g. a Backblaze key.</param>
    Task<UnitResult<Error>> SignInAsync(Func<string, string> askSecret, CancellationToken cancellationToken);
}
