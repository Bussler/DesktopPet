using System.IO;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MediaHeadphoneDiagnosticsTests
{
    [TestMethod]
    public void DataDirectoryOverrideAndExplicitDiagnosticDirectoryRemainIndependent()
    {
        var root = Path.Combine(Path.GetTempPath(), "hood-diagnostic-paths-" + Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(root, "profile");
        var explicitDirectory = Path.Combine(root, "explicit");
        var previousOverride = Environment.GetEnvironmentVariable("HOOD_DATA_DIRECTORY");
        try
        {
            Environment.SetEnvironmentVariable("HOOD_DATA_DIRECTORY", profile);
            MediaHeadphoneDiagnostics.Report("Profile diagnostic.");
            MediaHeadphoneDiagnostics.Report("Explicit diagnostic.", explicitDirectory);
            Assert.AreEqual(profile, HoodDataDirectory.Resolve());
            Assert.AreEqual("Profile diagnostic.", File.ReadAllText(Path.Combine(profile, "media-headphones.log"))
                .Split(' ', 2)[1].Trim());
            StringAssert.Contains(File.ReadAllText(Path.Combine(explicitDirectory, "media-headphones.log")), "Explicit diagnostic.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOOD_DATA_DIRECTORY", previousOverride);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void OptionalFeatureFailuresCreateAnAccessibleBoundedLog()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hood-media-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            MediaHeadphoneDiagnostics.Report("Could not load headphone sprites; restore the app files.", directory);
            var path = Path.Combine(directory, "media-headphones.log");
            Assert.IsTrue(File.Exists(path), "A packaged user must be able to read a persistent diagnostic.");
            StringAssert.Contains(File.ReadAllText(path), "restore the app files");
            File.WriteAllText(path, new string('x', 256 * 1024));
            MediaHeadphoneDiagnostics.Report("Audio observation recovered.", directory);
            Assert.IsTrue(new FileInfo(path).Length < 1024, "Old diagnostics must not grow without limit.");
            StringAssert.Contains(File.ReadAllText(path), "Audio observation recovered.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void LogWriteFailureDoesNotBreakTheOptionalFeature()
    {
        var file = Path.GetTempFileName();
        try { MediaHeadphoneDiagnostics.Report("Audio observation unavailable.", file); }
        finally { File.Delete(file); }
    }
}
