using Gml.Launcher.Core;
using Gml.Launcher.Core.Routes;
using Gml.Launcher.ViewModels.Components;

namespace Gml.Launcher.Tests.Routes;

public class ServerRoutesViewModelTests
{
    private FakeRoutesSource _source = null!;
    private FakePinger _pinger = null!;
    private InMemoryStorage _storage = null!;

    private ServerRoutesViewModel ViewModel() =>
        new(_source, _pinger, new RouteChoiceStore(_storage), new KeysAsText());

    [SetUp]
    public void SetUp()
    {
        _source = new FakeRoutesSource();
        _pinger = new FakePinger();
        _storage = new InMemoryStorage();
        _source.Routes["Waffle"] = [SampleRoutes.Direct(), SampleRoutes.Moscow(), SampleRoutes.Frankfurt()];
        _pinger.Results["waffle.jokerge.com"] = ServerPingResult.Online(140);
        _pinger.Results["fra.jokerge.com"] = ServerPingResult.Online(54);
    }

    [Test]
    public async Task Items_AreAutoThenEveryRoute()
    {
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        Assert.That(vm.Items.Select(i => i.Name),
            Is.EqualTo(new[] { SystemConstants.ServerRouteAuto, "Напрямую", "Москва", "Франкфурт" }));
        Assert.That(vm.Items[0].IsAuto, Is.True);
        Assert.That(vm.IsVisible, Is.True);
    }

    [Test]
    public async Task Load_PingsOnce_AndShowsStatusAndPing()
    {
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        var frankfurt = vm.Items.Single(i => i.Name == "Франкфурт");
        var moscow = vm.Items.Single(i => i.Name == "Москва");
        Assert.Multiple(() =>
        {
            Assert.That(_pinger.Pinged, Has.Count.EqualTo(3));
            Assert.That(frankfurt.Status, Is.EqualTo(RouteStatus.Online));
            Assert.That(frankfurt.PingText, Is.EqualTo(SystemConstants.ServerRoutePing.Replace("{0}", "54")));
            Assert.That(moscow.Status, Is.EqualTo(RouteStatus.Offline));
            Assert.That(moscow.PingText, Is.Empty);
        });
    }

    [Test]
    public async Task AutoItem_ShowsTheRouteItWouldPick()
    {
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        var auto = vm.Items[0];
        Assert.That(auto.Detail, Is.EqualTo("Франкфурт"));
        Assert.That(auto.Status, Is.EqualTo(RouteStatus.Online));
        Assert.That(auto.PingText, Is.EqualTo(SystemConstants.ServerRoutePing.Replace("{0}", "54")));
    }

    [Test]
    public void BeforeAnyCheck_StatusIsUnknown()
    {
        var item = new RouteItemViewModel(SampleRoutes.Direct(), new KeysAsText());

        Assert.That(item.Status, Is.EqualTo(RouteStatus.Unknown));
        Assert.That(item.PingText, Is.Empty);
    }

    [Test]
    public async Task DefaultSelection_IsAuto()
    {
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        Assert.That(vm.SelectedItem, Is.SameAs(vm.Items[0]));
    }

    [Test]
    public async Task SavedSelection_IsRestored()
    {
        await new RouteChoiceStore(_storage).SetAsync("Waffle", "Москва");
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        Assert.That(vm.SelectedItem?.Name, Is.EqualTo("Москва"));
    }

    [Test]
    public async Task SavedRouteGone_SelectsAuto()
    {
        await new RouteChoiceStore(_storage).SetAsync("Waffle", "Амстердам");
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        Assert.That(vm.SelectedItem?.IsAuto, Is.True);
    }

    [Test]
    public async Task Selecting_IsSavedPerProfile()
    {
        var vm = ViewModel();
        await vm.LoadAsync("Waffle");

        vm.SelectedItem = vm.Items.Single(i => i.Name == "Франкфурт");
        Assert.That(await new RouteChoiceStore(_storage).GetAsync("Waffle"), Is.EqualTo("Франкфурт"));

        vm.SelectedItem = vm.Items[0];
        Assert.That(await new RouteChoiceStore(_storage).GetAsync("Waffle"), Is.Null);
    }

    [Test]
    public async Task Loading_DoesNotOverwriteTheSavedChoice()
    {
        await new RouteChoiceStore(_storage).SetAsync("Waffle", "Москва");
        _source.Routes["Other"] = [SampleRoutes.Direct(), SampleRoutes.Frankfurt()];
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");
        await vm.LoadAsync("Other");

        Assert.That(await new RouteChoiceStore(_storage).GetAsync("Waffle"), Is.EqualTo("Москва"));
        Assert.That(await new RouteChoiceStore(_storage).GetAsync("Other"), Is.Null);
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task OneRouteOrNone_HidesTheControl(int count)
    {
        _source.Routes["Waffle"] = new[] { SampleRoutes.Direct(), SampleRoutes.Frankfurt() }.Take(count).ToList();
        var vm = ViewModel();

        await vm.LoadAsync("Waffle");

        Assert.That(vm.IsVisible, Is.False);
        Assert.That(_pinger.Pinged, Is.Empty);
    }

    [Test]
    public async Task RoutesNotLoaded_HidesTheControl()
    {
        var vm = ViewModel();
        await vm.LoadAsync("Waffle");

        await vm.LoadAsync("Unknown");

        Assert.That(vm.IsVisible, Is.False);
        Assert.That(vm.Items, Is.Empty);
    }

    [Test]
    public async Task NoProfile_HidesTheControl()
    {
        var vm = ViewModel();

        await vm.LoadAsync(null);

        Assert.That(vm.IsVisible, Is.False);
        Assert.That(_source.Requests, Is.Empty);
    }

    [Test]
    public async Task Check_PingsAgain_AndUpdatesTheItems()
    {
        var vm = ViewModel();
        await vm.LoadAsync("Waffle");
        _pinger.Results["msk.jokerge.com"] = ServerPingResult.Online(20);

        await vm.CheckAsync();

        Assert.That(_pinger.Pinged, Has.Count.EqualTo(6));
        Assert.That(vm.Items.Single(i => i.Name == "Москва").Status, Is.EqualTo(RouteStatus.Online));
        Assert.That(vm.Items[0].Detail, Is.EqualTo("Москва"));
        Assert.That(vm.IsChecking, Is.False);
    }
}
