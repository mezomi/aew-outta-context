namespace OuttaContext.Core.Doctor;

/// <summary>Things about this computer the doctor needs to look up.</summary>
public interface IEnvironmentInspector
{
    DateOnly Today { get; }

    /// <summary>Full path of a program on PATH (or downloaded by the app).</summary>
    Maybe<string> FindProgram(string name);

    /// <summary>yt-dlp's version, e.g. "2026.10.26", or none if it isn't installed.</summary>
    Task<Maybe<string>> GetYtDlpVersionAsync(CancellationToken cancellationToken);

    /// <summary>The browsers yt-dlp can pose as (e.g. chrome, firefox).</summary>
    Task<IReadOnlyList<string>> GetImpersonationClientsAsync(CancellationToken cancellationToken);
}
