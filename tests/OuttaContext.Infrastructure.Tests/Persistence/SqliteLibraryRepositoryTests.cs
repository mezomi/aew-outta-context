using Microsoft.Data.Sqlite;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class SqliteLibraryRepositoryTests : IDisposable
{
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
        Clip clip = _repository.AddClip(new NewClip("Table spot", "youtube", "https://y", 45, 65, 20, "/c/Table spot.mp4", "https://page"));

        Clip loaded = _repository.GetClip(clip.Id);
        (loaded.Title, loaded.Start, loaded.End, loaded.Duration, loaded.PageUrl).ShouldBe(("Table spot", 45.0, 65.0, 20.0, "https://page"));
        loaded.EpisodeId.ShouldBeNull();
    }

    [Fact]
    public void FindClipByTitle_DifferentCase_FindsIt()
    {
        _repository.AddClip(new NewClip("Table spot", "youtube", "u", 0, 1, 1, "/c.mp4", null));

        _repository.FindClipByTitle("TABLE SPOT").ShouldNotBeNull();
    }

    [Fact]
    public void AttachAndReorder_Clips_KeepTheirPositions()
    {
        Episode episode = _repository.CreateEpisode("Episode 1", 600);
        long[] ids = [.. new[] { "a", "b", "c" }.Select(t => _repository.AddClip(new NewClip(t, "youtube", "u", 0, 1, 1, $"/{t}.mp4", null)).Id)];
        foreach (long id in ids)
        {
            _repository.AttachClip(id, episode.Id);
        }

        _repository.Reorder(episode.Id, [ids[2], ids[0], ids[1]]);
        _repository.DetachClip(ids[0]);

        _repository.EpisodeClips(episode.Id).Select(c => (c.Title, c.Position)).ShouldBe([("c", (int?)1), ("b", 2)]);
    }

    [Fact]
    public void CreateEpisode_DuplicateName_IsRefused()
    {
        _repository.CreateEpisode("Episode 1", 600);

        Should.Throw<AlreadyExistsException>(() => _repository.CreateEpisode("episode 1", 600));
    }

    [Fact]
    public void MarkBuilt_Episode_IsNoLongerOpen()
    {
        Episode episode = _repository.CreateEpisode("Episode 1", 600);

        Episode built = _repository.MarkBuilt(episode.Id, "/out/Episode 1.mp4");

        built.IsBuilt.ShouldBeTrue();
        _repository.LatestOpenEpisode().ShouldBeNull();
    }

    [Fact]
    public void FindUpload_FileChangedSinceUpload_IsNotFound()
    {
        string file = _temp.Write("a.mp4", "abc");
        _repository.RecordUpload("clip", "1", file, "drive", "a.mp4");
        _repository.FindUpload(file, "drive", "a.mp4").ShouldNotBeNull();

        File.WriteAllText(file, "changed!");

        _repository.FindUpload(file, "drive", "a.mp4").ShouldBeNull();
    }

    [Fact]
    public void RefererFor_ScrapedLink_ReturnsThePage()
    {
        _repository.RememberLinks([("https://v/1", "https://page", "embed")]);

        _repository.RefererFor("https://v/1").ShouldBe("https://page");
        _repository.RefererFor("https://v/2").ShouldBeNull();
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
                """;
            insert.ExecuteNonQuery();
        }

        // Act
        var repository = new SqliteLibraryRepository(path);

        // Assert
        Clip clip = repository.EpisodeClips(repository.LatestOpenEpisode()!.Id).ShouldHaveSingleItem();
        (clip.Title, clip.Start, clip.Duration, clip.Position).ShouldBe(("Steve the Clown", 2730.0, 8.0, (int?)1));
    }
}
