using OuttaContext.Core.Common;

namespace OuttaContext.Core.Library;

/// <summary>A file that was uploaded, so the same unchanged file is not uploaded twice.</summary>
/// <param name="Reference">The clip ID or episode name.</param>
public sealed record UploadRecord(
    long Id, UploadKind Kind, string Reference, string LocalPath, string Storage, string RemotePath, ByteSize Size, string UploadedAt);
