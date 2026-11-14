using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Time;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>
/// library.db (SQLite): clips, episodes, uploads and scraped links. The schema matches the
/// earlier Python version, so an existing library keeps working.
/// </summary>
public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS episodes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE COLLATE NOCASE,
            target_length REAL NOT NULL,
            state TEXT NOT NULL DEFAULT 'open',
            output_path TEXT,
            created_at TEXT NOT NULL,
            built_at TEXT
        );
        CREATE TABLE IF NOT EXISTS clips (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL UNIQUE COLLATE NOCASE,
            source TEXT NOT NULL,
            source_url TEXT NOT NULL,
            start REAL NOT NULL,
            "end" REAL NOT NULL,
            duration REAL NOT NULL,
            file_path TEXT NOT NULL,
            page_url TEXT,
            created_at TEXT NOT NULL,
            episode_id INTEGER REFERENCES episodes(id),
            position INTEGER
        );
        CREATE TABLE IF NOT EXISTS uploads (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            kind TEXT NOT NULL,
            ref TEXT NOT NULL,
            local_path TEXT NOT NULL,
            storage TEXT NOT NULL,
            remote_path TEXT NOT NULL,
            size INTEGER NOT NULL,
            mtime REAL NOT NULL,
            uploaded_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS scraped_links (
            url TEXT PRIMARY KEY,
            page_url TEXT NOT NULL,
            kind TEXT NOT NULL,
            found_at TEXT NOT NULL
        );
        """;

    private const string ClipColumns = """
        id AS Id, title AS Title, source AS Source, source_url AS SourceUrl, start AS Start, "end" AS EndTime,
        duration AS Duration, file_path AS FilePath, page_url AS PageUrl, created_at AS CreatedAt,
        episode_id AS EpisodeId, position AS Position
        """;

    private const string EpisodeColumns = """
        id AS Id, name AS Name, target_length AS TargetLength, state AS State, output_path AS OutputPath,
        created_at AS CreatedAt, built_at AS BuiltAt
        """;

    private readonly string _connectionString;
    private readonly TimeProvider _clock;

    public SqliteLibraryRepository(string databasePath, TimeProvider? clock = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
        _clock = clock ?? TimeProvider.System;
        using SqliteConnection connection = Open();
        connection.Execute(Schema);
    }

    // --- Clips --------------------------------------------------------------------------

    public Clip AddClip(NewClip clip)
    {
        using SqliteConnection connection = Open();
        long id = connection.ExecuteScalar<long>(
            """
            INSERT INTO clips (title, source, source_url, start, "end", duration, file_path, page_url, created_at)
            VALUES (@Title, @Source, @SourceUrl, @Start, @End, @Duration, @FilePath, @PageUrl, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                Title = clip.Title.Value,
                clip.Source,
                clip.SourceUrl,
                Start = clip.Span.Start.Seconds,
                End = clip.Span.End.Seconds,
                Duration = clip.Duration.Seconds,
                clip.FilePath,
                PageUrl = clip.PageUrl.GetValueOrDefault(),
                CreatedAt = Now(),
            });
        return GetClip(id);
    }

    public Clip ReplaceClip(long id, NewClip clip)
    {
        using (SqliteConnection connection = Open())
        {
            connection.Execute(
                """
                UPDATE clips
                SET title = @Title, source = @Source, source_url = @SourceUrl, start = @Start, "end" = @End,
                    duration = @Duration, file_path = @FilePath, page_url = @PageUrl, created_at = @CreatedAt
                WHERE id = @Id
                """,
                new
                {
                    Id = id,
                    Title = clip.Title.Value,
                    clip.Source,
                    clip.SourceUrl,
                    Start = clip.Span.Start.Seconds,
                    End = clip.Span.End.Seconds,
                    Duration = clip.Duration.Seconds,
                    clip.FilePath,
                    PageUrl = clip.PageUrl.GetValueOrDefault(),
                    CreatedAt = Now(),
                });
        }

        return GetClip(id);
    }

    public Maybe<Clip> FindClip(long id) => QueryClips("WHERE id = @id", new { id }).TryFirst();

    public Maybe<Clip> FindClipByTitle(ClipTitle title) => QueryClips("WHERE title = @title", new { title = title.Value }).TryFirst();

    public IReadOnlyList<Clip> ListClips() => QueryClips("ORDER BY id", null);

    public IReadOnlyList<Clip> EpisodeClips(long episodeId) =>
        QueryClips("WHERE episode_id = @episodeId ORDER BY position, id", new { episodeId });

    public void AttachClip(long clipId, long episodeId)
    {
        using SqliteConnection connection = Open();
        connection.Execute(
            """
            UPDATE clips
            SET episode_id = @episodeId,
                position = (SELECT COUNT(*) FROM clips WHERE episode_id = @episodeId) + 1
            WHERE id = @clipId
            """,
            new { clipId, episodeId });
    }

    public void DetachClip(long clipId)
    {
        Clip clip = GetClip(clipId);
        using (SqliteConnection connection = Open())
        {
            connection.Execute("UPDATE clips SET episode_id = NULL, position = NULL WHERE id = @clipId", new { clipId });
        }

        clip.EpisodeId.Tap(episodeId => Reorder(episodeId, EpisodeClips(episodeId).Select(c => c.Id).ToList()));
    }

    public void Reorder(long episodeId, IReadOnlyList<long> clipIds)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        for (int index = 0; index < clipIds.Count; index++)
        {
            connection.Execute(
                "UPDATE clips SET position = @position WHERE id = @id AND episode_id = @episodeId",
                new { position = index + 1, id = clipIds[index], episodeId },
                transaction);
        }

        transaction.Commit();
    }

    // --- Episodes -----------------------------------------------------------------------

    public Episode CreateEpisode(EpisodeName name, VideoTime targetLength)
    {
        using SqliteConnection connection = Open();
        long id = connection.ExecuteScalar<long>(
            "INSERT INTO episodes (name, target_length, created_at) VALUES (@name, @targetLength, @createdAt); SELECT last_insert_rowid();",
            new { name = name.Value, targetLength = targetLength.Seconds, createdAt = Now() });
        return GetEpisode(id);
    }

    public Maybe<Episode> FindEpisode(long id) => QueryEpisodes("WHERE id = @id", new { id }).TryFirst();

    public Maybe<Episode> FindEpisode(EpisodeName name) => QueryEpisodes("WHERE name = @name", new { name = name.Value }).TryFirst();

    public IReadOnlyList<Episode> ListEpisodes() => QueryEpisodes("ORDER BY id", null);

    public Maybe<Episode> LatestOpenEpisode() => QueryEpisodes("WHERE state = 'open' ORDER BY id DESC LIMIT 1", null).TryFirst();

    public Episode MarkBuilt(long episodeId, string outputPath)
    {
        using (SqliteConnection connection = Open())
        {
            connection.Execute(
                "UPDATE episodes SET state = 'built', output_path = @outputPath, built_at = @builtAt WHERE id = @episodeId",
                new { outputPath, builtAt = Now(), episodeId });
        }

        return GetEpisode(episodeId);
    }

    // --- Uploads ------------------------------------------------------------------------

    public void RecordUpload(UploadKind kind, string reference, string localPath, string storage, string remotePath)
    {
        (string fullPath, long size, double modified) = Describe(localPath);
        using SqliteConnection connection = Open();
        connection.Execute(
            """
            INSERT INTO uploads (kind, ref, local_path, storage, remote_path, size, mtime, uploaded_at)
            VALUES (@kind, @reference, @fullPath, @storage, @remotePath, @size, @modified, @uploadedAt)
            """,
            new { kind = UploadRow.KindText(kind), reference, fullPath, storage, remotePath, size, modified, uploadedAt = Now() });
    }

    public Maybe<UploadRecord> FindUpload(string localPath, string storage, string remotePath)
    {
        (string fullPath, long size, double modified) = Describe(localPath);
        using SqliteConnection connection = Open();
        return connection.QueryFirstOrDefault<UploadRow>(
            """
            SELECT id AS Id, kind AS Kind, ref AS Reference, local_path AS LocalPath, storage AS Storage,
                   remote_path AS RemotePath, size AS Size, uploaded_at AS UploadedAt
            FROM uploads
            WHERE local_path = @fullPath AND storage = @storage AND remote_path = @remotePath
              AND size = @size AND abs(mtime - @modified) < 0.001
            ORDER BY id DESC LIMIT 1
            """,
            new { fullPath, storage, remotePath, size, modified }).AsMaybe().Map(row => row.ToRecord());
    }

    // --- Scraped links --------------------------------------------------------------------

    public void RememberLinks(IEnumerable<ScrapedLink> links)
    {
        using SqliteConnection connection = Open();
        string foundAt = Now();
        connection.Execute(
            "INSERT OR REPLACE INTO scraped_links (url, page_url, kind, found_at) VALUES (@VideoUrl, @PageUrl, @Kind, @foundAt)",
            links.Select(link => new { link.VideoUrl, link.PageUrl, link.Kind, foundAt }));
    }

    public Maybe<string> RefererFor(string videoUrl)
    {
        using SqliteConnection connection = Open();
        return connection.QueryFirstOrDefault<string>("SELECT page_url FROM scraped_links WHERE url = @videoUrl", new { videoUrl }).AsMaybe();
    }

    // --- Helpers -------------------------------------------------------------------------

    /// <summary>Only for IDs the database just gave us; a missing one means the database changed under us.</summary>
    private Clip GetClip(long id) =>
        FindClip(id).GetValueOrThrow($"Clip {id} disappeared from {_connectionString}.");

    private Episode GetEpisode(long id) =>
        FindEpisode(id).GetValueOrThrow($"Episode {id} disappeared from {_connectionString}.");

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA foreign_keys = ON");
        return connection;
    }

    private List<Clip> QueryClips(string where, object? parameters)
    {
        using SqliteConnection connection = Open();
        return connection.Query<ClipRow>($"SELECT {ClipColumns} FROM clips {where}", parameters).Select(row => row.ToRecord()).ToList();
    }

    private List<Episode> QueryEpisodes(string where, object? parameters)
    {
        using SqliteConnection connection = Open();
        return connection.Query<EpisodeRow>($"SELECT {EpisodeColumns} FROM episodes {where}", parameters).Select(row => row.ToRecord()).ToList();
    }

    private string Now() => _clock.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss+00:00", CultureInfo.InvariantCulture);

    /// <summary>Full path, size and modified time (seconds since 1970, like Python's st_mtime).</summary>
    private static (string FullPath, long Size, double Modified) Describe(string path)
    {
        var file = new FileInfo(path);
        return (file.FullName, file.Length, (file.LastWriteTimeUtc - DateTime.UnixEpoch).TotalSeconds);
    }

    // Row classes: Dapper fills these from query columns, then they become the immutable records.
#pragma warning disable CA1812 // Created by Dapper.
    private sealed class ClipRow
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string SourceUrl { get; set; } = string.Empty;

        public double Start { get; set; }

        public double EndTime { get; set; }

        public double Duration { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public string? PageUrl { get; set; }

        public string CreatedAt { get; set; } = string.Empty;

        public long? EpisodeId { get; set; }

        public long? Position { get; set; }

        public Clip ToRecord() =>
            new(
                Id,
                ClipTitle.FromStored(Title),
                Source,
                SourceUrl,
                ClipTimeSpan.Create(VideoTime.FromTrusted(Math.Max(Start, 0)), VideoTime.FromTrusted(Math.Max(EndTime, 0)))
                    .ValueOrThrow(error => new InvalidDataException($"Clip {Id} in library.db: {error.Message}")),
                VideoTime.FromTrusted(Math.Max(Duration, 0)),
                FilePath,
                Maybe.From(PageUrl),
                CreatedAt,
                EpisodeId is { } episodeId ? episodeId : Maybe<long>.None,
                Position is { } position ? (int)position : Maybe<int>.None);
    }

    private sealed class EpisodeRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public double TargetLength { get; set; }

        public string State { get; set; } = "open";

        public string? OutputPath { get; set; }

        public string CreatedAt { get; set; } = string.Empty;

        public string? BuiltAt { get; set; }

        public Episode ToRecord() =>
            new(
                Id,
                EpisodeName.FromTrusted(Name),
                VideoTime.FromTrusted(Math.Max(TargetLength, 0)),
                State == "built" ? EpisodeState.Built : EpisodeState.Open,
                Maybe.From(OutputPath),
                CreatedAt,
                Maybe.From(BuiltAt));
    }

    private sealed class UploadRow
    {
        public long Id { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public string LocalPath { get; set; } = string.Empty;

        public string Storage { get; set; } = string.Empty;

        public string RemotePath { get; set; } = string.Empty;

        public long Size { get; set; }

        public string UploadedAt { get; set; } = string.Empty;

        /// <summary>How the kind is stored: "clip" or "episode" (the Python version's values).</summary>
        public static string KindText(UploadKind kind) => kind == UploadKind.Episode ? "episode" : "clip";

        public UploadRecord ToRecord() =>
            new(Id, Kind == "episode" ? UploadKind.Episode : UploadKind.Clip, Reference, LocalPath, Storage, RemotePath, ByteSize.FromBytes(Math.Max(Size, 0)), UploadedAt);
    }
#pragma warning restore CA1812
}
