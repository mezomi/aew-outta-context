namespace OuttaContext.Core.Storage;

/// <summary>Where an upload ended up, e.g. "drive:AEW Outta Context/clips/Table spot.mp4".</summary>
public sealed record UploadedFile(string Location);
