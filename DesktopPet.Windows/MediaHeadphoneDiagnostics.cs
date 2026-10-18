using System.Diagnostics;
using System.IO;

namespace DesktopPet.Windows;

internal static class MediaHeadphoneDiagnostics
{
    private static readonly object Gate = new();

    internal static void Report(string message, string? directory = null)
    {
        Trace.TraceWarning(message);
        try
        {
            directory ??= HoodDataDirectory.Resolve();
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "media-headphones.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 256 * 1024) File.WriteAllText(path, string.Empty);
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Trace.TraceWarning($"Media headphone diagnostic could not be saved: {exception.Message}");
        }
    }
}
