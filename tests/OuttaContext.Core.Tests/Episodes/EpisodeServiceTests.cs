using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Settings;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Episodes;

public sealed class EpisodeServiceTests
{
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly EpisodeService _episodes;

    public EpisodeServiceTests()
    {
        _episodes = new EpisodeService(_repository, new OuttaSettings());
    }

    [Fact]
    public void Current_FirstUse_CreatesEpisode1()
    {
        Episode episode = _episodes.Current();

        episode.Name.Value.ShouldBe("Episode 1");
        episode.State.ShouldBe(EpisodeState.Open);
        _episodes.Current().Id.ShouldBe(episode.Id);
    }

    [Fact]
    public void AddClip_SeveralClips_KeepsOrderAndTotals()
    {
        AddClip("a", 100);
        AddClip("b", 200);

        EpisodeStatus status = AddClip("c", 150);

        status.Clips.Select(c => c.Title.Value).ShouldBe(["a", "b", "c"]);
        status.Clips.Select(c => c.Position.GetValueOrDefault()).ShouldBe([1, 2, 3]);
        (status.Total.Seconds, status.Remaining.Seconds, status.IsReady).ShouldBe((450, 150, false));
    }

    [Fact]
    public void AddClip_PastTheTarget_IsAddedAndReady()
    {
        AddClip("a", 500);

        EpisodeStatus status = AddClip("b", 150);

        status.Total.Seconds.ShouldBe(650);
        status.Remaining.Seconds.ShouldBe(0);
        status.IsReady.ShouldBeTrue();
        status.Describe().ShouldBe("\"Episode 1\": 10:50 of 10:00 (ready to build)");
    }

    [Fact]
    public void Move_ClipToFront_RenumbersTheRest()
    {
        long[] ids = [.. new[] { "a", "b", "c", "d" }.Select(t => AddClip(t).Clips[^1].Id)];

        EpisodeStatus status = _episodes.Move(ids[3], 1).ShouldSucceed();

        status.Clips.Select(c => c.Title.Value).ShouldBe(["d", "a", "b", "c"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Move_PositionOutsideTheEpisode_Fails(int position)
    {
        long id = AddClip("a").Clips[0].Id;
        AddClip("b");

        _episodes.Move(id, position).ShouldFailWith("episode.position").Message.ShouldContain("outside 1-2");
    }

    [Fact]
    public void Move_UnknownClip_Fails()
    {
        _episodes.Move(42, 1).ShouldFailWith("episode.clip_not_found");
    }

    [Fact]
    public void Remove_Clip_KeepsTheClipButTakesItOutOfTheEpisode()
    {
        AddClip("a");
        long id = AddClip("b").Clips[^1].Id;
        AddClip("c");

        RemovedClip removed = _episodes.Remove(id).ShouldSucceed();

        removed.Clip.Title.Value.ShouldBe("b");
        removed.Status.Clips.Select(c => (c.Title.Value, c.Position.GetValueOrDefault())).ShouldBe([("a", 1), ("c", 2)]);
        _repository.FindClip(id).ShouldHaveValue().EpisodeId.HasNoValue.ShouldBeTrue();
        _episodes.Remove(id).ShouldFailWith("episode.clip_not_in_episode");
    }

    [Fact]
    public void StartNew_Name_BecomesCurrent()
    {
        _episodes.Current();

        Episode special = _episodes.StartNew("  All Out   special ").ShouldSucceed();

        special.Name.Value.ShouldBe("All Out special");
        _episodes.Current().Id.ShouldBe(special.Id);
    }

    [Fact]
    public void StartNew_NameTakenIgnoringCase_Fails()
    {
        _episodes.StartNew("All Out special").ShouldSucceed();

        _episodes.StartNew("all out special").ShouldFailWith("episode.exists");
    }

    [Theory]
    [InlineData("   ", "episode.name_empty")]
    [InlineData("???", "episode.name_unusable")]
    public void StartNew_UnusableName_Fails(string name, string code)
    {
        _episodes.StartNew(name).ShouldFailWith(code);
    }

    [Fact]
    public void Finish_CurrentEpisode_MarksBuiltAndOpensTheNext()
    {
        Episode first = _episodes.Current();

        FinishedEpisode finished = _episodes.Finish(first, "/out/Episode 1.mp4");

        finished.Built.IsBuilt.ShouldBeTrue();
        finished.Built.OutputPath.ShouldHaveValue().ShouldBe("/out/Episode 1.mp4");
        Episode opened = finished.Opened.ShouldHaveValue();
        opened.Name.Value.ShouldBe("Episode 2");
        _episodes.Current().Id.ShouldBe(opened.Id);
    }

    [Fact]
    public void AddClip_BuiltEpisode_IsRefused()
    {
        _episodes.Finish(_episodes.Current(), "/x.mp4");
        Clip clip = _repository.AddClip(Values.NewClip("late", 5));

        _episodes.AddClip(clip, "Episode 1").ShouldFailWith("episode.built");
    }

    [Fact]
    public void Status_UnknownEpisode_Fails()
    {
        _episodes.Status("Episode 9").ShouldFailWith("episode.not_found").Hint.ShouldContain("episode list");
    }

    private EpisodeStatus AddClip(string title, double duration = 60) =>
        _episodes.AddClip(_repository.AddClip(Values.NewClip(title, duration))).ShouldSucceed();
}
