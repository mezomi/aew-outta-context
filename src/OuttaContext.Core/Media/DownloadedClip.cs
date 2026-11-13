using OuttaContext.Core.Common;

namespace OuttaContext.Core.Media;

/// <summary>A clip written to disk.</summary>
public sealed record DownloadedClip(string Path, ByteSize Size);
