using System.Reactive.Subjects;
using System.Text.Json;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Routes;

namespace Gml.Launcher.Tests.Routes;

/// <summary>What the launcher tells the client mod right before the game starts.</summary>
public class RouteFilePreparerTests
{
    private FakeRoutesSource _source = null!;
    private FakePinger _pinger = null!;
    private InMemoryStorage _storage = null!;
    private string _installation = null!;

    private RouteFilePreparer Preparer() => new(_source, _pinger, new RouteChoiceStore(_storage));

    private string RouteFile => RouteFileWriter.PathFor(_installation, "Waffle");

    [SetUp]
    public void SetUp()
    {
        _source = new FakeRoutesSource();
        _pinger = new FakePinger();
        _storage = new InMemoryStorage();
        _installation = Path.Combine(Path.GetTempPath(), "route-preparer-tests-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_installation))
            Directory.Delete(_installation, true);
    }

    [Test]
    public async Task RoutesNotLoaded_KeepsTheFile()
    {
        var plan = await Preparer().PlanAsync("Waffle");

        Assert.That(plan, Is.SameAs(RouteFilePlan.Keep));
        Assert.That(_pinger.Pinged, Is.Empty);
    }

    [Test]
    public async Task NoRoutes_RemovesTheFile()
    {
        _source.Routes["Waffle"] = [];

        var plan = await Preparer().PlanAsync("Waffle");

        Assert.That(plan, Is.SameAs(RouteFilePlan.Remove));
    }

    [Test]
    public async Task Auto_PingsEveryRoute_AndTargetsTheFastest()
    {
        var direct = SampleRoutes.Direct();
        var frankfurt = SampleRoutes.Frankfurt();
        _source.Routes["Waffle"] = [direct, frankfurt];
        _pinger.Results["waffle.jokerge.com"] = ServerPingResult.Online(140);
        _pinger.Results["fra.jokerge.com"] = ServerPingResult.Online(54);

        var plan = await Preparer().PlanAsync("Waffle");

        Assert.That(plan.Kind, Is.EqualTo(RouteFilePlanKind.Write));
        Assert.That(plan.Target, Is.SameAs(frankfurt));
        Assert.That(plan.Routes, Is.EqualTo(new[] { direct, frankfurt }));
        Assert.That(_pinger.Pinged, Is.EquivalentTo(new[] { "waffle.jokerge.com", "fra.jokerge.com" }));
    }

    [Test]
    public async Task SavedRoute_IsTheTarget_WithoutPinging()
    {
        var frankfurt = SampleRoutes.Frankfurt();
        _source.Routes["Waffle"] = [SampleRoutes.Direct(), frankfurt];
        await new RouteChoiceStore(_storage).SetAsync("Waffle", "Франкфурт");

        var plan = await Preparer().PlanAsync("Waffle");

        Assert.That(plan.Target, Is.SameAs(frankfurt));
        Assert.That(_pinger.Pinged, Is.Empty);
    }

    [Test]
    public async Task SavedRouteGone_FallsBackToAuto()
    {
        var direct = SampleRoutes.Direct();
        _source.Routes["Waffle"] = [direct, SampleRoutes.Frankfurt()];
        _pinger.Results["waffle.jokerge.com"] = ServerPingResult.Online(30);
        _pinger.Results["fra.jokerge.com"] = ServerPingResult.Online(54);
        await new RouteChoiceStore(_storage).SetAsync("Waffle", "Амстердам");

        var plan = await Preparer().PlanAsync("Waffle");

        Assert.That(plan.Target, Is.SameAs(direct));
    }

    [Test]
    public async Task Prepare_WritesTheFile_AndPutsItBackAfterTheFileSync()
    {
        _source.Routes["Waffle"] = [SampleRoutes.Direct(), SampleRoutes.Frankfurt()];
        _pinger.Results["fra.jokerge.com"] = ServerPingResult.Online(54);
        var filesSynced = new Subject<FileSyncResult>();

        using (await Preparer().PrepareAsync(_installation, "Waffle", filesSynced))
        {
            Assert.That(TargetHost(), Is.EqualTo("fra.jokerge.com"));

            // The pre-launch sync deletes files the profile does not list, this one included.
            File.Delete(RouteFile);
            filesSynced.OnNext(new FileSyncResult([], []));

            Assert.That(TargetHost(), Is.EqualTo("fra.jokerge.com"));
        }

        File.Delete(RouteFile);
        filesSynced.OnNext(new FileSyncResult([], []));
        Assert.That(File.Exists(RouteFile), Is.False, "after the launch the sync is no longer watched");
    }

    [Test]
    public async Task Prepare_RoutesNotLoaded_LeavesTheOldFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RouteFile)!);
        await File.WriteAllTextAsync(RouteFile, "{\"old\":true}");

        using (await Preparer().PrepareAsync(_installation, "Waffle", new Subject<FileSyncResult>()))
        {
        }

        Assert.That(await File.ReadAllTextAsync(RouteFile), Is.EqualTo("{\"old\":true}"));
    }

    [Test]
    public async Task Prepare_NoRoutes_DeletesTheOldFile()
    {
        _source.Routes["Waffle"] = [];
        Directory.CreateDirectory(Path.GetDirectoryName(RouteFile)!);
        await File.WriteAllTextAsync(RouteFile, "{\"old\":true}");

        using (await Preparer().PrepareAsync(_installation, "Waffle", new Subject<FileSyncResult>()))
        {
        }

        Assert.That(File.Exists(RouteFile), Is.False);
    }

    private string? TargetHost() =>
        JsonDocument.Parse(File.ReadAllText(RouteFile)).RootElement.GetProperty("target").GetProperty("host")
            .GetString();
}
