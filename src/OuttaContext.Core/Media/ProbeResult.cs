using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>What ffprobe says about a video file. Width, height and frame rate are missing for audio-only files.</summary>
public sealed record ProbeResult(VideoTime Duration, int? Width, int? Height, double? Fps, bool HasAudio);
