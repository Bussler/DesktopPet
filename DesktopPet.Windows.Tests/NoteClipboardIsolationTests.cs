using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteClipboardIsolationTests
{
    [TestMethod]
    public void PasteAndTableCopyWorkWhileSystemClipboardIsBusy()
    {
        var document = NotesDocument.FromText("");
        IDataObject? copied = null;
        var window = new NoteWindow(new HoodNote { Document = document },
            (_, updated) => { document = updated.Copy(); return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration(), copyToClipboard: data => copied = data);
        var editor = (RichTextBox)window.FindName("Editor");
        // Menu availability is unrelated to the local paste input and otherwise queries OLE.
        CommandManager.AddPreviewCanExecuteHandler(editor, (_, args) =>
        {
            if (args.Command != ApplicationCommands.Paste) return;
            args.CanExecute = true;
            args.Handled = true;
        });
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var opened = false;
        var clipboardThread = new Thread(() =>
        {
            opened = SpinWait.SpinUntil(() => OpenClipboard(IntPtr.Zero), TimeSpan.FromSeconds(5));
            ready.Set();
            if (!opened) return;
            try { release.Wait(); }
            finally { CloseClipboard(); }
        }) { IsBackground = true };
        try
        {
            window.Opacity = 0;
            window.Left = window.Top = -10000;
            window.Show();
            Assert.IsTrue(editor.Focus());
            clipboardThread.Start();
            try
            {
                Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(10)), "The clipboard lock must be ready.");
                Assert.IsTrue(opened, "The regression must hold the system clipboard open.");
                NoteClipboardTestInput.PastePlainText(editor,
                    "**bold**\r\n$$x$$\r\n| Name | Value |\r\n| --- | --- |\r\n| item | $y$ |");
                Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                    .Any(run => run.Text == "bold" && run.Style == NoteTextStyle.Bold));
                Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath));
                Assert.IsTrue(document.Paragraphs.Where(row => row.Cell is not null).SelectMany(row => row.Runs)
                    .Any(run => run.Kind == NoteInlineKind.InlineMath && run.Text == "$y$"));
                NotesSnapshotValidator.ValidateDocument(document);

                var table = editor.Document.Blocks.OfType<Table>().Single();
                var markdown = NoteTableMarkdown.Write(document, (Guid)table.Tag);
                editor.Selection.Select(table.ContentStart, table.ContentEnd);
                ApplicationCommands.Copy.Execute(null, editor);
                Assert.IsNotNull(copied);
                Assert.AreEqual(markdown, copied.GetData(DataFormats.UnicodeText));
                Assert.AreEqual(markdown, copied.GetData(DataFormats.Text));
            }
            finally
            {
                release.Set();
                Assert.IsTrue(clipboardThread.Join(TimeSpan.FromSeconds(5)), "The clipboard lock must be released.");
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
}
