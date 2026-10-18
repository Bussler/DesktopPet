using System.Runtime.InteropServices;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[TestClass]
public sealed class FocusNotificationsTests
{
    [TestMethod]
    public void AcquiresOnceAndRestoresOnlyItsOwnChange()
    {
        var profile = FocusNotifications.Unrestricted;
        var writes = new List<string>();
        var notifications = new FocusNotifications(() => profile, value => { writes.Add(value); profile = value; });
        notifications.Update(true);
        notifications.Update(true);
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        Assert.AreEqual(1, writes.Count);
        notifications.Update(false);
        notifications.Update(false);
        CollectionAssert.AreEqual(new[] { FocusNotifications.PriorityOnly, FocusNotifications.Unrestricted }, writes);
        Assert.AreEqual("", notifications.Error);
    }

    [TestMethod]
    [DataRow(FocusNotifications.PriorityOnly)]
    [DataRow(FocusNotifications.AlarmsOnly)]
    public void ExistingDoNotDisturbIsNeverWeakenedOrDisabled(string profile)
    {
        var notifications = new FocusNotifications(() => profile, _ => Assert.Fail("Existing DND must remain unchanged."));
        notifications.Update(true);
        StringAssert.Contains(notifications.Status, "already on");
        notifications.Update(false);
        Assert.AreEqual("", notifications.Error);
    }

    [TestMethod]
    [DataRow(FocusNotifications.Unrestricted)]
    [DataRow(FocusNotifications.AlarmsOnly)]
    public void DifferentExternalSettingIsPreservedOnRelease(string external)
    {
        var profile = FocusNotifications.Unrestricted;
        var writes = 0;
        var notifications = new FocusNotifications(() => profile, value => { writes++; profile = value; });
        notifications.Update(true);
        profile = external;
        notifications.Update(false);
        Assert.AreEqual(external, profile);
        Assert.AreEqual(1, writes);
    }

    [TestMethod]
    public void AccessFailureIsReportedWithoutRepeatedRequestsOnEveryTick()
    {
        var reads = 0;
        var notifications = new FocusNotifications(() => { reads++; throw new COMException("Access denied"); });
        notifications.Update(true);
        notifications.Update(true);
        StringAssert.Contains(notifications.Error, "Could not enable");
        Assert.AreEqual(1, reads);
        notifications.Update(false);
        notifications.Update(true);
        Assert.AreEqual(2, reads, "Resuming is a new attempt.");
    }

    [TestMethod]
    public void FailedRestoreRetainsOwnershipForCleanupRetry()
    {
        var profile = FocusNotifications.Unrestricted;
        var failRestore = true;
        var notifications = new FocusNotifications(() => profile, value =>
        {
            if (value == FocusNotifications.Unrestricted && failRestore) throw new COMException("Busy");
            profile = value;
        });
        notifications.Update(true);
        notifications.Update(false);
        StringAssert.Contains(notifications.Error, "Could not restore");
        Assert.AreEqual(FocusNotifications.PriorityOnly, profile);
        failRestore = false;
        notifications.Update(false, force: true);
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
        Assert.AreEqual("", notifications.Error);
    }

    [TestMethod]
    public void PartiallyFailedEnableStillRestoresThePreviousSetting()
    {
        var profile = FocusNotifications.Unrestricted;
        var notifications = new FocusNotifications(() => profile, value =>
        {
            profile = value;
            if (value == FocusNotifications.PriorityOnly) throw new COMException("Write failed after changing state");
        });
        notifications.Update(true);
        StringAssert.Contains(notifications.Error, "Could not enable");
        notifications.Update(false);
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UnimplementedProfileWritesAreReportedAndCleanupCanRetry(bool failEnable)
    {
        var profile = FocusNotifications.Unrestricted;
        var fail = true;
        var notifications = new FocusNotifications(() => profile, value =>
        {
            if (fail && value == (failEnable ? FocusNotifications.PriorityOnly : FocusNotifications.Unrestricted))
                Marshal.ThrowExceptionForHR(unchecked((int)0x80004001)); // E_NOTIMPL
            profile = value;
        });
        notifications.Update(true);
        if (!failEnable) notifications.Update(false);
        StringAssert.Contains(notifications.Error, failEnable ? "Could not enable" : "Could not restore");
        fail = false;
        notifications.Update(false, force: true);
        Assert.AreEqual(FocusNotifications.Unrestricted, profile);
        Assert.AreEqual("", notifications.Error);
    }

    [TestMethod]
    public void IgnoredWritesAndUnknownProfilesCannotClaimProtection()
    {
        var notifications = new FocusNotifications(() => FocusNotifications.Unrestricted, _ => { });
        notifications.Update(true);
        StringAssert.Contains(notifications.Error, "did not enable");
        notifications = new FocusNotifications(() => "unknown", _ => Assert.Fail("Do not write an unknown profile."));
        notifications.Update(true);
        StringAssert.Contains(notifications.Error, "unsupported");
    }
}
