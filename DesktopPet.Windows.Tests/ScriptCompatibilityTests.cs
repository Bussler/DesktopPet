using System.Diagnostics;
using System.IO;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class ScriptCompatibilityTests
{
    [TestMethod]
    public async Task AlternateSpriteCheckRunsInWindowsPowerShell()
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(AppContext.BaseDirectory, "tools", "Test-AlternatePetSprites.ps1"),
            "-AssetsRoot", Path.Combine(AppContext.BaseDirectory, "Assets") })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Windows PowerShell sprite check timed out.");
        }
        Assert.AreEqual(0, process.ExitCode, await error);
        var result = await output;
        StringAssert.Contains(result, "Pip: no side artifacts");
        StringAssert.Contains(result, "Crumb: no side artifacts");
    }
}
