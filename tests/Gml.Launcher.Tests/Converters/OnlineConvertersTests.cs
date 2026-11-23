using System.Globalization;
using Gml.Dto.Servers;
using Gml.Launcher.Assets.Resources;
using Gml.Launcher.Core.Converters;

namespace Gml.Launcher.Tests.Converters;

/// <summary>
///     A profile lists one server per route (direct domain, bridges) and they all report the same players, so
///     summing them multiplied the online count.
/// </summary>
public class OnlineConvertersTests
{
    private static ServerReadDto Server(bool isOnline, int? online) => new() { IsOnline = isOnline, Online = online };

    private static object Online(List<ServerReadDto> servers) =>
        new SumServersOnlineConverter().Convert(servers, typeof(string), null, CultureInfo.InvariantCulture);

    [Test]
    public void SeveralRoutesToOneServer_CountThePlayersOnce()
    {
        Assert.That(Online([Server(true, 5), Server(true, 5), Server(true, 4)]), Is.EqualTo("5"));
    }

    [Test]
    public void OfflineServers_AreIgnored()
    {
        Assert.That(Online([Server(false, 9), Server(true, 3)]), Is.EqualTo("3"));
    }

    [Test]
    public void NothingOnline_IsZero()
    {
        Assert.That(Online([Server(false, 9)]), Is.EqualTo("0"));
        Assert.That(Online([]), Is.EqualTo("0"));
    }

    [Test]
    public void Label_IsSingular_EvenWithSeveralServers()
    {
        var label = new ServersLabelConverter().Convert(new List<ServerReadDto> { Server(true, 1), Server(true, 1) },
            typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(label, Is.EqualTo(Resources.OnServer));
    }
}
