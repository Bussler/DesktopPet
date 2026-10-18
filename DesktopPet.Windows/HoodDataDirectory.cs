using System.IO;

namespace DesktopPet.Windows;

internal static class HoodDataDirectory
{
    internal static string Resolve() => Resolve(Environment.GetEnvironmentVariable("HOOD_DATA_DIRECTORY"));

    internal static string Resolve(string? storageOverride) => string.IsNullOrWhiteSpace(storageOverride)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoodDesktopPet")
        : Path.GetFullPath(storageOverride);
}
