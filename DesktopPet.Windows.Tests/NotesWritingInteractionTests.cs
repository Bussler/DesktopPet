using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;
using DesktopPet.Windows;
using Microsoft.Win32;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NotesWritingInteractionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void RealNotesTypingReachesTheSelectedPetAndSavesWithoutMovingFocus(string pet)
    {
        using var fixture = new Fixture(pet);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Focus alone must not start writing.");
        Type(fixture.Editor, "a");
        Assert.AreEqual("a", fixture.Notes.GetNote(fixture.NoteId).Text);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreSame(fixture.Editor, Keyboard.FocusedElement);
        var presentation = fixture.Pet.CurrentPresentation;
        Assert.AreEqual(fixture.Assets.GetFrameRectangle(fixture.Assets.Definition.GetAnimation(PetAction.Write).Frames[presentation.FrameIndex]),
            ((CroppedBitmap)((Image)fixture.Pet.FindName("PetImage")).Source).SourceRect);
        Assert.AreEqual(fixture.NoteId, NotesApplication.Start(new JsonNotesStore(fixture.NotesPath)).Notes.Single().Id);
        Assert.AreEqual("a", NotesApplication.Start(new JsonNotesStore(fixture.NotesPath)).GetNote(fixture.NoteId).Text);
    }

    [TestMethod]
    public void RepeatedKeysRenewTheDeadlineWithoutRestartingTheAnimation()
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, "a");
        fixture.Advance(360);
        Assert.AreEqual(3, fixture.Pet.CurrentPresentation.FrameIndex);
        Type(fixture.Editor, "b");
        Assert.AreEqual(3, fixture.Pet.CurrentPresentation.FrameIndex);
        fixture.Advance(799);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Advance(1);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.IsFalse(fixture.Session.IsWriting);
        Assert.IsTrue(fixture.Session.Configuration.Behavior.Roaming);
        var position = fixture.Session.Position;
        fixture.Session.Advance(TimeSpan.FromSeconds(1));
        Assert.AreNotEqual(position, fixture.Session.Position, "Writing releases its temporary roaming pause.");
        Type(fixture.Editor, "c");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreEqual(0, fixture.Pet.CurrentPresentation.FrameIndex, "A new burst starts a fresh writing cycle.");
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("ä")]
    [DataRow("漢")]
    public void SpacesAndComposedCharactersAreRealWritingInput(string text)
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, text);
        Assert.AreEqual(text, fixture.Notes.GetNote(fixture.NoteId).Text);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    [DataRow(Key.Enter, ModifierKeys.None)]
    [DataRow(Key.Tab, ModifierKeys.None)]
    [DataRow(Key.Back, ModifierKeys.None)]
    [DataRow(Key.Delete, ModifierKeys.None)]
    [DataRow(Key.V, ModifierKeys.Control)]
    [DataRow(Key.Insert, ModifierKeys.Shift)]
    public void EditingKeysAndKeyboardPasteStartWriting(Key key, ModifierKeys modifiers)
    {
        using var fixture = new Fixture();
        PressKey(fixture.Editor, key, modifiers);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    [DataRow(Key.Left, ModifierKeys.None)]
    [DataRow(Key.End, ModifierKeys.None)]
    [DataRow(Key.PageDown, ModifierKeys.None)]
    [DataRow(Key.LeftShift, ModifierKeys.Shift)]
    [DataRow(Key.LeftCtrl, ModifierKeys.Control)]
    [DataRow(Key.LeftAlt, ModifierKeys.Alt)]
    [DataRow(Key.B, ModifierKeys.Control)]
    [DataRow(Key.Z, ModifierKeys.Control)]
    [DataRow(Key.V, ModifierKeys.Control | ModifierKeys.Shift)]
    public void NavigationModifiersAndOtherCommandsDoNotStartOrRenewWriting(Key key, ModifierKeys modifiers)
    {
        using var fixture = new Fixture();
        PressKey(fixture.Editor, key, modifiers);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "a");
        fixture.Advance(700);
        PressKey(fixture.Editor, key, modifiers);
        fixture.Advance(100);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void ProgrammaticChangesAndMousePasteDoNotCountAsTyping()
    {
        using var fixture = new Fixture();
        fixture.Editor.Selection.Text = "loaded";
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        NoteClipboardTestInput.PastePlainText(fixture.Editor, " pasted");
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "x");
        fixture.Advance(700);
        NoteClipboardTestInput.PastePlainText(fixture.Editor, " more");
        fixture.Advance(100);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void RoutedEditingKeyAndFocusDepartureUseTheRealWindowPath()
    {
        using var fixture = new Fixture();
        fixture.Editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(fixture.Note)!, 0, Key.Back) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.IsTrue(((TextBox)fixture.Note.FindName("NoteTitle")).Focus());
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.IsFalse(fixture.Session.IsWriting);
        Assert.IsTrue(fixture.Editor.Focus());
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Returning focus does not replay activity.");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void WritingAndIdleOptionsAreIndependent(bool idle, bool writing)
    {
        using var fixture = new Fixture(configuration: HoodConfiguration.Defaults with
        { Behavior = HoodConfiguration.Defaults.Behavior with { IdleAnimations = idle, WritingAnimations = writing } });
        Type(fixture.Editor, "a");
        Assert.AreEqual(writing, fixture.Pet.CurrentPresentation.Action == PetAction.Write);
        Assert.AreEqual(idle, fixture.Session.Configuration.Behavior.IdleAnimations);
    }

    [TestMethod]
    public void WritingPausesMovementAndRandomActionsWithoutSavingAWriteAction()
    {
        using var fixture = new Fixture(configuration: HoodConfiguration.Defaults);
        fixture.Session.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(fixture.Session.Presentation.Action is PetAction.RunLeft or PetAction.RunRight);
        var position = fixture.Session.Position;
        var interval = fixture.Session.RemainingRandomInterval;
        Type(fixture.Editor, "a");
        fixture.Advance(700);
        Assert.AreEqual(position, fixture.Session.Position);
        Assert.AreEqual(interval, fixture.Session.RemainingRandomInterval);
        fixture.Session.SaveCurrentState();
        Assert.AreNotEqual(PetAction.Write, fixture.SessionStore.Snapshot!.Action);
        Assert.AreEqual(2, fixture.SessionStore.Snapshot.SchemaVersion);
        Assert.IsTrue(fixture.Session.Needs.Hunger > 0, "Ordinary needs continue during writing.");
    }

    [TestMethod]
    [DataRow("feed")]
    [DataRow("sleep")]
    [DataRow("click")]
    [DataRow("drag")]
    public void SleepCareClickAndDraggingKeepPriority(string action)
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, "a");
        switch (action)
        {
            case "feed": fixture.Session.Feed(); break;
            case "sleep": fixture.Session.Sleep(); break;
            case "click": fixture.Session.React(); break;
            case "drag": fixture.Session.BeginDragging(); break;
        }
        fixture.Pet.RefreshWritingPresentation();
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "b");
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        if (action == "sleep") Assert.AreEqual(HoodSessionMode.Sleeping, fixture.Session.Mode);
    }

    [TestMethod]
    public void EndingFeedResumesOnlyStillCurrentTyping()
    {
        using var fixture = new Fixture();
        fixture.Session.Feed();
        Type(fixture.Editor, "a");
        fixture.Advance(700);
        Type(fixture.Editor, "b");
        fixture.Advance(201);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Session.Feed();
        fixture.Advance(901);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void WritingDoesNotDeferAnAutomaticNap()
    {
        using var fixture = new Fixture(configuration: HoodConfiguration.Defaults with
        { Sleep = HoodConfiguration.Defaults.Sleep with { AutomaticNapThreshold = 0.001 } });
        Type(fixture.Editor, "a");
        fixture.Advance(200);
        Assert.AreEqual(HoodSessionMode.Sleeping, fixture.Session.Mode);
        Assert.AreEqual(PetAction.Sleep, fixture.Pet.CurrentPresentation.Action);
        Assert.IsFalse(fixture.Session.IsWriting);
    }

    [TestMethod]
    public void ApplyingDisabledClearsActivityAndEnablingWaitsForFreshTyping()
    {
        using var fixture = new Fixture();
        var path = fixture.NotesPath + ".yaml";
        var controller = new HoodConfigurationController(fixture.Session, new YamlConfigurationStore(path));
        try
        {
            Type(fixture.Editor, "a");
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            controller.SaveAndApply("behavior.writing_animations", controller.Current with
            { Behavior = controller.Current.Behavior with { WritingAnimations = false } });
            fixture.Pet.RefreshBehaviorSettings();
            Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            Assert.IsFalse(fixture.Session.IsWriting);
            controller.SaveAndApply("behavior.writing_animations", controller.Current with
            { Behavior = controller.Current.Behavior with { WritingAnimations = true } });
            fixture.Pet.RefreshBehaviorSettings();
            Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            fixture.Note.Activate();
            fixture.Editor.Focus();
            Type(fixture.Editor, "b");
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void TitleTypingSavesAndEnterTransfersFocusWithoutReplayingWriting()
    {
        using var fixture = new Fixture();
        var title = (TextBox)fixture.Note.FindName("NoteTitle");
        title.Focus();
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        title.SelectAll();
        Type(title, "Plan");
        Assert.AreEqual("Plan", fixture.Notes.GetNote(fixture.NoteId).Name);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        title.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fixture.Note)!, 0, Key.Enter)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.AreSame(fixture.Editor, Keyboard.FocusedElement);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "body");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreEqual("body", fixture.Notes.GetNote(fixture.NoteId).Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FormulaSourceHasItsOwnTypingActivityAndLeavingItStopsImmediately(bool display)
    {
        var document = new NotesDocument
        {
            Paragraphs = [new()
            {
                Kind = display ? NoteBlockKind.DisplayMath : NoteBlockKind.Paragraph,
                Runs = [new("before "), new(display ? "$$x$$" : "$x$", Kind: display ? NoteInlineKind.Text : NoteInlineKind.InlineMath), new(" after")]
            }, new() { Runs = [new("body")] }]
        };
        if (display) document.Paragraphs[0].Runs = [new("$$x$$")];
        using var fixture = new Fixture(document: document);
        fixture.Editor.CaretPosition = ((Paragraph)fixture.Editor.Document.Blocks.LastBlock).ContentEnd;
        Type(fixture.Editor, "a");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        var button = GetInlines(fixture.Editor.Document).OfType<InlineUIContainer>()
            .Select(inline => inline.Child).OfType<NoteInlineMathButton>().Single();
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        fixture.Note.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Opening source is a field change, not typing.");
        var source = GetInlines(fixture.Editor.Document).OfType<NoteInlineMathSourceSpan>().Single();
        Type(fixture.Editor, "y");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        StringAssert.Contains(fixture.Notes.GetNote(fixture.NoteId).Text, "y");
        fixture.Editor.CaretPosition = ((Paragraph)fixture.Editor.Document.Blocks.LastBlock).ContentEnd;
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Leaving source clears activity before deferred rendering.");
        fixture.Note.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "z");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Pet.ReportNoteWriting(source, false);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "A stale source callback cannot cancel body typing.");
    }

    [TestMethod]
    public void SwitchingNotesAndOldWindowCleanupCannotCancelNewerTyping()
    {
        using var fixture = new Fixture();
        var other = fixture.AddNote();
        fixture.Focus(fixture.Note);
        Type(fixture.Editor, "first");
        fixture.Focus(other);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        var otherEditor = (RichTextBox)other.FindName("Editor");
        Type(otherEditor, "second");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Pet.ReportNoteWriting(fixture.Editor, false);
        fixture.Note.SetGroupVisibilityImmediately(false);
        fixture.Note.Close();
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreEqual("second", fixture.Notes.Notes.Single(note => note.Id != fixture.NoteId).Text);
    }

    [TestMethod]
    [DataRow("close")]
    [DataRow("hide")]
    [DataRow("native-hide")]
    [DataRow("delete")]
    public void ClosingHidingAndDeletingClearOnlyThatNotesActivity(string action)
    {
        using var fixture = new Fixture();
        fixture.AddNote();
        fixture.Focus(fixture.Note);
        Type(fixture.Editor, "a");
        switch (action)
        {
            case "close": fixture.Note.Close(); break;
            case "hide":
                fixture.Note.BeginGroupVisibilityTransition(false, new System.Windows.Point(100, 100), TimeSpan.FromSeconds(1), () => { });
                break;
            case "native-hide": fixture.Note.Hide(); break;
            case "delete": fixture.Trash(fixture.NoteId); break;
        }
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.IsFalse(fixture.Session.IsWriting);
        if (action == "hide")
        {
            Type(fixture.Editor, "hidden");
            Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            fixture.Note.SetGroupVisibilityImmediately(true);
            fixture.Focus(fixture.Note);
            Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            Type(fixture.Editor, "fresh");
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Paragraph)]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Code)]
    public void RichContentTypingKeepsItsStructureAndUndoHistory(NoteBlockKind kind)
    {
        using var fixture = new Fixture(document: new NotesDocument
        { Paragraphs = [new() { Kind = kind, Runs = [new("seed", NoteTextStyle.Bold)] }] });
        Type(fixture.Editor, "x");
        var accepted = fixture.Notes.GetNote(fixture.NoteId).Document;
        Assert.AreEqual(kind, accepted.Paragraphs.Single().Kind);
        StringAssert.Contains(accepted.GetPlainText(), "seed");
        StringAssert.Contains(accepted.GetPlainText(), "x");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Advance(700);
        typeof(NoteWindow).GetMethod("HandleEditorKey", Private)!.Invoke(fixture.Note, [Key.Z, true, false]);
        Assert.AreEqual("seed", fixture.Notes.GetNote(fixture.NoteId).Text);
        fixture.Advance(100);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Undo alone cannot renew typing activity.");
    }

    [TestMethod]
    public void TableCellTypingReachesThePetAndRetainsCellMembership()
    {
        var table = Guid.NewGuid();
        using var fixture = new Fixture(document: new NotesDocument
        {
            Paragraphs = Enumerable.Range(0, 4).Select(index => new NoteParagraph
            { Cell = new(table, index / 2, index % 2), Runs = [new("seed")] }).ToList()
        });
        var paragraph = (Paragraph)((Table)fixture.Editor.Document.Blocks.FirstBlock).RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock;
        fixture.Editor.CaretPosition = paragraph.ContentEnd;
        Type(fixture.Editor, "x");
        var accepted = fixture.Notes.GetNote(fixture.NoteId).Document;
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreEqual(4, accepted.Paragraphs.Count);
        Assert.IsTrue(accepted.Paragraphs.All(row => row.Cell!.TableId == table));
        StringAssert.Contains(accepted.Paragraphs[0].Runs.Single().Text, "x");
    }

    [TestMethod]
    public void SuspensionDiscardsTypingEvenWhenResumeOccursBeforeItsDeadline()
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, "a");
        fixture.Power(PowerModes.Suspend);
        Assert.IsFalse(fixture.Session.IsWriting);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "hidden");
        fixture.Clock.Advance(50);
        fixture.Power(PowerModes.Resume);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Type(fixture.Editor, "fresh");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void ClosingTheOverlayReleasesItsWritingSuppression()
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, "a");
        fixture.Pet.Close();
        Assert.IsFalse(fixture.Session.IsWriting);
        Type(fixture.Editor, "b");
        Assert.IsFalse(fixture.Session.IsWriting);
    }

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void FocusOverridesWinAndOnlyCurrentTypingResumesAfterward(string pet)
    {
        using var fixture = new Fixture(pet);
        var focus = new FocusSession();
        fixture.Pet.AttachFocus(focus);
        focus.StartFocus();
        Type(fixture.Editor, "a");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        focus.Advance(focus.Remaining);
        Assert.AreEqual(PetAction.Wave, fixture.Pet.CurrentPresentation.Action);
        fixture.Advance(500);
        Type(fixture.Editor, "b");
        Assert.AreEqual(PetAction.Wave, fixture.Pet.CurrentPresentation.Action);
        Assert.IsTrue(focus.HandlePetClick());
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        focus.TakeBreak();
        Type(fixture.Editor, "c");
        Assert.AreEqual(PetAction.Sleep, fixture.Pet.CurrentPresentation.Action);
        focus.TogglePause();
        Assert.AreEqual(PetAction.Sleep, fixture.Pet.CurrentPresentation.Action);
        fixture.Advance(800);
        focus.Stop();
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.IsFalse(fixture.Session.IsWriting);
        Assert.AreEqual(HoodSessionMode.Awake, fixture.Session.Mode, "Visual break sleep does not create a care nap.");
    }

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void ActualChecklistCompletionWinsThenReturnsOnlyToFreshTyping(string pet)
    {
        using var fixture = new Fixture(pet, document: new NotesDocument
        { Paragraphs = [new() { Kind = NoteBlockKind.Checklist, Runs = [new("task")] }] });
        Type(fixture.Editor, "a");
        var paragraph = (Paragraph)((System.Windows.Documents.List)fixture.Editor.Document.Blocks.FirstBlock)
            .ListItems.FirstListItem.Blocks.FirstBlock;
        var checkbox = paragraph.Inlines.OfType<InlineUIContainer>().Select(inline => inline.Child).OfType<CheckBox>().Single();
        ClickCheckBox(checkbox);
        Assert.AreEqual(PetAction.Pet, fixture.Pet.CurrentPresentation.Action);
        Assert.IsTrue(fixture.Notes.GetNote(fixture.NoteId).Document.Paragraphs.Single().IsChecked);
        fixture.Advance(1700);
        Type(fixture.Editor, "b");
        Assert.AreEqual(PetAction.Pet, fixture.Pet.CurrentPresentation.Action);
        fixture.Advance(220);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        ClickCheckBox(checkbox);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action, "Unchecking must not create another celebration.");
        fixture.Advance(580);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void ActualSpriteToggleKeepsNotesVisibleAndNeverQueuesHiddenTyping()
    {
        using var fixture = new Fixture();
        Type(fixture.Editor, "a");
        fixture.ToggleSprites();
        Assert.IsFalse(fixture.Session.Configuration.Behavior.ShowSprites);
        Assert.IsFalse(fixture.Session.IsWriting);
        Assert.IsTrue(fixture.Note.IsVisible);
        Assert.IsFalse(fixture.Timer.IsEnabled);
        Assert.IsNull(fixture.Pet.CurrentAssets);
        Type(fixture.Editor, "hidden");
        Assert.IsNull(fixture.Pet.CurrentAssets, "Hidden input must not load artwork.");
        fixture.ToggleSprites();
        fixture.Timer.Stop();
        Assert.IsTrue(fixture.Session.Configuration.Behavior.ShowSprites);
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        fixture.Focus(fixture.Note);
        Type(fixture.Editor, "fresh");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void QuitAndActualSessionReloadNeverRestoreWritingActivity()
    {
        using var fixture = new Fixture();
        var path = fixture.NotesPath + ".state.json";
        try
        {
            Type(fixture.Editor, "a");
            fixture.Session.Quit();
            Assert.IsFalse(fixture.Session.IsWriting);
            var store = new JsonSessionStore(path);
            store.Save(fixture.SessionStore.Snapshot!);
            var restored = HoodSession.Start(fixture.Assets.Definition, [new("Primary", new(0, 0, 1920, 1080), true)],
                new ZeroRandom(), store, fixture.Session.Configuration, store.Load());
            Assert.IsFalse(restored.IsWriting);
            Assert.AreNotEqual(PetAction.Write, restored.Presentation.Action);
            var json = File.ReadAllText(path);
            Assert.IsFalse(json.Contains("writing", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains("SourceIdentity", StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LegacyWritingFallbackRespectsIdleAndSwitchesBackToCustomWriting(bool idle)
    {
        using var fixture = new Fixture(configuration: HoodConfiguration.Defaults with
        { Behavior = HoodConfiguration.Defaults.Behavior with { IdleAnimations = idle, MediaHeadphones = false } });
        var directory = Path.Combine(Path.GetTempPath(), $"HoodWritingLegacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "hood.animations.json");
        var sheetPath = Path.Combine(directory, "hood_spritesheet.png");
        try
        {
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Assets.Source.Directory, "hood.animations.json")))!;
            var animations = manifest["animations"]!.AsArray();
            animations.Remove(animations.Single(animation => animation!["action"]!.GetValue<string>() == "write"));
            var decoder = BitmapDecoder.Create(new Uri(Path.Combine(fixture.Assets.Source.Directory, "hood_spritesheet.png")),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var legacySheet = new CroppedBitmap(decoder.Frames[0], new Int32Rect(0, 0, 6144, 4576));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(legacySheet));
            using (var stream = File.Create(sheetPath)) encoder.Save(stream);
            manifest["sheetHeight"] = legacySheet.PixelHeight;
            File.WriteAllText(manifestPath, manifest.ToJsonString());
            Type(fixture.Editor, "a");
            var source = HoodAssets.ReadSourceForPet(directory, fixture.Assets.Definition);
            fixture.Pet.ApplyPetAssets(HoodAssets.LoadForDisplay(source, 1, 1, 1));
            fixture.Advance(240);
            Assert.AreEqual(PetAction.Idle, fixture.Pet.CurrentPresentation.Action);
            Assert.AreEqual(idle ? 3 : 0, fixture.Pet.CurrentPresentation.FrameIndex);
            Assert.IsTrue(fixture.Session.IsWriting);
            AssertCurrentCrop(fixture.Pet);
            fixture.Pet.ApplyPetAssets(HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "pip", source.Definition));
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            AssertCurrentCrop(fixture.Pet);
        }
        finally { File.Delete(manifestPath); File.Delete(sheetPath); Directory.Delete(directory); }
    }

    [TestMethod]
    [DataRow("hood", "save")]
    [DataRow("pip", "save")]
    [DataRow("crumb", "save")]
    [DataRow("puddle", "save")]
    [DataRow("hood", "cancel")]
    [DataRow("pip", "cancel")]
    [DataRow("crumb", "cancel")]
    [DataRow("puddle", "cancel")]
    [DataRow("hood", "failure")]
    [DataRow("pip", "failure")]
    [DataRow("crumb", "failure")]
    [DataRow("puddle", "failure")]
    public void PetPreviewSaveCancelAndFailureKeepWritingOnTheCorrectArtwork(string pet, string outcome)
    {
        var original = pet == "hood" ? "pip" : "hood";
        using var fixture = new Fixture(original);
        var controller = new HoodConfigurationController(fixture.Session, fixture.ConfigurationStore);
        Type(fixture.Editor, "a");
        fixture.Clock.Advance(240);
        var needs = fixture.Session.Needs;
        var position = fixture.Session.Position;
        var settings = new SettingsWindow(controller, fixture.Pet, UiStyleCatalog.StickyNotes);
        try
        {
            ((ComboBox)settings.FindName("PetComboBox")).SelectedValue = pet;
            Assert.AreEqual(pet, fixture.Pet.CurrentSource.Definition.Name.ToLowerInvariant());
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            AssertCurrentCrop(fixture.Pet);
            if (outcome == "failure")
            {
                using (var locked = new FileStream(fixture.ConfigurationPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    SaveSettings(settings);
                Assert.AreEqual(original, controller.Current.Pet);
                StringAssert.Contains(((TextBlock)settings.FindName("AppearanceErrorText")).Text, "Settings were not saved");
            }
            else if (outcome == "save") SaveSettings(settings);
            else settings.Close();
            fixture.Timer.Stop();
            var expected = outcome == "save" ? pet : original;
            Assert.AreEqual(expected, fixture.Pet.CurrentSource.Definition.Name.ToLowerInvariant());
            Assert.AreEqual(expected, fixture.ConfigurationStore.LoadOrCreate().Pet);
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            Assert.AreEqual(2, fixture.Pet.CurrentPresentation.FrameIndex);
            AssertCurrentCrop(fixture.Pet);
            Assert.AreEqual(needs, fixture.Session.Needs);
            Assert.AreEqual(position, fixture.Session.Position);
        }
        finally { settings.Close(); }
    }

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void WritingKeepsItsPhaseAndLogicalSizeAcrossScaleAndDpiChanges(string pet)
    {
        using var fixture = new Fixture(pet);
        Type(fixture.Editor, "a");
        fixture.Clock.Advance(240);
        fixture.Pet.ApplyPetScale(1.5);
        fixture.Pet.ReloadAssetsForDpi(1.5, 1.5);
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        Assert.AreEqual(2, fixture.Pet.CurrentPresentation.FrameIndex);
        Assert.AreEqual(288d, fixture.Pet.Width);
        Assert.AreEqual(312d, fixture.Pet.Height);
        AssertCurrentCrop(fixture.Pet);
        Assert.AreEqual(pet, fixture.Pet.CurrentSource.Definition.Name.ToLowerInvariant());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ControlEnterLeavesFormulaWithoutStartingWritingInTheNewBodyField(bool display)
    {
        using var fixture = new Fixture(document: new NotesDocument
        {
            Paragraphs = [new()
            {
                Kind = display ? NoteBlockKind.DisplayMath : NoteBlockKind.Paragraph,
                Runs = [new(display ? "$$x$$" : "$x$", Kind: display ? NoteInlineKind.Text : NoteInlineKind.InlineMath)]
            }]
        });
        var button = GetInlines(fixture.Editor.Document).OfType<InlineUIContainer>()
            .Select(inline => inline.Child).OfType<NoteInlineMathButton>().Single();
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Type(fixture.Editor, "y");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
        PressKey(fixture.Editor, Key.Enter, ModifierKeys.Control);
        Assert.IsNull(typeof(NoteWindow).GetField("_activeMathSource", Private)!.GetValue(fixture.Note));
        Assert.AreNotEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action,
            "The key belonged to the formula field that was left, not the new body field.");
        Type(fixture.Editor, "body");
        Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
    }

    [TestMethod]
    public void ArtworkReloadFailureLeavesTypingAndNoteSavingWorking()
    {
        using var fixture = new Fixture();
        var directory = Path.Combine(Path.GetTempPath(), $"HoodWritingReload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "hood.animations.json");
        var sheetPath = Path.Combine(directory, "hood_spritesheet.png");
        try
        {
            File.Copy(Path.Combine(fixture.Assets.Source.Directory, "hood.animations.json"), manifestPath);
            File.Copy(Path.Combine(fixture.Assets.Source.Directory, "hood_spritesheet.png"), sheetPath);
            var source = HoodAssets.ReadSourceForPet(directory, fixture.Assets.Definition);
            fixture.Pet.ApplyPetAssets(HoodAssets.LoadForDisplay(source, 1, 1, 1));
            var retained = fixture.Pet.CurrentAssets;
            Type(fixture.Editor, "a");
            File.WriteAllText(sheetPath, "invalid image");
            Assert.Throws<PetAssetException>(() => fixture.Pet.ReloadAssetsForDpi(1.5, 1.5));
            Assert.AreSame(retained, fixture.Pet.CurrentAssets);
            Type(fixture.Editor, "b");
            Assert.AreEqual("ab", fixture.Notes.GetNote(fixture.NoteId).Text);
            Assert.AreEqual(PetAction.Write, fixture.Pet.CurrentPresentation.Action);
            AssertCurrentCrop(fixture.Pet);
        }
        finally { File.Delete(manifestPath); File.Delete(sheetPath); Directory.Delete(directory); }
    }

    private static void PressKey(Control target, Key key, ModifierKeys modifiers)
    {
        ((NoteWindow)Window.GetWindow(target)).HandleNoteKey(key, modifiers, target);
        target.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void SaveSettings(SettingsWindow settings) => typeof(SettingsWindow).GetMethod("OnSaveClick", Private)!
        .Invoke(settings, [settings, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)]);

    private static void ClickCheckBox(CheckBox checkbox) => typeof(System.Windows.Controls.Primitives.ToggleButton)
        .GetMethod("OnClick", Private)!.Invoke(checkbox, null);

    private static void AssertCurrentCrop(HoodOverlayWindow pet)
    {
        pet.RefreshWritingPresentation();
        var assets = pet.CurrentAssets!;
        var presentation = pet.CurrentPresentation;
        var frame = assets.Definition.GetAnimation(presentation.Action).Frames[presentation.FrameIndex];
        var image = (CroppedBitmap)((Image)pet.FindName("PetImage")).Source;
        Assert.AreSame(assets.SpriteSheet, image.Source);
        Assert.AreEqual(assets.GetFrameRectangle(frame), image.SourceRect);
    }

    private static IEnumerable<Inline> GetInlines(FlowDocument document) => document.Blocks.OfType<Paragraph>()
        .SelectMany(paragraph => Flatten(paragraph.Inlines));

    private static IEnumerable<Inline> Flatten(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline;
            if (inline is Span span) foreach (var child in Flatten(span.Inlines)) yield return child;
        }
    }

    private static void Type(Control editor, string text)
    {
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, text));
        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string NotesPath = Path.Combine(Path.GetTempPath(), $"HoodWriting-{Guid.NewGuid():N}.json");
        public readonly LoadedHoodAssets Assets;
        public string ConfigurationPath => NotesPath + ".yaml";
        public readonly YamlConfigurationStore ConfigurationStore;
        public readonly FakeClock Clock = new();
        public readonly RecordingStore SessionStore = new();
        public readonly HoodSession Session;
        public readonly HoodOverlayWindow Pet;
        public readonly NotesApplication Notes;
        public readonly Guid NoteId;
        public readonly NoteWindow Note;
        public RichTextBox Editor => (RichTextBox)Note.FindName("Editor");
        public DispatcherTimer Timer => (DispatcherTimer)typeof(HoodOverlayWindow).GetField("_frameTimer", Private)!.GetValue(Pet)!;
        private readonly App _app;
        private readonly FieldInfo[] _fields;
        private readonly object?[] _originalValues;
        private readonly Dictionary<Guid, NoteWindow> _windows;
        private readonly Dictionary<Guid, NoteWindow> _originalWindows;

        public Fixture(string pet = "hood", HoodConfiguration? configuration = null, NotesDocument? document = null)
        {
            Assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);
            var defaults = configuration ?? HoodConfiguration.Defaults with
            { Behavior = HoodConfiguration.Defaults.Behavior with { IdleAnimations = false, RandomEvents = false, MediaHeadphones = false } };
            defaults = defaults with { Pet = pet };
            Session = HoodSession.Start(Assets.Definition, [new("Primary", new(0, 0, 1920, 1080), true)], new ZeroRandom(), SessionStore,
                defaults, new HoodSessionSnapshot { Position = new(500, 400), RemainingRandomInterval = TimeSpan.FromSeconds(5) });
            ConfigurationStore = new YamlConfigurationStore(ConfigurationPath);
            ConfigurationStore.Save(defaults);
            Action noop = () => { };
            Pet = new HoodOverlayWindow(Session, Assets.Source, Assets, noop, noop, noop, noop, noop, noop,
                UiStyleCatalog.StickyNotes, new SilentAudioProbe(), Clock);
            Notes = NotesApplication.Start(new JsonNotesStore(NotesPath));
            NoteId = Notes.CreateNote();
            if (document is not null) Notes.UpdateDocument(NoteId, document);
            Notes.SetGroupVisible(false);
            _app = System.Windows.Application.Current as App ?? new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _fields = new[] { "_notes", "_session", "_petWindow", "_effectiveNotesSettings", "_effectiveUiStyle", "_configurationStore" }
                .Select(name => typeof(App).GetField(name, Private)!).ToArray();
            _originalValues = _fields.Select(field => field.GetValue(_app)).ToArray();
            object?[] values = [Notes, Session, Pet, defaults.Notes, defaults.UiStyle, ConfigurationStore];
            for (var i = 0; i < _fields.Length; i++) _fields[i].SetValue(_app, values[i]);
            _windows = (Dictionary<Guid, NoteWindow>)typeof(App).GetField("_noteWindows", Private)!.GetValue(_app)!;
            _originalWindows = new(_windows);
            _windows.Clear();
            Restore();
            Note = _windows[NoteId];
            CommandManager.AddPreviewCanExecuteHandler(Editor, (_, args) =>
            {
                if (args.Command != ApplicationCommands.Paste) return;
                args.CanExecute = true;
                args.Handled = true;
            });
            Note.Opacity = 0;
            Note.Left = Note.Top = -10000;
            Notes.SetGroupVisible(true);
            Restore();
            Focus(Note);
        }

        public void Advance(int milliseconds)
        {
            var elapsed = TimeSpan.FromMilliseconds(milliseconds);
            Session.Advance(elapsed);
            Clock.Advance(milliseconds);
            Pet.AdvanceFocusAnimation(elapsed);
            Pet.AdvanceChecklistAnimation(elapsed);
            Pet.RefreshWritingPresentation();
        }

        private void Restore() => typeof(App).GetMethod("RestoreOpenNotes", Private)!.Invoke(_app, [false]);

        public NoteWindow AddNote()
        {
            var id = Notes.CreateNote();
            Notes.SetGroupVisible(false);
            Restore();
            var window = _windows[id];
            window.Opacity = 0;
            window.Left = window.Top = -10000;
            Notes.SetGroupVisible(true);
            Restore();
            return window;
        }

        public void Focus(NoteWindow window)
        {
            Timer.Stop();
            window.Activate();
            Assert.IsTrue(((RichTextBox)window.FindName("Editor")).Focus());
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.IsTrue(window.IsActive);
        }

        public void Trash(Guid noteId) => typeof(App).GetMethod("MoveNoteToTrash", Private)!.Invoke(_app, [noteId]);

        public void Power(PowerModes mode) => typeof(HoodOverlayWindow).GetMethod("ApplyPowerMode", Private)!.Invoke(Pet, [mode]);

        public void ToggleSprites() => typeof(App).GetMethod("ToggleSprites", Private)!.Invoke(_app, null);

        public void Dispose()
        {
            foreach (var window in _windows.Values.ToArray()) window.CloseWithoutMembershipChange();
            _windows.Clear();
            foreach (var pair in _originalWindows) _windows.Add(pair.Key, pair.Value);
            Pet.Close();
            for (var i = 0; i < _fields.Length; i++) _fields[i].SetValue(_app, _originalValues[i]);
            File.Delete(NotesPath);
            File.Delete(ConfigurationPath);
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int milliseconds) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
    private sealed class RecordingStore : ISessionStore
    {
        public HoodSessionSnapshot? Snapshot;
        public void Save(HoodSessionSnapshot snapshot) => Snapshot = snapshot;
    }
    private sealed class ZeroRandom : IPetRandomSource { public int NextIndex(int maximum) => 0; }
    private sealed class SilentAudioProbe : IMediaAudioProbe
    {
        public MediaAudioObservation Read() => MediaAudioObservation.Unavailable;
    }
    private sealed class TestApp : App { protected override void OnStartup(StartupEventArgs e) { } }
}
