using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Gml.Launcher.Core.Stats;

/// <summary>
///     Turns a profile's <c>logs</c> folder into game runs: every <c>yyyy-MM-dd-N.log.gz</c> archive plus
///     <c>latest.log</c> is one start of the client. Archives never change, so their spans are cached.
/// </summary>
public sealed class ClientLogScanner(ILogSpanCache cache)
{
    private const string LatestLog = "latest.log";

    public IReadOnlyList<GameRun> Scan(string logsDirectory)
    {
        var runs = new List<GameRun>();
        if (!Directory.Exists(logsDirectory))
            return runs;

        foreach (var file in new DirectoryInfo(logsDirectory).EnumerateFiles())
        {
            if (ClientLogParser.TryParseArchiveName(file.Name, out var date))
            {
                if (ReadArchive(file) is { } span)
                    runs.Add(span.ToArchivedRun(date));
            }
            else if (file.Name.Equals(LatestLog, StringComparison.OrdinalIgnoreCase))
            {
                if (TryRead(file, gzip: false) is { } span)
                    runs.Add(span.ToRunEndingAt(file.LastWriteTime));
            }
        }

        return runs;
    }

    /// <summary>How long the client of this profile was running in total, menus and loading included.</summary>
    public TimeSpan TotalTime(string logsDirectory)
    {
        var total = TimeSpan.Zero;
        foreach (var run in Scan(logsDirectory))
            total += run.Duration;
        return total;
    }

    private LogSpan? ReadArchive(FileInfo file)
    {
        var key = $"{file.Name}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        if (cache.TryGet(key, out var cached))
            return cached;

        var span = TryRead(file, gzip: true);
        cache.Set(key, span);
        return span;
    }

    private static LogSpan? TryRead(FileInfo file, bool gzip)
    {
        try
        {
            // The game may still be writing latest.log.
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using Stream content = gzip ? new GZipStream(stream, CompressionMode.Decompress) : stream;
            using var reader = new StreamReader(content, Encoding.UTF8);

            return ClientLogParser.ReadSpan(ReadLines(reader));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                              or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}
