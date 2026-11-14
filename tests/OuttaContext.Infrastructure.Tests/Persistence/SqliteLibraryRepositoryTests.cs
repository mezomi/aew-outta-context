using Microsoft.Data.Sqlite;
using OuttaContext.Core.Library;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class SqliteLibraryRepositoryTests : IDisposable
{
    private static readonly VideoTime TenMinutes = VideoTime.FromTrusted(600);

    private readonly TempDirectory _temp = new();
    private readonly SqliteLibraryRepository _repository;

    public SqliteLibraryRepositoryTests()
    {
        _repository = new SqliteLibraryRepository(_temp.File("library.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _temp.Dispose();
    }

    [Fact]
    public void AddClip_Clip_IsSavedWithAllFields()
    {
        var newClip = new NewClip(
            Values.Title("Table spot"), "youtube", "https://y", Values.Span("45-65"), Values.Time(20), "/c/Table spot.mp4", "https://page");

        Clip clip = _repository.AddClip(newClip);

        Clip loaded = _repository.FindClip(clip.Id).ShouldHaveValue();
        (loaded.Title.Value, loaded.Span.Start.Seconds, loaded.Span.End.Seconds, loaded.Duration.Seconds, loaded.PageUrl.GetValueOrDefault())
            .ShouldBe(("Table spot", 45.0, 65.0, 20.0, "https://page"));
        loaded.EpisodeId.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void ReplaceClip_ClipInEpisode_KeepsIdAndPosition()
    {
        Episode episode = _repository.CreateEpisode(Values.Episode("Episode 1"), TenMinutes);
        Clip old = _repository.AddClip(Values.NewClip("Steve", 8));
        _repository.AttachClip(old.Id, episode.Id);

        Clip replaced = _repository.ReplaceClip(old.Id, Values.NewClip("Steve fixed", 3, "/c/Steve fixed.mp4"));

        (replaced.Id, replaced.Title.Value, replaced.Duration.Seconds, replaced.FilePath).ShouldBe((old.Id, "Steve fixed", 3.0, "/c/Steve fixed.mp4"));
        (replaced.EpisodeId.GetValueOrDefault(), replaced.Position.GetValueOrDefault()).ShouldBe((episode.Id, 1));
        _repository.ListClips().ShouldHaveSingleItem();
    }

    [Fact]
    public void FindClip_UnknownId_IsNone()
    {
        _repository.FindClip(99).HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void FindClipByTitle_DifferentCase_FindsIt()
    {
        _repository.AddClip(Values.NewClip("Table spot"));

        _repository.FindClipByTitle(Values.Title("TABLE SPOT")).HasValue.ShouldBeTrue();
    }

    [Fact]
    public void AttachAndReorder_Clips_KeepTheirPositions()
    {
        Episode episode = _repository.CreateEpisode(Values.Episode("Episode 1"), TenMinutes);
        long[] ids = [.. new[] { "a", "b", "c" }.Select(t => _repository.AddClip(Values.NewClip(t)).Id)];
        foreach (long id in ids)
        {
            _repository.AttachClip(id, episode.Id);
        }

        _repository.Reorder(episode.Id, [ids[2], ids[0], ids[1]]);
        _repository.DetachClip(ids[0]);

        _repository.EpisodeClips(episode.Id).Select(c => (c.Title.Value, c.Position.GetValueOrDefault())).ShouldBe([("c", 1), ("b", 2)]);
    }

    [Fact]
    public void FindEpisode_DifferentCase_FindsIt()
    {
        _repository.CreateEpisode(Values.Episode("Episode 1"), TenMinutes);

        _repository.FindEpisode(Values.Episode("episode 1")).HasValue.ShouldBeTrue();
    }

    [Fact]
    public void MarkBuilt_Episode_IsNoLongerOpen()
    {
        Episode episode = _repository.CreateEpisode(Values.Episode("Episode 1"), TenMinutes);

        Episode built = _repository.MarkBuilt(episode.Id, "/out/Episode 1.mp4");

        built.IsBuilt.ShouldBeTrue();
        built.OutputPath.ShouldHaveValue().ShouldBe("/out/Episode 1.mp4");
        _repository.LatestOpenEpisode().HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void FindUpload_FileChangedSinceUpload_IsNotFound()
    {
        string file = _temp.Write("a.mp4", "abc");
        _repository.RecordUpload(UploadKind.Clip, "1", file, "drive", "a.mp4");
        _repository.FindUpload(file, "drive", "a.mp4").ShouldHaveValue().Kind.ShouldBe(UploadKind.Clip);

        File.WriteAllText(file, "changed!");

        _repository.FindUpload(file, "drive", "a.mp4").HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void RefererFor_ScrapedLink_ReturnsThePage()
    {
        _repository.RememberLinks([new ScrapedLink("https://v/1", "https://page", "embed")]);

        _repository.RefererFor("https://v/1").ShouldHaveValue().ShouldBe("https://page");
        _repository.RefererFor("https://v/2").HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void Open_DatabaseCreatedByThePythonVersion_ReadsItsEpisodesAndClips()
    {
        // Arrange: rows written the way the Python version wrote them
        string path = _temp.File("python-library.db");
        _ = new SqliteLibraryRepository(path);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO episodes (name, target_length, created_at) VALUES ('Episode 1', 600.0, '2026-11-13T02:00:00+00:00');
                INSERT INTO clips (title, source, source_url, start, "end", duration, file_path, page_url, created_at, episode_id, position)
                VALUES ('Steve the Clown', 'dailymotion', 'https://www.dailymotion.com/video/k1oj', 2730.0, 2738.0, 8.0,
                        'C:\Clips\Steve the Clown.mp4', NULL, '2026-11-13T02:01:00+00:00', 1, 1);
                INSERT INTO uploads (kind, ref, local_path, storage, remote_path, size, mtime, uploaded_at)
                VALUES ('episode', 'Episode 1', '/x.mp4', 'drive', 'Episode 1/x.mp4', 10, 0, 'then');
                """;
            insert.ExecuteNonQuery();
        }

        // Act
        var repository = new SqliteLibraryRepository(path);

        // Assert
        Episode episode = repository.LatestOpenEpisode().ShouldHaveValue();
        episode.TargetLength.Seconds.ShouldBe(600);
        Clip clip = repository.EpisodeClips(episode.Id).ShouldHaveSingleItem();
        (clip.Title.Value, clip.Span.Start.Seconds, clip.Duration.Seconds, clip.Position.GetValueOrDefault()).ShouldBe(("Steve the Clown", 2730.0, 8.0, 1));
        clip.PageUrl.HasNoValue.ShouldBeTrue();
    }
}
