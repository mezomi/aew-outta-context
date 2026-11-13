namespace OuttaContext.Infrastructure.Updates;

/// <summary>What `outta update` did to outta.exe.</summary>
public enum UpdateOutcome
{
    AlreadyUpToDate,
    UpdateAvailable,
    Updated,
}
