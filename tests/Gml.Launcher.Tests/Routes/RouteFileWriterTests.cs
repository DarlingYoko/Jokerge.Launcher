using System.Text;
using System.Text.Json;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Routes;

namespace Gml.Launcher.Tests.Routes;

/// <summary>The client mod reads this file to redirect the connection; its format must not drift.</summary>
public class RouteFileWriterTests
{
    private static readonly ServerRouteDto Direct = new() { Name = "Напрямую", Address = "waffle.jokerge.com", Port = 25565 };
    private static readonly ServerRouteDto Frankfurt = new() { Name = "Франкфурт", Address = "fra.jokerge.com", Port = 25566 };

    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "route-file-tests-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "config", RouteFileWriter.FileName);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void PathFor_IsTheProfileConfigFolder()
    {
        Assert.That(RouteFileWriter.PathFor(Path.Combine("C:", "Jokerge"), "Waffle"),
            Is.EqualTo(Path.Combine("C:", "Jokerge", "game data", "Waffle", "config", "jokerge-route.json")));
    }

    [Test]
    public void Serialize_WritesMainRoutesAndTarget()
    {
        var json = RouteFileWriter.Serialize([Direct, Frankfurt], Frankfurt);

        var expected = JsonDocument.Parse("""
            {
              "main": { "host": "waffle.jokerge.com", "port": 25565 },
              "routes": [
                { "name": "Напрямую", "host": "waffle.jokerge.com", "port": 25565 },
                { "name": "Франкфурт", "host": "fra.jokerge.com", "port": 25566 }
              ],
              "target": { "name": "Франкфурт", "host": "fra.jokerge.com", "port": 25566 }
            }
            """);
        Assert.That(Normalize(JsonDocument.Parse(json)), Is.EqualTo(Normalize(expected)));
    }

    [Test]
    public void Serialize_KeepsCyrillicReadable()
    {
        var json = RouteFileWriter.Serialize([Direct], Direct);

        Assert.That(json, Does.Contain("\"Напрямую\""));
    }

    [Test]
    public void Write_CreatesTheFolder_AsUtf8WithoutBom()
    {
        new RouteFileWriter().Write(_path, [Direct, Frankfurt], Frankfurt);

        var bytes = File.ReadAllBytes(_path);
        Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(RouteFileWriter.Serialize([Direct, Frankfurt], Frankfurt)));
    }

    [Test]
    public void Write_ReplacesTheOldFile_AndLeavesNoTempFiles()
    {
        var writer = new RouteFileWriter();
        writer.Write(_path, [Direct, Frankfurt], Frankfurt);

        writer.Write(_path, [Direct, Frankfurt], Direct);

        var target = JsonDocument.Parse(File.ReadAllText(_path)).RootElement.GetProperty("target");
        Assert.That(target.GetProperty("name").GetString(), Is.EqualTo("Напрямую"));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path)!), Is.EqualTo(new[] { _path }));
    }

    [Test]
    public void Delete_RemovesTheFile()
    {
        var writer = new RouteFileWriter();
        writer.Write(_path, [Direct], Direct);

        writer.Delete(_path);

        Assert.That(File.Exists(_path), Is.False);
    }

    [Test]
    public void Delete_NoFile_IsFine()
    {
        Assert.DoesNotThrow(() => new RouteFileWriter().Delete(_path));
    }

    [Test]
    public void Apply_Keep_LeavesTheFileAlone()
    {
        var writer = new RouteFileWriter();
        writer.Write(_path, [Direct, Frankfurt], Frankfurt);
        var before = File.ReadAllText(_path);

        writer.Apply(_path, RouteFilePlan.Keep);

        Assert.That(File.ReadAllText(_path), Is.EqualTo(before));
    }

    [Test]
    public void Apply_Remove_DeletesTheFile()
    {
        var writer = new RouteFileWriter();
        writer.Write(_path, [Direct], Direct);

        writer.Apply(_path, RouteFilePlan.Remove);

        Assert.That(File.Exists(_path), Is.False);
    }

    [Test]
    public void Apply_Write_WritesTheTarget()
    {
        new RouteFileWriter().Apply(_path, RouteFilePlan.Write([Direct, Frankfurt], Frankfurt));

        var target = JsonDocument.Parse(File.ReadAllText(_path)).RootElement.GetProperty("target");
        Assert.That(target.GetProperty("host").GetString(), Is.EqualTo("fra.jokerge.com"));
    }

    private static string Normalize(JsonDocument document) =>
        JsonSerializer.Serialize(document.RootElement);
}
