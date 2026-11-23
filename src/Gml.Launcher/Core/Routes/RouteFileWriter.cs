using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Gml.Client.Interfaces;

namespace Gml.Launcher.Core.Routes;

/// <summary>
///     <c>&lt;install&gt;/game data/&lt;profile&gt;/config/jokerge-route.json</c>, read by the client mod: the server
///     list keeps the main address and the mod sends the connection to the target instead. The format is shared
///     with the mod, do not change it.
/// </summary>
public sealed class RouteFileWriter
{
    public const string FileName = "jokerge-route.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Route names are Russian; keep them readable instead of \u escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string PathFor(string installationDirectory, string profileName) =>
        Path.Combine(installationDirectory, "game data", profileName, "config", FileName);

    /// <param name="routes">All routes; the first is the main address.</param>
    public static string Serialize(IReadOnlyList<ServerRouteDto> routes, ServerRouteDto target)
    {
        var main = routes[0];
        var file = new RouteFileJson(
            new HostJson(main.Address, main.Port),
            routes.Select(route => new RouteJson(route.Name, route.Address, route.Port)).ToArray(),
            new RouteJson(target.Name, target.Address, target.Port));

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public void Apply(string path, RouteFilePlan plan)
    {
        switch (plan.Kind)
        {
            case RouteFilePlanKind.Write:
                Write(path, plan.Routes, plan.Target!);
                break;
            case RouteFilePlanKind.Remove:
                Delete(path);
                break;
            case RouteFilePlanKind.Keep:
                break;
        }
    }

    /// <summary>
    ///     Writes a temp file next to the target and moves it over, so the mod never reads a half-written file.
    /// </summary>
    public void Write(string path, IReadOnlyList<ServerRouteDto> routes, ServerRouteDto target)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temp = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, Serialize(routes, target), Utf8WithoutBom);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed record RouteFileJson(HostJson Main, RouteJson[] Routes, RouteJson Target);

    private sealed record HostJson(string Host, int Port);

    private sealed record RouteJson(string Name, string Host, int Port);
}
