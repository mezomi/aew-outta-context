namespace OuttaContext.Infrastructure.Updates;

/// <summary>The outta executable that is running now (what `outta update` replaces).</summary>
public sealed record RunningExecutable(string Path)
{
    /// <summary>True when running as the published single-file exe (not from a development build).</summary>
    public static bool IsPublished => string.IsNullOrEmpty(typeof(RunningExecutable).Assembly.Location);

    public static RunningExecutable Current() => new(Environment.ProcessPath ?? "outta");
}
