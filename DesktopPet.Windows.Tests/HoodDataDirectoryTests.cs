using System.IO;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class HoodDataDirectoryTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void MissingOrBlankOverrideUsesLocalApplicationData(string? storageOverride)
    {
        Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet"), HoodDataDirectory.Resolve(storageOverride));
    }

    [TestMethod]
    public void OverridesResolveToTheRequestedAbsoluteDirectory()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "Hood alternate profile");
        Assert.AreEqual(absolute, HoodDataDirectory.Resolve(absolute));
        Assert.AreEqual(Path.GetFullPath("Hood alternate profile"), HoodDataDirectory.Resolve("Hood alternate profile"));
    }
}
