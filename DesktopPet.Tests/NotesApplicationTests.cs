using System.Text.Json;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NotesApplicationTests
{
    [TestMethod]
    public void FailedTableSaveRetainsTheSavedTableAndRetriesTheEditableChanges()
    {
        var store = new SwitchableNotesStore();
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        var document = NoteTableTests.Parse();
        app.UpdateDocument(id, document);
        store.ShouldFail = true;
        var expanded = NoteTables.Change(document, document.Paragraphs[0].Cell!.TableId, NoteTableChange.InsertRow, 2);
        app.UpdateDocument(id, expanded);
        Assert.IsTrue(app.HasUnsavedChanges);
        Assert.AreEqual(7, store.Load()!.Notes.Single().Document.Paragraphs.Count);
        Assert.AreEqual(10, app.GetNote(id).Document.Paragraphs.Count);
        store.ShouldFail = false;
        Assert.IsTrue(app.Flush());
        Assert.IsFalse(app.HasUnsavedChanges);
        Assert.AreEqual(10, store.Load()!.Notes.Single().Document.Paragraphs.Count);
    }

    [TestMethod]
    public void TablesPersistSearchAndUndoStructuralAndAlignmentChanges()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonNotesStore(Path.Combine(directory.Path, "notes.json"));
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        var document = NoteTableTests.Parse();
        var tableId = document.Paragraphs[0].Cell!.TableId;
        app.UpdateDocument(id, document);
        var aligned = document.Copy();
        foreach (var paragraph in aligned.Paragraphs.Where(row => row.Cell?.Column == 0))
            paragraph.Cell = paragraph.Cell! with { Alignment = NoteCellAlignment.Right };
        app.UpdateDocument(id, aligned);
        Assert.AreEqual(NoteCellAlignment.Right, store.Load()!.Notes.Single().Document.Paragraphs[0].Cell!.Alignment);
        Assert.IsTrue(app.Undo(id));
        Assert.AreEqual(NoteCellAlignment.Left, app.GetNote(id).Document.Paragraphs[0].Cell!.Alignment);
        Assert.IsTrue(app.Redo(id));
        var expanded = NoteTables.Change(aligned, tableId, NoteTableChange.InsertRow, 2);
        app.UpdateDocument(id, expanded);
        Assert.IsTrue(app.Undo(id));
        Assert.AreEqual(7, app.GetNote(id).Document.Paragraphs.Count);
        Assert.IsTrue(app.Redo(id));
        var restarted = NotesApplication.Start(store);
        Assert.AreEqual(tableId, restarted.GetNote(id).Document.Paragraphs[0].Cell!.TableId);
        Assert.AreEqual(10, restarted.GetNote(id).Document.Paragraphs.Count);
        Assert.AreEqual(id, restarted.Search("Email").Single().Id);
        Assert.IsTrue(restarted.TryMoveToTrash(id, out _));
        Assert.IsTrue(restarted.TryRestoreFromTrash(id, out _));
        Assert.AreEqual(expanded.GetPlainText(), restarted.GetNote(id).Text);
    }

    [TestMethod]
    public void CodeFormattingSurvivesUndoRedoSaveRestartAndSearch()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonNotesStore(Path.Combine(directory.Path, "notes.json"));
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        var source = NotesDocument.FromText("Use `value`\r\n```\r\n  return value;\r\n```");
        app.UpdateDocument(id, source);
        var start = source.GetPlainText().Length;
        var length = 0;
        var formatted = NoteCodeFormatting.Apply(source, ref start, ref length);
        app.UpdateDocument(id, formatted);
        Assert.IsTrue(app.Undo(id));
        Assert.AreEqual(source.GetPlainText(), app.GetNote(id).Text);
        Assert.IsTrue(app.Redo(id));
        var restored = NotesApplication.Start(store);
        var note = restored.GetNote(id);
        Assert.AreEqual("Use value\r\n  return value;\r\n", note.Text);
        Assert.AreEqual(NoteTextStyle.Code, note.Document.Paragraphs[0].Runs.Last().Style);
        Assert.AreEqual(NoteBlockKind.Code, note.Document.Paragraphs[1].Kind);
        Assert.AreEqual(NoteBlockKind.Paragraph, note.Document.Paragraphs[2].Kind);
        Assert.AreEqual(id, restored.Search("return value").Single().Id);
        Assert.IsFalse(restored.HasUnsavedChanges);
    }

    [TestMethod]
    public void Rename_PersistsImmediatelyAndSurvivesCloseTrashAndRestoreWithoutChangingContent()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var store = new JsonNotesStore(path);
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        var otherId = app.CreateNote();
        var document = NotesDocument.FromText("Buy milk").SetStyle(0, 3, NoteTextStyle.Bold, true);
        var bounds = new NoteBounds(200, 140, 360, 320);
        app.UpdateDocument(id, document);
        app.UpdateBounds(id, bounds);
        app.UpdateColor(id, NoteColor.Blue);

        app.UpdateName(id, "Shopping 🛒");

        Assert.AreEqual("Shopping 🛒", store.Load()!.Notes.Single(note => note.Id == id).Name);
        Assert.AreEqual("Note", app.GetNote(otherId).DisplayName);
        Assert.IsFalse(app.HasUnsavedChanges);
        Assert.IsTrue(app.TryCloseNote(id, out _));
        Assert.IsTrue(app.TryMoveToTrash(id, out _));

        var restarted = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual("Shopping 🛒", restarted.Trash.Single().Name);
        Assert.AreEqual(id, restarted.SearchTrash("SHOPPING").Single().Id);
        Assert.IsTrue(restarted.TryRestoreFromTrash(id, out _));

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        var note = restored.GetNote(id);
        Assert.AreEqual("Shopping 🛒", note.Name);
        Assert.AreEqual("Buy milk", note.Text);
        Assert.AreEqual(NoteTextStyle.Bold, note.Document.Paragraphs[0].Runs[0].Style);
        Assert.AreEqual(bounds, note.Bounds);
        Assert.AreEqual(NoteColor.Blue, note.Color);
        Assert.IsFalse(restored.OpenNoteIds.Contains(id));
    }

    [TestMethod]
    public void Search_MatchesNamesAndContentAndDisplaysUnnamedNotesWithFallback()
    {
        var app = NotesApplication.Start(new SwitchableNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NotesDocument.FromText("Buy milk"));
        app.UpdateName(id, "Shopping");

        Assert.AreEqual("Shopping", app.Search("SHOPPING").Single().Name);
        Assert.AreEqual(id, app.Search("MILK").Single().Id);
        Assert.AreEqual(0, app.Search("missing").Count);
        app.UpdateName(id, "Renamed");
        Assert.AreEqual(0, app.Search("Shopping").Count);
        app.UpdateName(id, " ");
        Assert.AreEqual("Note", app.Search(null).Single().Name);
        app.UpdateName(id, string.Empty);
        Assert.AreEqual("Note", app.GetNote(id).DisplayName);
        Assert.IsTrue(app.TryMoveToTrash(id, out _));
        Assert.AreEqual("Note", app.SearchTrash("MILK").Single().Name);
    }

    [TestMethod]
    public void Rename_WhenSaveFailsRetainsLatestNameForRetryAndLeavesSavedNameIntact()
    {
        var store = new SwitchableNotesStore();
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        app.UpdateName(id, "Saved name");
        store.ShouldFail = true;
        app.UpdateName(id, "Pending name");
        app.UpdateName(id, "Latest name");

        Assert.IsTrue(app.HasUnsavedChanges);
        Assert.IsNotNull(app.LastSaveError);
        Assert.AreEqual("Latest name", app.GetNote(id).Name);
        Assert.AreEqual("Saved name", store.Load()!.Notes.Single().Name);
        store.ShouldFail = false;
        Assert.IsTrue(app.Flush());
        Assert.AreEqual("Latest name", NotesApplication.Start(store).GetNote(id).Name);
    }

    [TestMethod]
    public void Rename_RejectsInvalidNamesAndUnknownNotesWithoutChangingSavedState()
    {
        var store = new SwitchableNotesStore();
        var app = NotesApplication.Start(store);
        var id = app.CreateNote();
        app.UpdateName(id, "Saved name");

        foreach (var invalid in new[] { null, "first\rsecond", "first\nsecond", new string('x', NotesSnapshotValidator.MaximumTextCharacters + 1) })
        {
            Assert.Throws<InvalidDataException>(() => app.UpdateName(id, invalid!));
            var snapshot = store.Load()!;
            snapshot.Notes[0] = snapshot.Notes[0] with { Name = invalid! };
            Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.Validate(snapshot));
        }

        Assert.Throws<ArgumentException>(() => app.UpdateName(Guid.NewGuid(), "Unknown"));
        Assert.AreEqual("Saved name", app.GetNote(id).Name);
        Assert.AreEqual("Saved name", store.Load()!.Notes.Single().Name);
        Assert.IsFalse(app.HasUnsavedChanges);
    }

    [TestMethod]
    public void JsonNotesStore_LoadsExistingVersionThreeNotesWithoutNamesAndAllowsRenaming()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var id = Guid.NewGuid();
        File.WriteAllText(path, $$"""
            {
              "schemaVersion": 3,
              "notes": [{ "id": "{{id}}", "document": { "paragraphs": [{ "runs": [{ "text": "Existing text" }] }] } }],
              "openNoteIds": ["{{id}}"],
              "groupVisible": true,
              "firstActivationCompleted": true
            }
            """);
        var app = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual("Note", app.GetNote(id).DisplayName);
        app.UpdateName(id, "Existing note renamed");

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual("Existing note renamed", restored.GetNote(id).Name);
        Assert.AreEqual("Existing text", restored.GetNote(id).Text);
        Assert.IsTrue(restored.GroupVisible);
        Assert.AreEqual(id, restored.OpenNoteIds.Single());
    }

    [TestMethod]
    public void Activate_FirstUseCreatesOneStableNoteAndRepeatedTogglesPreserveBoundsAndMembership()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var store = new JsonNotesStore(path);
        var app = NotesApplication.Start(store);

        Assert.IsFalse(app.FirstActivationCompleted);
        Assert.AreEqual(0, app.Notes.Count);

        var first = app.Activate();
        var noteId = first.NoteId!.Value;

        Assert.AreEqual(NotesActivationKind.CreatedFirstNote, first.Kind);
        Assert.AreEqual(1, app.Notes.Count);
        Assert.AreEqual(noteId, app.Notes[0].Id);
        Assert.AreEqual(string.Empty, app.Notes[0].Text);
        Assert.IsTrue(app.GroupVisible);

        var bounds = new NoteBounds(450, 220, 360, 320);
        app.UpdateBounds(noteId, bounds);
        var closedId = app.CreateNote();
        Assert.IsTrue(app.TryCloseNote(closedId, out _));

        for (var toggle = 0; toggle < 10; toggle++)
        {
            var expected = toggle % 2 == 0 ? NotesActivationKind.Hidden : NotesActivationKind.Shown;
            Assert.AreEqual(expected, app.Activate().Kind);
            Assert.AreEqual(bounds, app.GetNote(noteId).Bounds);
            CollectionAssert.AreEqual(new[] { noteId }, app.OpenNoteIds.ToArray());
            Assert.IsFalse(app.OpenNoteIds.Contains(closedId));
        }

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        Assert.IsTrue(restored.GroupVisible);
        Assert.AreEqual(bounds, restored.GetNote(noteId).Bounds);
        CollectionAssert.AreEqual(new[] { noteId }, restored.OpenNoteIds.ToArray());
        Assert.IsFalse(restored.OpenNoteIds.Contains(closedId));
        Assert.AreEqual(2, app.Notes.Count);
        Assert.AreEqual(noteId, app.Notes[0].Id);
    }

    [TestMethod]
    public void Start_DoesNotSeedOrPersistAFirstNoteBeforeUserActivation()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));

        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(app.FirstActivationCompleted);
        Assert.AreEqual(0, app.Notes.Count);
    }

    [TestMethod]
    public void JsonNotesStore_RestoresMultilineTextBoundsAndHiddenGroup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var store = new JsonNotesStore(path);
        var app = NotesApplication.Start(store);
        var noteId = app.Activate().NoteId!.Value;
        var bounds = new NoteBounds(-1400, 72, 440, 360);

        app.UpdateDocument(noteId, NotesDocument.FromText("First line\r\nSecond line\r\n"));
        app.UpdateBounds(noteId, bounds);
        app.SetGroupVisible(false);

        var restored = NotesApplication.Start(new JsonNotesStore(path));

        Assert.IsTrue(restored.FirstActivationCompleted);
        Assert.IsFalse(restored.GroupVisible);
        Assert.AreEqual(noteId, restored.OpenNoteIds.Single());
        Assert.AreEqual("First line\r\nSecond line\r\n", restored.Notes.Single().Text);
        Assert.AreEqual(bounds, restored.Notes.Single().Bounds);
    }

    [TestMethod]
    public void AcceptedEdit_IsPersistedImmediatelyWithoutAnExplicitFlush()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonNotesStore(Path.Combine(directory.Path, "notes.json"));
        var app = NotesApplication.Start(store);
        var noteId = app.CreateNote();

        app.UpdateDocument(noteId, NotesDocument.FromText("saved as soon as the edit was accepted"));

        Assert.AreEqual("saved as soon as the edit was accepted", store.Load()!.Notes.Single().Text);
        Assert.IsFalse(app.HasUnsavedChanges);
    }

    [TestMethod]
    public void JsonNotesStore_LeavesDamagedSourceUntouched()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        const string damaged = "{ damaged notes";
        File.WriteAllText(path, damaged);

        var exception = Assert.Throws<NotesStoreException>(() => NotesApplication.Start(new JsonNotesStore(path)));

        Assert.AreEqual(damaged, File.ReadAllText(path));
        StringAssert.Contains(exception.Message, "did not replace it");
    }

    [TestMethod]
    public void JsonNotesStore_WhenReplacementFailsRetainsPreviousDataAndNoteCanRetryBeforeClose()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var noteId = app.Activate().NoteId!.Value;
        const string original = "saved text";
        app.UpdateDocument(noteId, NotesDocument.FromText(original));
        var savedFile = File.ReadAllText(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            app.UpdateDocument(noteId, NotesDocument.FromText("new text that did not save"));
            app.UpdateDocument(noteId, NotesDocument.FromText("latest pending text"));
            Assert.IsTrue(app.HasUnsavedChanges);
            Assert.IsNotNull(app.LastSaveError);
            Assert.IsFalse(app.TryCloseNote(noteId, out var error));
            Assert.IsNotNull(error);
            Assert.AreEqual("latest pending text", app.Notes.Single().Text);
        }

        Assert.AreEqual(savedFile, File.ReadAllText(path));
        Assert.IsTrue(app.Flush());
        Assert.IsFalse(app.HasUnsavedChanges);
        Assert.IsTrue(app.TryCloseNote(noteId, out _));
        Assert.AreEqual(0, app.OpenNoteIds.Count);

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual("latest pending text", restored.Notes.Single().Text);
        Assert.AreEqual(0, restored.OpenNoteIds.Count);
    }

    [TestMethod]
    public void NotesApplication_RetrySavesLatestInjectedFailureWithoutDuplicatingNotes()
    {
        var store = new SwitchableNotesStore();
        var app = NotesApplication.Start(store);
        var noteId = app.CreateNote();
        var savedSnapshot = store.Snapshot!;
        store.ShouldFail = true;

        app.UpdateDocument(noteId, NotesDocument.FromText("first pending version"));
        app.UpdateDocument(noteId, NotesDocument.FromText("latest pending version"));
        Assert.IsNotNull(app.UpdateColor(noteId, NoteColor.Lavender));

        Assert.IsTrue(app.HasUnsavedChanges);
        Assert.IsNotNull(app.LastSaveError);
        Assert.AreEqual(string.Empty, savedSnapshot.Notes.Single().Text);
        Assert.AreEqual("latest pending version", app.GetNote(noteId).Text);
        Assert.AreEqual(1, app.Notes.Count);

        Assert.IsFalse(app.Flush());
        store.ShouldFail = false;
        Assert.IsTrue(app.Flush());
        Assert.IsFalse(app.HasUnsavedChanges);
        Assert.IsNull(app.LastSaveError);
        Assert.AreEqual(noteId, store.Snapshot!.Notes.Single().Id);
        Assert.AreEqual("latest pending version", store.Snapshot.Notes.Single().Text);
        Assert.AreEqual(NoteColor.Lavender, store.Snapshot.Notes.Single().Color);
        Assert.AreEqual(1, store.Snapshot.Notes.Count);
    }

    [TestMethod]
    public void JsonNotesStore_ArchivesDamagedNotesOnlyAfterResetIsRequested()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        const string damaged = "{ broken notes";
        File.WriteAllText(path, damaged);
        var store = new JsonNotesStore(path);

        var exception = Assert.Throws<NotesStoreException>(() => NotesApplication.Start(store));
        Assert.AreEqual(damaged, File.ReadAllText(path));

        var archivedPath = store.ResetAfterAcknowledgement();

        Assert.IsNotNull(archivedPath);
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(damaged, File.ReadAllText(archivedPath));
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
        Assert.AreEqual(0, NotesApplication.Start(store).Notes.Count);
        StringAssert.Contains(exception.Message, "did not replace it");
    }

    [TestMethod]
    public void JsonNotesStore_WhenDamagedNotesCannotBeArchivedLeavesTheSourceInPlace()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        const string damaged = "{ broken notes";
        File.WriteAllText(path, damaged);
        var store = new JsonNotesStore(path);

        NotesStoreException exception;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            exception = Assert.Throws<NotesStoreException>(() => store.ResetAfterAcknowledgement());
            Assert.AreEqual(damaged, File.ReadAllText(path));
        }

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(damaged, File.ReadAllText(path));
        StringAssert.Contains(exception.Message, "original file was left in place");
    }

    [TestMethod]
    [DataRow("\"3\"")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("true")]
    public void JsonNotesStore_NonnumericSchemaVersionUsesAcknowledgedRecovery(string version)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var damaged = "{\"schemaVersion\":" + version + "}";
        File.WriteAllText(path, damaged);
        var store = new JsonNotesStore(path);

        Assert.Throws<NotesStoreException>(() => NotesApplication.Start(store));
        Assert.AreEqual(damaged, File.ReadAllText(path));
        var archived = store.ResetAfterAcknowledgement();
        Assert.IsNotNull(archived);
        Assert.AreEqual(damaged, File.ReadAllText(archived));
        Assert.AreEqual(0, NotesApplication.Start(store).Notes.Count);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(100)]
    [DataRow(200)]
    public void CreateNote_AtCapacityExplainsHowToFreeSpaceAndPreservesNotes(int trashCount)
    {
        var store = new SwitchableNotesStore();
        store.Save(new NotesSnapshot
        {
            Notes = Enumerable.Range(0, NotesSnapshotValidator.MaximumNotes - trashCount).Select(_ => new HoodNote()).ToList(),
            Trash = Enumerable.Range(0, trashCount).Select(_ => new HoodNote()).ToList(),
            FirstActivationCompleted = true
        });
        var app = NotesApplication.Start(store);
        var original = JsonSerializer.Serialize(store.Load());

        var error = Assert.Throws<InvalidDataException>(() => app.CreateNote());
        StringAssert.Contains(error.Message, "Permanently delete");
        Assert.AreEqual(original, JsonSerializer.Serialize(store.Load()));
        Assert.AreEqual(NotesSnapshotValidator.MaximumNotes, app.Notes.Count + app.Trash.Count);
        Assert.IsFalse(app.HasUnsavedChanges);
    }

    [TestMethod]
    public void JsonNotesStore_RejectsUnsupportedSchemaAndInvalidBoundsWithoutChangingSource()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var store = new JsonNotesStore(path);
        var note = new HoodNote();
        store.Save(new NotesSnapshot
        {
            Notes = [note],
            OpenNoteIds = [note.Id],
            GroupVisible = true,
            FirstActivationCompleted = true
        });
        var valid = File.ReadAllText(path);

        foreach (var damaged in new[]
        {
            valid.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 99", StringComparison.Ordinal),
            valid.Replace("\"width\": 320", "\"width\": 0", StringComparison.Ordinal),
            valid.Replace("\"kind\": \"paragraph\"", "\"kind\": \"unsupported\"", StringComparison.Ordinal)
        })
        {
            File.WriteAllText(path, damaged);
            Assert.Throws<NotesStoreException>(() => NotesApplication.Start(store));
            Assert.AreEqual(damaged, File.ReadAllText(path));
        }
    }

    [TestMethod]
    public void CreateBeforeHoodActivation_PreventsASecondFirstUseNoteAndSearchUsesReadableText()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));

        var firstId = app.CreateNote();
        app.UpdateDocument(firstId, NotesDocument.FromText("Meeting\r\nbring the design sketch"));
        var secondId = app.CreateNote();
        app.UpdateDocument(secondId, NotesDocument.FromText("Groceries: apples, bread, and tea"));

        Assert.IsTrue(app.FirstActivationCompleted);
        Assert.AreEqual(2, app.Notes.Count);
        Assert.AreEqual(NotesActivationKind.Hidden, app.Activate().Kind);
        Assert.AreEqual(NotesActivationKind.Shown, app.Activate().Kind);
        Assert.AreEqual(2, app.Notes.Count);

        var results = app.Search("DESIGN");
        Assert.AreEqual(firstId, results.Single().Id);
        Assert.IsTrue(results.Single().IsOpen);
        Assert.AreEqual("Meeting bring the design sketch", results.Single().Preview);
        Assert.AreEqual(0, app.Search("not present").Count);
    }

    [TestMethod]
    public void DefaultAndIndividualColorsPersistIndependentlyAcrossRestart()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path), NoteColor.Blue);
        var firstId = app.Activate().NoteId!.Value;
        Assert.AreEqual(NoteColor.Blue, app.GetNote(firstId).Color);

        Assert.IsNull(app.UpdateColor(firstId, NoteColor.Green));
        app.SetDefaultColor(NoteColor.Pink);
        var secondId = app.CreateNote();
        Assert.AreEqual(NoteColor.Green, app.GetNote(firstId).Color);
        Assert.AreEqual(NoteColor.Pink, app.GetNote(secondId).Color);

        var restored = NotesApplication.Start(new JsonNotesStore(path), NoteColor.Lavender);
        Assert.AreEqual(NoteColor.Green, restored.GetNote(firstId).Color);
        Assert.AreEqual(NoteColor.Pink, restored.GetNote(secondId).Color);
        Assert.AreEqual("#FFEDE3FA", NoteColorPalette.GetBackgroundHex(NoteColor.Lavender, darkTheme: false));
    }

    [TestMethod]
    public void UpdateColor_WhenStorageFailsKeepsTheRequestedColorForRetryAndPreservesTheFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var noteId = app.CreateNote();
        var originalFile = File.ReadAllText(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = app.UpdateColor(noteId, NoteColor.Blue);

            Assert.IsNotNull(error);
            Assert.AreEqual(NoteColor.Blue, app.GetNote(noteId).Color);
            Assert.IsTrue(app.HasUnsavedChanges);
            Assert.AreEqual(originalFile, File.ReadAllText(path));
        }

        Assert.IsTrue(app.Flush());
        Assert.AreEqual(NoteColor.Blue, NotesApplication.Start(new JsonNotesStore(path)).GetNote(noteId).Color);
    }

    [TestMethod]
    public void ClosingAndOpeningOneNoteChangesOnlyItsGroupMembershipAndRestoresItAfterRestart()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var firstId = app.CreateNote();
        app.UpdateDocument(firstId, NotesDocument.FromText("First note"));
        var secondId = app.CreateNote();
        app.UpdateDocument(secondId, NotesDocument.FromText("Second note"));

        Assert.IsTrue(app.TryCloseNote(firstId, out _));
        Assert.AreEqual(secondId, app.OpenNoteIds.Single());
        Assert.AreEqual(2, app.Search(string.Empty).Count);

        app.OpenNote(firstId);
        Assert.AreEqual(2, app.OpenNoteIds.Count);
        Assert.AreEqual(2, NotesApplication.Start(new JsonNotesStore(path)).OpenNoteIds.Count);

        Assert.IsTrue(app.TryCloseNote(firstId, out _));
        Assert.IsTrue(app.TryCloseNote(secondId, out _));
        Assert.AreEqual(NotesActivationKind.ShowNotesList, app.Activate().Kind);
        Assert.AreEqual(2, app.Notes.Count);
        Assert.AreEqual(0, app.OpenNoteIds.Count);
    }

    [TestMethod]
    public void GroupVisibilityRestoresSavedBoundsContentAndOpenMembershipWithoutDuplicates()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var visibleId = app.CreateNote();
        var bounds = new NoteBounds(450, 220, 360, 320);
        app.UpdateBounds(visibleId, bounds);
        app.UpdateDocument(visibleId, NotesDocument.FromText("keep this note"));
        var closedId = app.CreateNote();
        Assert.IsTrue(app.TryCloseNote(closedId, out _));
        app.SetGroupVisible(false);

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        Assert.IsFalse(restored.GroupVisible);
        Assert.AreEqual(visibleId, restored.OpenNoteIds.Single());
        Assert.AreEqual(bounds, restored.GetNote(visibleId).Bounds);
        Assert.AreEqual("keep this note", restored.GetNote(visibleId).Text);
        Assert.AreEqual(NotesActivationKind.Shown, restored.Activate().Kind);
        Assert.AreEqual(2, restored.Notes.Count);
        Assert.AreEqual(visibleId, restored.OpenNoteIds.Single());
        Assert.IsFalse(restored.OpenNoteIds.Contains(closedId));
    }

    [TestMethod]
    public void VisibilityTransitionCoordinator_IgnoresStaleCompletionsAndTracksLatestIntent()
    {
        var coordinator = new NotesVisibilityTransitionCoordinator();
        var hide = coordinator.Request(visible: false);
        var show = coordinator.Request(visible: true);

        Assert.IsFalse(coordinator.IsCurrent(hide));
        Assert.IsTrue(coordinator.IsCurrent(show));
        var latestHide = coordinator.Request(visible: false);
        Assert.IsFalse(coordinator.IsCurrent(show));
        Assert.IsTrue(coordinator.IsCurrent(latestHide));
        Assert.IsFalse(latestHide.Visible);
    }

    [TestMethod]
    public void NotesDocument_CombinesAndRemovesInlineStylesWithoutChangingReadableText()
    {
        var document = NotesDocument.FromText("plain emphasis\r\nsecond line")
            .SetStyle(6, 9, NoteTextStyle.Bold, enabled: true)
            .SetStyle(6, 9, NoteTextStyle.Italic, enabled: true)
            .SetStyle(6, 9, NoteTextStyle.Bold, enabled: false);

        Assert.AreEqual("plain emphasis\r\nsecond line", document.GetPlainText());
        Assert.AreEqual(NoteTextStyle.None, document.Paragraphs[0].Runs[0].Style);
        Assert.AreEqual(NoteTextStyle.Italic, document.Paragraphs[0].Runs.Single(run => run.Text == "emphasis").Style);
        Assert.AreEqual("second line", document.Paragraphs[1].Runs.Single().Text);
    }

    [TestMethod]
    public void JsonNotesStore_MigratesVersionOnePlainTextWithoutChangingIdentityOrOpenState()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var noteId = Guid.NewGuid();
        var original = $$"""
            {
              "schemaVersion": 1,
              "notes": [{
                "id": "{{noteId}}",
                "text": "first line\r\nsecond line",
                "bounds": { "left": 24, "top": 48, "width": 320, "height": 300 }
              }],
              "openNoteIds": ["{{noteId}}"],
              "groupVisible": true,
              "firstActivationCompleted": true
            }
            """;
        File.WriteAllText(path, original);

        var store = new JsonNotesStore(path);
        var migrated = store.Load()!;

        Assert.AreEqual(3, migrated.SchemaVersion);
        Assert.AreEqual(noteId, migrated.Notes.Single().Id);
        Assert.AreEqual(NoteColor.Yellow, migrated.Notes.Single().Color);
        Assert.AreEqual("first line\r\nsecond line", migrated.Notes.Single().Text);
        Assert.AreEqual(noteId, migrated.OpenNoteIds.Single());
        Assert.IsTrue(migrated.GroupVisible);

        store.Save(migrated);
        var restored = store.Load()!;
        Assert.AreEqual("first line\r\nsecond line", restored.Notes.Single().Text);
        Assert.AreEqual(3, restored.SchemaVersion);
    }

    [TestMethod]
    public void JsonNotesStore_LoadsVersionTwoNotesCreatedBeforeColorWasAdded()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var noteId = Guid.NewGuid();
        File.WriteAllText(path, $$"""
            {
              "schemaVersion": 2,
              "notes": [{
                "id": "{{noteId}}",
                "document": { "paragraphs": [{ "kind": "paragraph", "runs": [{ "text": "old note", "style": "none" }] }] },
                "bounds": { "left": 96, "top": 96, "width": 320, "height": 300 }
              }],
              "openNoteIds": ["{{noteId}}"],
              "groupVisible": true,
              "firstActivationCompleted": true
            }
            """);

        var loaded = NotesApplication.Start(new JsonNotesStore(path));

        Assert.AreEqual(3, new JsonNotesStore(path).Load()!.SchemaVersion);
        Assert.AreEqual("old note", loaded.GetNote(noteId).Text);
        Assert.AreEqual(NoteColor.Yellow, loaded.GetNote(noteId).Color);
    }

    [TestMethod]
    public void TrashLifecycle_PreservesFormattedNoteAndRestoresItClosedAfterRestart()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var trashedId = app.CreateNote();
        var bounds = new NoteBounds(360, 180, 420, 340);
        var document = NotesDocument.FromText("first task\r\nsecond task")
            .SetStyle(6, 4, NoteTextStyle.Bold, true)
            .SetListKind(0, "first task\r\nsecond task".Length, NoteBlockKind.Checklist)
            .SetChecklistChecked(1, true);
        app.UpdateDocument(trashedId, document);
        app.UpdateBounds(trashedId, bounds);
        Assert.IsNull(app.UpdateColor(trashedId, NoteColor.Lavender));
        var remainingId = app.CreateNote();

        Assert.IsTrue(app.TryMoveToTrash(trashedId, out var moveError), moveError);
        Assert.AreEqual(1, app.Notes.Count);
        Assert.AreEqual(remainingId, app.OpenNoteIds.Single());
        Assert.IsTrue(app.GroupVisible);
        Assert.AreEqual(0, app.Search("first task").Count);
        Assert.AreEqual(trashedId, app.SearchTrash("SECOND").Single().Id);

        var restarted = NotesApplication.Start(new JsonNotesStore(path));
        var inTrash = restarted.Trash.Single();
        Assert.AreEqual(trashedId, inTrash.Id);
        Assert.AreEqual(document.GetPlainText(), inTrash.Text);
        Assert.AreEqual(NoteTextStyle.Bold, inTrash.Document.Paragraphs[0].Runs.Single(run => run.Text == "task").Style);
        Assert.AreEqual(NoteBlockKind.Checklist, inTrash.Document.Paragraphs[0].Kind);
        Assert.IsTrue(inTrash.Document.Paragraphs[1].IsChecked);
        Assert.AreEqual(NoteColor.Lavender, inTrash.Color);
        Assert.AreEqual(bounds, inTrash.Bounds);

        Assert.IsTrue(restarted.TryRestoreFromTrash(trashedId, out var restoreError), restoreError);
        Assert.AreEqual(0, restarted.Trash.Count);
        Assert.AreEqual(2, restarted.Notes.Count);
        Assert.IsFalse(restarted.OpenNoteIds.Contains(trashedId));
        Assert.AreEqual(document.GetPlainText(), restarted.GetNote(trashedId).Text);
        restarted.OpenNote(trashedId);
        Assert.AreEqual(2, restarted.Notes.Count);
        Assert.AreEqual(2, restarted.OpenNoteIds.Count);

        var finalRestart = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual(2, finalRestart.Notes.Count);
        Assert.AreEqual(0, finalRestart.Trash.Count);
        Assert.IsTrue(finalRestart.OpenNoteIds.Contains(trashedId));
    }

    [TestMethod]
    public void JsonNotesStore_RoundTripsFormattingColorsBoundsMembershipAndTrashTogether()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var store = new JsonNotesStore(path);
        var app = NotesApplication.Start(store);

        var openId = app.CreateNote();
        var richText = "Overview\r\nfirst task\r\nsecond task";
        var taskStart = richText.IndexOf("first task", StringComparison.Ordinal);
        var document = NotesDocument.FromText(richText)
            .SetStyle(0, "Overview".Length, NoteTextStyle.Bold, true)
            .SetStyle(taskStart, 5, NoteTextStyle.Bold, true)
            .SetStyle(taskStart, 5, NoteTextStyle.Italic, true)
            .SetListKind(taskStart, "first task\r\nsecond task".Length, NoteBlockKind.Checklist)
            .SetChecklistChecked(2, true);
        app.UpdateDocument(openId, document);
        app.UpdateBounds(openId, new NoteBounds(360, 120, 480, 360));
        Assert.IsNull(app.UpdateColor(openId, NoteColor.Blue));

        var closedId = app.CreateNote();
        app.UpdateDocument(closedId, NotesDocument.FromText("Closed meeting notes"));
        Assert.IsTrue(app.TryCloseNote(closedId, out var closeError), closeError);

        var trashedId = app.CreateNote();
        app.UpdateDocument(trashedId, NotesDocument.FromText("Retained in Trash"));
        app.UpdateBounds(trashedId, new NoteBounds(880, 180, 360, 300));
        Assert.IsNull(app.UpdateColor(trashedId, NoteColor.Lavender));
        Assert.IsTrue(app.TryMoveToTrash(trashedId, out var trashError), trashError);
        app.SetGroupVisible(false);

        var restored = NotesApplication.Start(store);
        var restoredOpenNote = restored.GetNote(openId);
        Assert.AreEqual(richText, restoredOpenNote.Text);
        Assert.AreEqual(NoteColor.Blue, restoredOpenNote.Color);
        Assert.AreEqual(new NoteBounds(360, 120, 480, 360), restoredOpenNote.Bounds);
        Assert.AreEqual(NoteTextStyle.Bold, restoredOpenNote.Document.Paragraphs[0].Runs.Single().Style);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic,
            restoredOpenNote.Document.Paragraphs[1].Runs.Single(run => run.Text == "first").Style);
        Assert.AreEqual(NoteBlockKind.Checklist, restoredOpenNote.Document.Paragraphs[2].Kind);
        Assert.IsTrue(restoredOpenNote.Document.Paragraphs[2].IsChecked);
        Assert.AreEqual("Closed meeting notes", restored.GetNote(closedId).Text);
        Assert.IsFalse(restored.OpenNoteIds.Contains(closedId));
        Assert.AreEqual("Retained in Trash", restored.Trash.Single(note => note.Id == trashedId).Text);
        Assert.AreEqual(NoteColor.Lavender, restored.Trash.Single(note => note.Id == trashedId).Color);
        CollectionAssert.AreEqual(new[] { openId }, restored.OpenNoteIds.ToArray());
        Assert.IsFalse(restored.GroupVisible);
        Assert.IsTrue(restored.FirstActivationCompleted);
    }

    [TestMethod]
    public void TrashAndPermanentDelete_FlushPendingEditsAndDoNotCommitWhenStorageFails()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var firstId = app.CreateNote();
        app.UpdateDocument(firstId, NotesDocument.FromText("saved before delete"));
        var secondId = app.CreateNote();
        Assert.IsTrue(app.TryMoveToTrash(firstId, out _));
        var savedFile = File.ReadAllText(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            app.UpdateDocument(secondId, NotesDocument.FromText("pending edit"));
            Assert.IsFalse(app.TryRestoreFromTrash(firstId, out var restoreError));
            Assert.IsNotNull(restoreError);
            Assert.IsFalse(app.TryPermanentlyDeleteFromTrash(firstId, confirmed: true, out var deleteError));
            Assert.IsNotNull(deleteError);
            Assert.AreEqual(firstId, app.Trash.Single().Id);
            Assert.AreEqual(1, app.Notes.Count);
            Assert.AreEqual(savedFile, File.ReadAllText(path));
        }

        Assert.IsTrue(app.TryMoveToTrash(secondId, out _));
        Assert.AreEqual("pending edit", app.Trash.Single(note => note.Id == secondId).Text);
        Assert.AreEqual(NotesActivationKind.ShowNotesList, app.Activate().Kind);
        var beforeCancel = File.ReadAllText(path);
        Assert.IsFalse(app.TryPermanentlyDeleteFromTrash(firstId, confirmed: false, out var cancelError));
        Assert.IsNull(cancelError);
        Assert.AreEqual(2, app.Trash.Count);
        Assert.IsTrue(app.Trash.Any(note => note.Id == firstId));
        Assert.AreEqual(beforeCancel, File.ReadAllText(path));
        Assert.IsTrue(app.TryPermanentlyDeleteFromTrash(firstId, confirmed: true, out _));
        Assert.AreEqual(secondId, app.Trash.Single().Id);

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        Assert.AreEqual(1, restored.Trash.Count);
        Assert.AreEqual(secondId, restored.Trash.Single().Id);
        Assert.AreEqual(0, restored.Notes.Count);
    }

    [TestMethod]
    public void NotesSnapshotValidator_RejectsOpenMembershipForTrashedNotes()
    {
        var note = new HoodNote();
        var snapshot = new NotesSnapshot
        {
            Trash = [note],
            OpenNoteIds = [note.Id],
            GroupVisible = true,
            FirstActivationCompleted = true
        };

        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.Validate(snapshot));
    }

    [TestMethod]
    public void NotesApplication_UndoAndRedoAreIndependentForEachNote()
    {
        using var directory = new TemporaryDirectory();
        var app = NotesApplication.Start(new JsonNotesStore(Path.Combine(directory.Path, "notes.json")));
        var firstId = app.CreateNote();
        var secondId = app.CreateNote();
        app.UpdateDocument(firstId, NotesDocument.FromText("first note"));
        app.UpdateDocument(firstId, NotesDocument.FromText("first note").SetStyle(0, 5, NoteTextStyle.Bold, true));

        Assert.IsTrue(app.CanUndo(firstId));
        Assert.IsFalse(app.CanUndo(secondId));
        Assert.IsTrue(app.Undo(firstId));
        Assert.AreEqual(NoteTextStyle.None, app.GetNote(firstId).Document.Paragraphs[0].Runs.Single().Style);
        Assert.AreEqual(string.Empty, app.GetNote(secondId).Text);
        Assert.IsTrue(app.Redo(firstId));
        Assert.AreEqual(NoteTextStyle.Bold, app.GetNote(firstId).Document.Paragraphs[0].Runs.Single(run => run.Text == "first").Style);
        Assert.IsFalse(app.CanRedo(firstId));
    }

    [TestMethod]
    public void JsonNotesStore_RoundTripsCombinedFormattingThroughRealStorage()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var noteId = app.CreateNote();
        var document = NotesDocument.FromText("keep bold and italic")
            .SetStyle(5, 15, NoteTextStyle.Bold, true)
            .SetStyle(10, 4, NoteTextStyle.Italic, true);
        app.UpdateDocument(noteId, document);

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        var restoredNote = restored.GetNote(noteId);

        Assert.AreEqual("keep bold and italic", restoredNote.Text);
        Assert.AreEqual(NoteTextStyle.Bold, restoredNote.Document.Paragraphs[0].Runs.Single(run => run.Text == "bold ").Style);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, restoredNote.Document.Paragraphs[0].Runs.Single(run => run.Text == "and ").Style);
        Assert.AreEqual(NoteTextStyle.Bold, restoredNote.Document.Paragraphs[0].Runs.Single(run => run.Text == "italic").Style);
    }

    [TestMethod]
    public void NotesDocument_BulletedAndNumberedListsPreserveTextFormattingAndEndEmptyItems()
    {
        var plain = NotesDocument.FromText("topic\r\nfirst\r\nsecond\r\nafter");
        var firstStart = plain.GetPlainText().IndexOf("first", StringComparison.Ordinal);
        var listLength = "first\r\nsecond".Length;
        var listed = plain
            .SetStyle(firstStart, "first".Length, NoteTextStyle.Bold, true)
            .SetListKind(firstStart, listLength, NoteBlockKind.Bulleted);

        Assert.AreEqual("topic\r\nfirst\r\nsecond\r\nafter", listed.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Bulleted, listed.GetListKind(firstStart, listLength));
        Assert.AreEqual(NoteTextStyle.Bold, listed.Paragraphs[1].Runs.Single().Style);

        var numbered = listed.SetListKind(firstStart, listLength, NoteBlockKind.Numbered);
        Assert.AreEqual(NoteBlockKind.Numbered, numbered.Paragraphs[1].Kind);
        Assert.AreEqual(NoteBlockKind.Numbered, numbered.Paragraphs[2].Kind);
        Assert.AreEqual("topic\r\nfirst\r\nsecond\r\nafter", numbered.GetPlainText());

        var continued = numbered.SplitParagraph(2, numbered.GetParagraphRange(2).Length);
        Assert.AreEqual(NoteBlockKind.Numbered, continued.Paragraphs[3].Kind);
        Assert.AreEqual(string.Empty, continued.Paragraphs[3].Runs.SingleOrDefault()?.Text ?? string.Empty);
        var ended = continued.EndEmptyListItem(3);
        Assert.AreEqual(NoteBlockKind.Paragraph, ended.Paragraphs[3].Kind);
        Assert.AreEqual("topic\r\nfirst\r\nsecond\r\n\r\nafter", ended.GetPlainText());
    }

    [TestMethod]
    public void NotesDocument_MapsParagraphBoundaryOffsetsToTheCorrectItem()
    {
        var document = NotesDocument.FromText("one\r\ntwo");

        Assert.AreEqual((0, 3), document.GetParagraphPositionAt(3));
        Assert.AreEqual((0, 3), document.GetParagraphPositionAt(4));
        Assert.AreEqual((1, 0), document.GetParagraphPositionAt(5));
        Assert.AreEqual((1, 3), document.GetParagraphPositionAt(8));

        var listed = document.ToggleListKind(5, 0, NoteBlockKind.Checklist);
        Assert.AreEqual(NoteBlockKind.Paragraph, listed.Paragraphs[0].Kind);
        Assert.AreEqual(NoteBlockKind.Checklist, listed.Paragraphs[1].Kind);
    }

    [TestMethod]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void NotesDocument_FormatsTextAfterSoftLineBreaksUsingPlainTextOffsets(string lineBreak)
    {
        var document = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("Head" + lineBreak + "Tail")] }]
        };
        var formatted = document.SetStyle(6, 4, NoteTextStyle.Bold, true);

        Assert.AreEqual("Head\r\nTail", formatted.GetPlainText());
        Assert.AreEqual("Tail", formatted.Paragraphs[0].Runs.Last().Text);
        Assert.AreEqual(NoteTextStyle.Bold, formatted.Paragraphs[0].Runs.Last().Style);
        Assert.AreEqual(NoteTextStyle.None, formatted.Paragraphs[0].Runs.First().Style);
    }

    [TestMethod]
    public void JsonNotesStore_RoundTripsListBoundariesAndListChangesUndoAndRedo()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var noteId = app.CreateNote();
        var plain = NotesDocument.FromText("plain\r\none\r\ntwo");
        var firstStart = plain.GetPlainText().IndexOf("one", StringComparison.Ordinal);
        app.UpdateDocument(noteId, plain);
        app.UpdateDocument(noteId, plain.SetListKind(firstStart, "one\r\ntwo".Length, NoteBlockKind.Numbered));

        Assert.IsTrue(app.Undo(noteId));
        Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[1].Kind);
        Assert.IsTrue(app.Redo(noteId));

        var restored = NotesApplication.Start(new JsonNotesStore(path)).GetNote(noteId).Document;
        Assert.AreEqual(NoteBlockKind.Numbered, restored.Paragraphs[1].Kind);
        Assert.AreEqual(NoteBlockKind.Numbered, restored.Paragraphs[2].Kind);
        Assert.AreEqual("plain\r\none\r\ntwo", restored.GetPlainText());
    }

    [TestMethod]
    public void NotesSnapshotValidator_RejectsUnsupportedListKinds()
    {
        using var directory = new TemporaryDirectory();
        var snapshot = new NotesSnapshot
        {
            Notes =
            [
                new HoodNote
                {
                    Document = new NotesDocument
                    {
                        Paragraphs = [new NoteParagraph { Kind = (NoteBlockKind)99 }]
                    }
                }
            ],
            FirstActivationCompleted = true
        };

        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.Validate(snapshot));
    }

    [TestMethod]
    public void NotesDocument_ChecklistSplitsFormattedTextAndPreservesNeighborParagraphs()
    {
        var document = NotesDocument.FromText("before\r\nfirst task\r\nafter")
            .SetStyle(14, 4, NoteTextStyle.Bold, true)
            .SetListKind(8, "first task".Length, NoteBlockKind.Checklist)
            .SetChecklistChecked(1, true);

        var split = document.SplitParagraph(1, 7);

        Assert.AreEqual("before\r\nfirst t\r\nask\r\nafter", split.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Checklist, split.Paragraphs[1].Kind);
        Assert.AreEqual(NoteBlockKind.Checklist, split.Paragraphs[2].Kind);
        Assert.IsTrue(split.Paragraphs[1].IsChecked);
        Assert.IsFalse(split.Paragraphs[2].IsChecked);
        Assert.AreEqual(NoteTextStyle.Bold, split.Paragraphs[1].Runs.Single(run => run.Text == "t").Style);
        Assert.AreEqual(NoteTextStyle.Bold, split.Paragraphs[2].Runs.Single(run => run.Text == "ask").Style);
        Assert.AreEqual(NoteBlockKind.Paragraph, split.Paragraphs[3].Kind);
        Assert.AreEqual("after", split.Paragraphs[3].Runs.Single().Text);
    }

    [TestMethod]
    public void NotesDocument_ContinuesChecklistAndEndsAnEmptyItem()
    {
        var checklist = NotesDocument.FromText("first task\r\nneighbor")
            .SetListKind(0, "first task".Length, NoteBlockKind.Checklist)
            .SetChecklistChecked(0, true);

        var continued = checklist.SplitParagraph(0, checklist.GetParagraphRange(0).Length);
        Assert.AreEqual(NoteBlockKind.Checklist, continued.Paragraphs[1].Kind);
        Assert.IsFalse(continued.Paragraphs[1].IsChecked);
        Assert.AreEqual(NoteBlockKind.Paragraph, continued.Paragraphs[2].Kind);

        var ended = continued.EndEmptyListItem(1);
        Assert.AreEqual(NoteBlockKind.Paragraph, ended.Paragraphs[1].Kind);
        Assert.IsFalse(ended.Paragraphs[1].IsChecked);
        Assert.AreEqual("first task\r\n\r\nneighbor", ended.GetPlainText());
    }

    [TestMethod]
    public void JsonNotesStore_RoundTripsChecklistStateWithUndoRedoAndPlainTextSearch()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "notes.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var noteId = app.CreateNote();
        var document = NotesDocument.FromText("first task\r\nsecond task")
            .SetListKind(0, "first task\r\nsecond task".Length, NoteBlockKind.Checklist)
            .SetChecklistChecked(1, true);
        app.UpdateDocument(noteId, document);
        app.UpdateDocument(noteId, document.SetChecklistChecked(1, false));

        Assert.IsTrue(app.Undo(noteId));
        Assert.IsTrue(app.GetNote(noteId).Document.Paragraphs[1].IsChecked);
        Assert.IsTrue(app.Redo(noteId));

        var restored = NotesApplication.Start(new JsonNotesStore(path));
        var restoredDocument = restored.GetNote(noteId).Document;
        Assert.IsFalse(restoredDocument.Paragraphs[1].IsChecked);
        Assert.AreEqual(NoteBlockKind.Checklist, restoredDocument.Paragraphs[0].Kind);
        Assert.AreEqual(NoteBlockKind.Checklist, restoredDocument.Paragraphs[1].Kind);
        Assert.AreEqual("first task\r\nsecond task", restoredDocument.GetPlainText());
        Assert.AreEqual("first task second task", restored.Search("SECOND").Single().Preview);
    }

    [TestMethod]
    public void NotesSnapshotValidator_RejectsCheckedParagraphOutsideChecklist()
    {
        var snapshot = new NotesSnapshot
        {
            Notes =
            [
                new HoodNote
                {
                    Document = new NotesDocument
                    {
                        Paragraphs = [new NoteParagraph { IsChecked = true }]
                    }
                }
            ],
            FirstActivationCompleted = true
        };

        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.Validate(snapshot));
    }

    [TestMethod]
    public void NotesSnapshotValidator_RejectsUnsupportedNoteColors()
    {
        var snapshot = new NotesSnapshot
        {
            Notes = [new HoodNote { Color = (NoteColor)99 }],
            FirstActivationCompleted = true
        };

        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.Validate(snapshot));
    }

    [TestMethod]
    public void NoteColorPalette_UsesReadableDistinctStyleColorsWithTextContrast()
    {
        foreach (var style in UiStyleCatalog.Supported)
        {
            var lightFills = NoteColorPalette.Colors.Select(color => NoteColorPalette.GetBackgroundHex(color, false, style)).ToArray();
            var darkFills = NoteColorPalette.Colors.Select(color => NoteColorPalette.GetBackgroundHex(color, true, style)).ToArray();
            Assert.AreEqual(NoteColorPalette.Colors.Count, lightFills.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                $"{style} light note colors must stay visually distinct.");
            Assert.AreEqual(NoteColorPalette.Colors.Count, darkFills.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                $"{style} dark note colors must stay visually distinct.");

            foreach (var color in NoteColorPalette.Colors)
            {
                foreach (var darkTheme in new[] { false, true })
                {
                    var background = GetLuminance(NoteColorPalette.GetBackgroundHex(color, darkTheme, style));
                    var foreground = GetLuminance(NoteColorPalette.GetForegroundHex(darkTheme));
                    var ratio = (Math.Max(background, foreground) + 0.05) / (Math.Min(background, foreground) + 0.05);
                    Assert.IsTrue(ratio >= 4.5, $"{color} in {style} {(darkTheme ? "dark" : "light")} theme has contrast {ratio:F2}:1.");
                }
            }
        }

        foreach (var color in NoteColorPalette.Colors)
        {
            foreach (var darkTheme in new[] { false, true })
            {
                var styleFills = UiStyleCatalog.Supported
                    .Select(style => NoteColorPalette.GetBackgroundHex(color, darkTheme, style)).ToArray();
                Assert.AreEqual(UiStyleCatalog.Supported.Count, styleFills.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    $"{color} must adapt its fill across styles while keeping its category.");
            }
        }
    }
    [TestMethod]
    public void RecoverNotePlacements_UpdatesOnlyInaccessibleActiveAndTrashedBoundsAndPreservesState()
    {
        using var directory = new TemporaryDirectory();
        var notesPath = Path.Combine(directory.Path, "notes.json");
        var petStatePath = Path.Combine(directory.Path, "state.json");
        var configurationPath = Path.Combine(directory.Path, "config.yaml");
        const string petState = "pet placement remains independent";
        const string configuration = "settings remain independent";
        File.WriteAllText(petStatePath, petState);
        File.WriteAllText(configurationPath, configuration);
        var app = NotesApplication.Start(new JsonNotesStore(notesPath));

        var openId = app.CreateNote();
        var openDocument = NotesDocument.FromText("open note").SetStyle(0, 4, NoteTextStyle.Bold, true);
        app.UpdateDocument(openId, openDocument);
        app.UpdateBounds(openId, new NoteBounds(-1500, 80, 500, 400));
        Assert.IsNull(app.UpdateColor(openId, NoteColor.Blue));

        var closedId = app.CreateNote();
        var closedDocument = NotesDocument.FromText("closed note");
        app.UpdateDocument(closedId, closedDocument);
        app.UpdateBounds(closedId, new NoteBounds(400, 200, 500, 400));
        Assert.IsTrue(app.TryCloseNote(closedId, out var closeError), closeError);

        var trashedId = app.CreateNote();
        var trashedDocument = NotesDocument.FromText("trashed note").SetListKind(0, 12, NoteBlockKind.Checklist);
        app.UpdateDocument(trashedId, trashedDocument);
        app.UpdateBounds(trashedId, new NoteBounds(2200, 200, 500, 400));
        Assert.IsTrue(app.TryMoveToTrash(trashedId, out var trashError), trashError);
        app.SetGroupVisible(false);

        var displays = new[] { new DisplayInfo("Remaining", new PetBounds(0, 0, 1200, 760), true, ScaleFactor: 1.5) };
        Assert.IsTrue(app.RecoverNotePlacements(displays));
        Assert.IsFalse(app.HasUnsavedChanges);

        var restored = NotesApplication.Start(new JsonNotesStore(notesPath));
        Assert.AreEqual(new NoteBounds(0, 80, 500, 400), restored.GetNote(openId).Bounds);
        Assert.AreEqual(NoteColor.Blue, restored.GetNote(openId).Color);
        Assert.AreEqual(NoteTextStyle.Bold, restored.GetNote(openId).Document.Paragraphs[0].Runs[0].Style);
        Assert.AreEqual(new NoteBounds(400, 200, 500, 400), restored.GetNote(closedId).Bounds);
        Assert.AreEqual("closed note", restored.GetNote(closedId).Text);
        Assert.AreEqual(new NoteBounds(700, 200, 500, 400), restored.Trash.Single(note => note.Id == trashedId).Bounds);
        Assert.AreEqual(NoteBlockKind.Checklist, restored.Trash.Single(note => note.Id == trashedId).Document.Paragraphs[0].Kind);
        CollectionAssert.AreEqual(new[] { openId }, restored.OpenNoteIds.ToArray());
        Assert.IsFalse(restored.GroupVisible);
        Assert.IsTrue(restored.FirstActivationCompleted);
        Assert.AreEqual(petState, File.ReadAllText(petStatePath));
        Assert.AreEqual(configuration, File.ReadAllText(configurationPath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hood-notes-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class SwitchableNotesStore : INotesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public NotesSnapshot? Snapshot { get; private set; }
        public bool ShouldFail { get; set; }

        public NotesSnapshot? Load() => Snapshot is null
            ? null
            : JsonSerializer.Deserialize<NotesSnapshot>(JsonSerializer.Serialize(Snapshot), JsonOptions);

        public void Save(NotesSnapshot snapshot)
        {
            if (ShouldFail)
            {
                throw new NotesStoreException("injected-notes.json", "Injected notes save failure.");
            }

            Snapshot = JsonSerializer.Deserialize<NotesSnapshot>(JsonSerializer.Serialize(snapshot), JsonOptions);
        }
    }

    private static double GetLuminance(string argb)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var red = Convert.ToByte(argb.Substring(3, 2), 16);
        var green = Convert.ToByte(argb.Substring(5, 2), 16);
        var blue = Convert.ToByte(argb.Substring(7, 2), 16);
        return 0.2126 * Linearize(red) + 0.7152 * Linearize(green) + 0.0722 * Linearize(blue);
    }
}
