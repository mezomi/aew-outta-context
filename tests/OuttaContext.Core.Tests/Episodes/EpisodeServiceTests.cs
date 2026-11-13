using OuttaContext.Core.Episodes;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Episodes;

public sealed class EpisodeServiceTests
{
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly EpisodeService _episodes;

    public EpisodeServiceTests()
    {
        _episodes = new EpisodeService(_repository, targetLength: 600);
    }

    [Fact]
    public void Current_FirstUse_CreatesEpisode1()
    {
        Episode episode = _episodes.Current();

        episode.Name.ShouldBe("Episode 1");
        episode.State.ShouldBe(Episode.Open);
        _episodes.Current().Id.ShouldBe(episode.Id);
    }

    [Fact]
    public void AddClip_SeveralClips_KeepsOrderAndTotals()
    {
        AddClip("a", 100);
        AddClip("b", 200);

        EpisodeStatus status = AddClip("c", 150);

        status.Clips.Select(c => c.Title).ShouldBe(["a", "b", "c"]);
        status.Clips.Select(c => c.Position).ShouldBe([1, 2, 3]);
        (status.Total, status.Remaining, status.IsReady).ShouldBe((450, 150, false));
    }

    [Fact]
    public void AddClip_PastTheTarget_IsAddedAndReady()
    {
        AddClip("a", 500);

        EpisodeStatus status = AddClip("b", 150);

        status.Total.ShouldBe(650);
        status.Remaining.ShouldBe(0);
        status.IsReady.ShouldBeTrue();
    }

    [Fact]
    public void Move_ClipToFront_RenumbersTheRest()
    {
        long[] ids = [.. new[] { "a", "b", "c", "d" }.Select(t => AddClip(t).Clips[^1].Id)];

        EpisodeStatus status = _episodes.Move(ids[3], 1);

        status.Clips.Select(c => c.Title).ShouldBe(["d", "a", "b", "c"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Move_PositionOutsideTheEpisode_Throws(int position)
    {
        long id = AddClip("a").Clips[0].Id;
        AddClip("b");

        Should.Throw<OuttaException>(() => _episodes.Move(id, position)).Message.ShouldContain("outside 1-2");
    }

    [Fact]
    public void Remove_Clip_KeepsTheClipButTakesItOutOfTheEpisode()
    {
        AddClip("a");
        long id = AddClip("b").Clips[^1].Id;
        AddClip("c");

        (Clip removed, EpisodeStatus status) = _episodes.Remove(id);

        removed.Title.ShouldBe("b");
        status.Clips.Select(c => (c.Title, c.Position)).ShouldBe([("a", (int?)1), ("c", 2)]);
        _repository.GetClip(id).EpisodeId.ShouldBeNull();
        Should.Throw<NotFoundException>(() => _episodes.Remove(id));
    }

    [Fact]
    public void StartNew_Name_BecomesCurrentAndDuplicatesAreRefused()
    {
        _episodes.Current();

        Episode special = _episodes.StartNew("  All Out   special ");

        special.Name.ShouldBe("All Out special");
        _episodes.Current().Id.ShouldBe(special.Id);
        Should.Throw<AlreadyExistsException>(() => _episodes.StartNew("all out special"));
    }

    [Fact]
    public void Finish_CurrentEpisode_MarksBuiltAndOpensTheNext()
    {
        Episode first = _episodes.Current();

        (Episode built, Episode? opened) = _episodes.Finish(first, "/out/Episode 1.mp4");

        built.IsBuilt.ShouldBeTrue();
        built.OutputPath.ShouldBe("/out/Episode 1.mp4");
        opened.ShouldNotBeNull();
        opened.Name.ShouldBe("Episode 2");
        _episodes.Current().Id.ShouldBe(opened.Id);
    }

    [Fact]
    public void AddClip_BuiltEpisode_IsRefused()
    {
        _episodes.Finish(_episodes.Current(), "/x.mp4");
        Clip clip = _repository.AddClip(new NewClip("late", "youtube", "u", 0, 5, 5, "/c.mp4", null));

        Should.Throw<EpisodeBuiltException>(() => _episodes.AddClip(clip, "Episode 1"));
    }

    private EpisodeStatus AddClip(string title, double duration = 60)
    {
        Clip clip = _repository.AddClip(new NewClip(title, "youtube", "u", 0, duration, duration, $"/c/{title}.mp4", null));
        return _episodes.AddClip(clip);
    }
}
