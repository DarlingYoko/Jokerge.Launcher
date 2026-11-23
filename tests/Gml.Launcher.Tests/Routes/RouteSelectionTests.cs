using Gml.Client.Interfaces;
using Gml.Launcher.Core.Routes;

namespace Gml.Launcher.Tests.Routes;

public class RouteSelectionTests
{
    private static readonly ServerRouteDto Direct = Route("Напрямую", "waffle.jokerge.com");
    private static readonly ServerRouteDto Moscow = Route("Москва", "msk.jokerge.com");
    private static readonly ServerRouteDto Frankfurt = Route("Франкфурт", "fra.jokerge.com");
    private static readonly IReadOnlyList<ServerRouteDto> Routes = [Direct, Moscow, Frankfurt];

    private static ServerRouteDto Route(string name, string address) =>
        new() { Name = name, Address = address, Port = 25565 };

    private static Func<ServerRouteDto, ServerPingResult?> Pings(
        params (ServerRouteDto Route, ServerPingResult Result)[] pings) =>
        route => pings.Where(p => ReferenceEquals(p.Route, route)).Select(p => p.Result).FirstOrDefault();

    [Test]
    public void Auto_PicksTheOnlineRouteWithTheLowestPing()
    {
        var target = RouteSelection.PickAuto(Routes, Pings(
            (Direct, ServerPingResult.Online(120)),
            (Moscow, ServerPingResult.Online(35)),
            (Frankfurt, ServerPingResult.Online(54))));

        Assert.That(target, Is.SameAs(Moscow));
    }

    [Test]
    public void Auto_SkipsOfflineRoutes()
    {
        var target = RouteSelection.PickAuto(Routes, Pings(
            (Direct, ServerPingResult.Online(120)),
            (Moscow, ServerPingResult.Offline),
            (Frankfurt, ServerPingResult.Online(54))));

        Assert.That(target, Is.SameAs(Frankfurt));
    }

    [Test]
    public void Auto_SamePing_KeepsTheEarlierRoute()
    {
        var target = RouteSelection.PickAuto(Routes, Pings(
            (Direct, ServerPingResult.Offline),
            (Moscow, ServerPingResult.Online(40)),
            (Frankfurt, ServerPingResult.Online(40))));

        Assert.That(target, Is.SameAs(Moscow));
    }

    [Test]
    public void Auto_NothingOnline_FallsBackToTheMainRoute()
    {
        var target = RouteSelection.PickAuto(Routes, Pings(
            (Direct, ServerPingResult.Offline),
            (Moscow, ServerPingResult.Offline)));

        Assert.That(target, Is.SameAs(Direct));
    }

    [Test]
    public void Auto_NotCheckedYet_IsTheMainRoute()
    {
        Assert.That(RouteSelection.PickAuto(Routes, _ => null), Is.SameAs(Direct));
    }

    [Test]
    public void Auto_NoRoutes_IsNothing()
    {
        Assert.That(RouteSelection.PickAuto([], _ => null), Is.Null);
    }

    [Test]
    public void SavedRoute_IsFoundByName()
    {
        Assert.That(RouteSelection.FindSaved(Routes, "Франкфурт"), Is.SameAs(Frankfurt));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Амстердам")]
    public void SavedRoute_MissingOrAuto_IsNothing(string? saved)
    {
        Assert.That(RouteSelection.FindSaved(Routes, saved), Is.Null);
    }

    [Test]
    public void Resolve_PrefersTheSavedRoute_OverAFasterOne()
    {
        var target = RouteSelection.Resolve(Routes, "Напрямую", Pings(
            (Direct, ServerPingResult.Online(120)),
            (Moscow, ServerPingResult.Online(35))));

        Assert.That(target, Is.SameAs(Direct));
    }

    [Test]
    public void Resolve_SavedRouteGone_IsAuto()
    {
        var target = RouteSelection.Resolve(Routes, "Амстердам", Pings(
            (Direct, ServerPingResult.Online(120)),
            (Moscow, ServerPingResult.Online(35))));

        Assert.That(target, Is.SameAs(Moscow));
    }
}
