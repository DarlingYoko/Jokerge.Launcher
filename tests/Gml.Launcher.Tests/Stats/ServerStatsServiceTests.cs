using System.Net;
using System.Text;
using Gml.Launcher.Core.Services;

namespace Gml.Launcher.Tests.Stats;

public class ServerStatsServiceTests
{
    private const string Template = "https://{profile}.stats.example.org/players.json";
    private const string Uuid = "d7cf8f82-6aec-4889-b692-ccbe6ff9063c";

    private const string Json = """
        { "version": 1, "advancementTotal": 10, "players": [
          { "uuid": "d7cf8f82-6aec-4889-b692-ccbe6ff9063c", "name": "microtatar", "playSeconds": 3600,
            "joins": 2, "lastSeen": "2026-11-17T19:01:56Z", "advancements": 4 } ] }
        """;

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Test]
    public async Task LoadsThePlayerFromTheProfileAddress()
    {
        var handler = new FakeHandler(_ => Ok(Json));
        var service = new ServerStatsService(new HttpClient(handler), Template);

        var stats = await service.GetAsync("Waffle", Uuid, "microtatar");

        Assert.That(stats?.Joins, Is.EqualTo(2));
        Assert.That(handler.Requests, Is.EqualTo(new[] { new Uri("https://waffle.stats.example.org/players.json") }));
    }

    [Test]
    public async Task KeepsTheFileForAWhile_ThenAsksAgain()
    {
        var handler = new FakeHandler(_ => Ok(Json));
        var clock = new ManualClock();
        var service = new ServerStatsService(new HttpClient(handler), Template, clock);

        await service.GetAsync("Waffle", Uuid, "microtatar");
        clock.Now += TimeSpan.FromMinutes(5);
        await service.GetAsync("Waffle", Uuid, "microtatar");
        Assert.That(handler.Requests, Has.Count.EqualTo(1));

        clock.Now += ServerStatsService.CacheFor;
        await service.GetAsync("Waffle", Uuid, "microtatar");
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ServerWithoutStatsSite_IsNullAndNotCached()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = new ServerStatsService(new HttpClient(handler), Template);

        Assert.That(await service.GetAsync("Other", Uuid, "microtatar"), Is.Null);
        Assert.That(await service.GetAsync("Other", Uuid, "microtatar"), Is.Null);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task NetworkErrorOrBrokenFile_IsNull()
    {
        var failing = new ServerStatsService(
            new HttpClient(new FakeHandler(_ => throw new HttpRequestException("no such host"))), Template);
        var broken = new ServerStatsService(new HttpClient(new FakeHandler(_ => Ok("<html>"))), Template);

        Assert.That(await failing.GetAsync("Waffle", Uuid, "microtatar"), Is.Null);
        Assert.That(await broken.GetAsync("Waffle", Uuid, "microtatar"), Is.Null);
    }

    [Test]
    public async Task NoAddress_NoRequest()
    {
        var handler = new FakeHandler(_ => Ok(Json));
        var service = new ServerStatsService(new HttpClient(handler), string.Empty);

        Assert.That(await service.GetAsync("Waffle", Uuid, "microtatar"), Is.Null);
        Assert.That(handler.Requests, Is.Empty);
    }
}
