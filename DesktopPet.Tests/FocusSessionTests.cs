using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class FocusSessionTests
{
    [TestMethod]
    public void DefaultsAndRepeatedRoundsWaitForManualCommands()
    {
        var session = new FocusSession();
        var restores = 0;
        var expectedRestores = 0;
        session.RestoreRequested += () => restores++;
        Assert.AreEqual(25, session.FocusMinutes);
        Assert.AreEqual(5, session.BreakMinutes);
        Assert.AreEqual(FocusPhase.Idle, session.Phase);
        session.Advance(TimeSpan.MaxValue);
        Assert.AreEqual(FocusPhase.Idle, session.Phase);
        session.Configure(1, 2);
        for (var round = 0; round < 3; round++)
        {
            session.StartFocus();
            Assert.AreEqual(TimeSpan.FromMinutes(1), session.Remaining);
            Assert.IsNull(session.PetActionOverride);
            Assert.IsFalse(session.HandlePetClick());
            session.Advance(TimeSpan.FromSeconds(59.5));
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), session.Remaining);
            session.Advance(TimeSpan.FromMinutes(10));
            Assert.AreEqual(FocusPhase.FocusFinished, session.Phase);
            Assert.AreEqual(TimeSpan.Zero, session.Remaining);
            Assert.AreEqual(PetAction.Wave, session.PetActionOverride);
            Assert.AreEqual(expectedRestores, restores);
            session.Advance(TimeSpan.MaxValue);
            Assert.AreEqual(PetAction.Wave, session.PetActionOverride);
            if (round != 1)
            {
                Assert.IsTrue(session.HandlePetClick());
                Assert.IsNull(session.PetActionOverride);
                Assert.IsFalse(session.HandlePetClick());
                Assert.AreEqual(FocusPhase.FocusFinished, session.Phase);
                Assert.AreEqual(++expectedRestores, restores);
            }
            session.TakeBreak();
            Assert.AreEqual(TimeSpan.FromMinutes(2), session.Remaining);
            Assert.IsFalse(session.WavePending);
            Assert.AreEqual(PetAction.Sleep, session.PetActionOverride);
            session.Advance(TimeSpan.FromMinutes(2));
            Assert.AreEqual(FocusPhase.BreakFinished, session.Phase);
            Assert.AreEqual(++expectedRestores, restores);
            session.Advance(TimeSpan.MaxValue);
            Assert.AreEqual(FocusPhase.BreakFinished, session.Phase);
            Assert.AreEqual(PetAction.Sleep, session.PetActionOverride);
        }
    }

    [TestMethod]
    public void PauseResumeAndConfigurationPreserveCapturedTimeForBothPhases()
    {
        var session = new FocusSession();
        session.Configure(1, 2);
        session.StartFocus();
        foreach (var phase in new[] { FocusPhase.Focus, FocusPhase.Break })
        {
            var full = session.Remaining;
            session.Advance(TimeSpan.FromMilliseconds(1234));
            session.TogglePause();
            session.Configure(3, 4);
            session.Advance(TimeSpan.MaxValue);
            Assert.AreEqual(full - TimeSpan.FromMilliseconds(1234), session.Remaining);
            Assert.AreEqual(phase, session.Phase);
            Assert.IsTrue(session.IsPaused);
            if (phase == FocusPhase.Break) Assert.AreEqual(PetAction.Sleep, session.PetActionOverride);
            session.TogglePause();
            session.Advance(session.Remaining);
            session.TakeBreak();
        }
        session.StartFocus();
        Assert.AreEqual(TimeSpan.FromMinutes(3), session.Remaining);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SkipBreakRetainsSleepAndWaitsForStart(bool paused)
    {
        var session = new FocusSession();
        session.StartFocus();
        session.Advance(session.Remaining);
        session.TakeBreak();
        if (paused) session.TogglePause();
        session.SkipBreak();
        Assert.AreEqual(FocusPhase.BreakFinished, session.Phase);
        Assert.IsFalse(session.IsPaused);
        Assert.AreEqual(TimeSpan.Zero, session.Remaining);
        Assert.AreEqual(PetAction.Sleep, session.PetActionOverride);
        Assert.IsTrue(session.HandlePetClick());
        session.Advance(TimeSpan.MaxValue);
        Assert.AreEqual(FocusPhase.BreakFinished, session.Phase);
        session.StartFocus();
        Assert.AreEqual(FocusPhase.Focus, session.Phase);
        Assert.IsNull(session.PetActionOverride);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)]
    public void StopClearsEveryRunningPausedAndWaitingStateButKeepsPreferences(int state)
    {
        var session = new FocusSession();
        session.Configure(2, 3);
        session.StartFocus();
        if (state == 1) session.TogglePause();
        if (state >= 2) session.Advance(session.Remaining);
        if (state >= 3) session.TakeBreak();
        if (state == 4) session.TogglePause();
        if (state == 5) session.Advance(session.Remaining);
        if (state == 6) session.SkipBreak();
        session.Stop();
        session.Stop();
        Assert.AreEqual(FocusPhase.Idle, session.Phase);
        Assert.AreEqual(TimeSpan.Zero, session.Remaining);
        Assert.IsFalse(session.WavePending || session.IsPaused);
        Assert.IsNull(session.PetActionOverride);
        Assert.AreEqual(2, session.FocusMinutes);
        Assert.AreEqual(3, session.BreakMinutes);
    }

    [TestMethod]
    [DataRow(0, 5)] [DataRow(-1, 5)] [DataRow(25, 0)] [DataRow(1441, 5)] [DataRow(25, int.MaxValue)]
    public void InvalidDurationsAreRejectedAtomically(int focus, int rest)
    {
        var session = new FocusSession();
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Configure(focus, rest));
        Assert.AreEqual(25, session.FocusMinutes);
        Assert.AreEqual(5, session.BreakMinutes);
    }

    [TestMethod]
    public void DuplicateAndUnrelatedCommandsDoNotRestartOrAdvancePhases()
    {
        var session = new FocusSession();
        session.TakeBreak(); session.TogglePause(); session.SkipBreak();
        Assert.AreEqual(FocusPhase.Idle, session.Phase);
        session.StartFocus();
        session.Advance(TimeSpan.FromSeconds(1));
        var remaining = session.Remaining;
        session.StartFocus(); session.TakeBreak(); session.SkipBreak();
        Assert.AreEqual(remaining, session.Remaining);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(TimeSpan.FromTicks(-1)));
        session.Advance(remaining);
        session.StartFocus(); session.TogglePause(); session.SkipBreak();
        Assert.AreEqual(FocusPhase.FocusFinished, session.Phase);
        session.TakeBreak();
        session.Advance(TimeSpan.FromSeconds(1));
        remaining = session.Remaining;
        session.TakeBreak(); session.StartFocus();
        Assert.AreEqual(remaining, session.Remaining);
    }

    [TestMethod]
    public void DifferentElapsedSlicesReachTheSameManualBoundary()
    {
        var whole = new FocusSession();
        var sliced = new FocusSession();
        whole.StartFocus(); sliced.StartFocus();
        whole.Advance(TimeSpan.FromMinutes(30));
        for (var i = 0; i < 1800; i++) sliced.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(whole.Phase, sliced.Phase);
        Assert.AreEqual(whole.Remaining, sliced.Remaining);
        Assert.AreEqual(whole.PetActionOverride, sliced.PetActionOverride);
    }
}
