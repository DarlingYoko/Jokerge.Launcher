using System.Text.Json;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class ServerStatsParserTests
{
    private const string Json = """
        {
         "version": 1,
         "server": "Waffle",
         "generatedAt": "2026-11-20T16:36:49.451Z",
         "advancementTotal": 533,
         "players": [
          { "uuid": "d7cf8f82-6aec-4889-b692-ccbe6ff9063c", "name": "microtatar", "playSeconds": 557862,
            "joins": 145, "lastSeen": "2026-11-17T19:01:56.000Z", "advancements": 197 },
          { "uuid": "2da6aadc-5f08-474b-b14c-2b10b225dcc0", "name": "Darling_Yoko", "playSeconds": 0,
            "joins": 0, "lastSeen": null, "advancements": 0 }
         ]
        }
        """;

    [Test]
    public void FindsPlayerByUuid()
    {
        var stats = ServerStatsParser.Find(Json, "d7cf8f82-6aec-4889-b692-ccbe6ff9063c", "someone-else");

        Assert.That(stats, Is.EqualTo(new ServerPlayerStats(
            TimeSpan.FromSeconds(557862), 145,
            new DateTimeOffset(2026, 11, 17, 19, 1, 56, TimeSpan.Zero), 197, 533)));
    }

    [Test]
    public void UuidMatchIgnoresDashesAndCase()
    {
        var stats = ServerStatsParser.Find(Json, "D7CF8F826AEC4889B692CCBE6FF9063C", "x");

        Assert.That(stats?.Joins, Is.EqualTo(145));
    }

    [Test]
    public void FallsBackToNameWhenUuidIsUnknown()
    {
        var stats = ServerStatsParser.Find(Json, "00000000-0000-0000-0000-000000000000", "darling_yoko");

        Assert.That(stats, Is.EqualTo(new ServerPlayerStats(TimeSpan.Zero, 0, null, 0, 533)));
    }

    [Test]
    public void UnknownPlayer_IsNull()
    {
        Assert.That(ServerStatsParser.Find(Json, "00000000-0000-0000-0000-000000000000", "nobody"), Is.Null);
    }

    [Test]
    public void UnsupportedVersion_IsNull()
    {
        var json = Json.Replace("\"version\": 1", "\"version\": 2");

        Assert.That(ServerStatsParser.Find(json, "d7cf8f82-6aec-4889-b692-ccbe6ff9063c", "microtatar"), Is.Null);
    }

    [Test]
    public void BrokenJson_Throws()
    {
        Assert.That(() => ServerStatsParser.Find("<html>", "d7cf8f82-6aec-4889-b692-ccbe6ff9063c", "microtatar"),
            Throws.InstanceOf<JsonException>());
    }
}
