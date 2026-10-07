using System.Diagnostics;
using System.IO;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class BuildScriptTests
{
    [TestMethod]
    public async Task ReleaseOnlyRestoresAndPublishesAppThenPackages()
    {
        using var fixture = new ScriptFixture();
        var result = await fixture.RunAsync("Build-AndPublish.ps1");
        Assert.AreEqual(0, result.ExitCode, result.Error);
        CollectionAssert.AreEqual(new[]
        {
            "--version",
            @"restore .\DesktopPet.Windows\DesktopPet.Windows.csproj --locked-mode",
            @"publish .\DesktopPet.Windows\DesktopPet.Windows.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output "
                + Path.Combine(fixture.Root, "candidate", "app"),
            "Complete-ReleaseCandidate " + Path.Combine(fixture.Root, "candidate")
        }, fixture.Commands);
        StringAssert.Contains(result.Output, Path.Combine(fixture.Root, "candidate", "app", "DesktopPet.Windows.exe"));
    }

    [TestMethod]
    public async Task TestRunsAllSpriteChecksAndReleaseTestsWithoutPublishing()
    {
        using var fixture = new ScriptFixture();
        var result = await fixture.RunAsync("Test.ps1");
        Assert.AreEqual(0, result.ExitCode, result.Error);
        CollectionAssert.AreEqual(new[]
        {
            "--version", @"restore .\DesktopPet.sln --locked-mode",
            "Test-HoodSpriteTransparency", "Test-HoodRunSprites", "Test-AlternatePetSprites",
            @"test .\DesktopPet.sln --configuration Release --no-restore --logger trx --results-directory .\DesktopPet.Tests\TestResults"
        }, fixture.Commands);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, "candidate")));
    }

    [TestMethod]
    [DataRow("Build-AndPublish.ps1", "restore", 2)]
    [DataRow("Build-AndPublish.ps1", "publish", 3)]
    [DataRow("Build-AndPublish.ps1", "Complete-ReleaseCandidate", 4)]
    [DataRow("Test.ps1", "restore", 2)]
    [DataRow("Test.ps1", "Test-HoodSpriteTransparency", 3)]
    [DataRow("Test.ps1", "Test-HoodRunSprites", 4)]
    [DataRow("Test.ps1", "Test-AlternatePetSprites", 5)]
    [DataRow("Test.ps1", "test", 6)]
    public async Task FailureStopsBeforeNextStep(string script, string failingStep, int commandCount)
    {
        using var fixture = new ScriptFixture();
        var result = await fixture.RunAsync(script, failingStep);
        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Error, failingStep);
        Assert.AreEqual(commandCount, fixture.Commands.Length);
    }

    [TestMethod]
    public async Task ExistingCandidateIsRejectedBeforeInvokingSdk()
    {
        using var fixture = new ScriptFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "candidate"));
        var result = await fixture.RunAsync("Build-AndPublish.ps1");
        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Error, "Output already exists");
        Assert.AreEqual(0, fixture.Commands.Length);
    }

    private sealed class ScriptFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Hood script tests " + Guid.NewGuid());
        public string[] Commands => File.Exists(Path.Combine(Root, "commands.txt"))
            ? File.ReadAllLines(Path.Combine(Root, "commands.txt")) : [];

        public ScriptFixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "tools"));
            Directory.CreateDirectory(Path.Combine(Root, "caller"));
            foreach (var script in new[] { "Build-AndPublish.ps1", "Test.ps1" })
                File.Copy(Path.Combine(AppContext.BaseDirectory, "tools", script), Path.Combine(Root, "tools", script));
            File.WriteAllText(Path.Combine(Root, "dotnet.ps1"), """
                if ($PWD.Path -ne $env:SCRIPT_TEST_ROOT) { throw 'Wrong working directory.' }
                Add-Content -LiteralPath (Join-Path $env:SCRIPT_TEST_ROOT 'commands.txt') -Value ($args -join ' ')
                $global:LASTEXITCODE = if ($args[0] -eq $env:SCRIPT_TEST_FAIL) { 7 } else { 0 }
                """);
            foreach (var helper in new[] { "Test-HoodSpriteTransparency", "Test-HoodRunSprites", "Test-AlternatePetSprites", "Complete-ReleaseCandidate" })
                File.WriteAllText(Path.Combine(Root, "tools", helper + ".ps1"), """
                    param([string]$CandidateDirectory)
                    $step = [IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
                    $command = if ($CandidateDirectory) { "$step $CandidateDirectory" } else { $step }
                    Add-Content -LiteralPath (Join-Path $env:SCRIPT_TEST_ROOT 'commands.txt') -Value $command
                    if ($step -eq $env:SCRIPT_TEST_FAIL) { throw "$step failed." }
                    """);
        }

        public async Task<(int ExitCode, string Output, string Error)> RunAsync(string script, string failingStep = "")
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.Combine(Root, "caller")
            };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                Path.Combine(Root, "tools", script), "-DotnetExe", Path.Combine(Root, "dotnet.ps1") })
                start.ArgumentList.Add(argument);
            if (script == "Build-AndPublish.ps1")
            {
                start.ArgumentList.Add("-CandidateDirectory");
                start.ArgumentList.Add("candidate");
            }
            start.Environment["SCRIPT_TEST_ROOT"] = Root;
            start.Environment["SCRIPT_TEST_FAIL"] = failingStep;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Script test timed out.");
            }
            return (process.ExitCode, await output, await error);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
