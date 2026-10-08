using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteListNestingTests
{
    [TestMethod]
    [DataRow("emphasis", "**bold**")]
    [DataRow("code", "`code`")]
    [DataRow("math", "$x$")]
    [DataRow("link", "[site](https://example.com)")]
    public void InlineFormattingPreservesListNesting(string formatting, string text)
    {
        var document = NotesDocument.FromText("Parent\r\n" + text);
        foreach (var row in document.Paragraphs) row.Kind = NoteBlockKind.Bulleted;
        document.Paragraphs[1].ListDepth = 1;
        var start = document.GetPlainText().Length;
        var length = 0;
        var input = (document.GetParagraphRange(1).Start, text.Length);
        var updated = formatting switch
        {
            "emphasis" => NoteEmphasisFormatting.Apply(document, input, ref start, ref length),
            "code" => NoteCodeFormatting.Apply(document, ref start, ref length),
            "math" => NoteInlineMathFormatting.Apply(document, input, ref start, ref length),
            _ => NoteLinkFormatting.Apply(document, input, ref start, ref length)
        };
        Assert.AreNotSame(document, updated, "The test must perform a conversion.");
        Assert.IsTrue(updated.Paragraphs[1].Runs.Any(run => run.Style != NoteTextStyle.None || run.Kind != NoteInlineKind.Text));
        Assert.AreEqual(1, updated.Paragraphs[1].ListDepth);
        Assert.AreEqual(1, updated.SetStyle(0, updated.GetPlainText().Length, NoteTextStyle.Italic, true).Paragraphs[1].ListDepth);
        var split = updated.SplitParagraph(1, 1);
        Assert.AreEqual(1, split.Paragraphs[2].ListDepth);
        Assert.AreEqual(1, split.ToggleListKind(split.GetParagraphRange(2).Start, 0, NoteBlockKind.Checklist).Paragraphs[2].ListDepth);
        Assert.AreEqual(0, split.ToggleListKind(split.GetParagraphRange(2).Start, 0, NoteBlockKind.Bulleted).Paragraphs[2].ListDepth);
        NotesSnapshotValidator.ValidateDocument(updated);
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, -1)]
    [DataRow(NoteBlockKind.Bulleted, 33)]
    [DataRow(NoteBlockKind.Numbered, 1000000)]
    [DataRow(NoteBlockKind.Numbered, 1000001)]
    [DataRow(NoteBlockKind.Paragraph, 1)]
    public void InvalidListDepthIsRejected(NoteBlockKind kind, int depth)
    {
        var document = new NotesDocument { Paragraphs = [new() { Kind = kind, ListDepth = depth }] };
        Assert.ThrowsExactly<InvalidDataException>(() => NotesSnapshotValidator.ValidateDocument(document));
    }

    [TestMethod]
    public void VersionSevenNotesLoadWithOuterListsAndUpgradeOnSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodLegacyLists-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonNotesStore(path);
            var document = NotesDocument.FromText("Existing task").SetListKind(0, 13, NoteBlockKind.Checklist);
            store.Save(new NotesSnapshot { FirstActivationCompleted = true, Notes = [new HoodNote { Document = document }] });
            var json = File.ReadAllText(path);
            Assert.IsFalse(json.Contains("listDepth", StringComparison.Ordinal));
            File.WriteAllText(path, json.Replace($"\"schemaVersion\": {NotesSnapshot.CurrentSchemaVersion}", "\"schemaVersion\": 7"));
            var restored = store.Load()!;
            Assert.AreEqual(NotesSnapshot.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.AreEqual(0, restored.Notes.Single().Document.Paragraphs.Single().ListDepth);
            Assert.AreEqual(document.GetPlainText(), restored.Notes.Single().Text);
            store.Save(restored);
            Assert.IsTrue(File.ReadAllText(path).Contains($"\"schemaVersion\": {NotesSnapshot.CurrentSchemaVersion}", StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ExcessiveSavedDepthIsRejectedWithoutChangingTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodUnsafeLists-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonNotesStore(path);
            var document = NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Bulleted);
            store.Save(new NotesSnapshot { FirstActivationCompleted = true, Notes = [new HoodNote { Document = document }] });
            var json = File.ReadAllText(path).Replace("\"kind\": \"bulleted\"", "\"kind\": \"bulleted\", \"listDepth\": 1000000");
            File.WriteAllText(path, json);
            Assert.ThrowsExactly<NotesStoreException>(() => store.Load());
            Assert.AreEqual(json, File.ReadAllText(path), "Recovery must preserve the invalid source file.");
        }
        finally { File.Delete(path); }
    }
}
