using OuttaContext.Core.Library;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Episodes;

/// <summary>An episode with its clips, plus the totals shown by `outta episode status`.</summary>
public sealed record EpisodeStatus(Episode Episode, IReadOnlyList<Clip> Clips)
{
    public VideoTime Total => VideoTime.FromTrusted(Clips.Sum(clip => clip.Duration.Seconds));

    public VideoTime Remaining => Episode.TargetLength.MinusOrZero(Total);

    public bool IsReady => Total.IsAtLeast(Episode.TargetLength);

    /// <summary>"Episode 1": 0:08 of 10:00 (9:52 left).</summary>
    public string Describe()
    {
        string line = $"\"{Episode.Name}\": {Total} of {Episode.TargetLength}";
        return IsReady ? line + " (ready to build)" : line + $" ({Remaining} left)";
    }
}
