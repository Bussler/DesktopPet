using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DesktopPet.Windows.Tests;

internal static class NoteClipboardTestInput
{
    public static void PastePlainText(RichTextBox editor, string text) =>
        Paste(editor, new DataObject(DataFormats.UnicodeText, text));

    public static void Paste(RichTextBox editor, IDataObject data)
    {
        Assert.IsTrue(editor.Focus(), "The paste target must have input focus.");
        var paste = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
        editor.RaiseEvent(paste);
        if (!paste.CommandCancelled)
        {
            Assert.AreEqual(DataFormats.UnicodeText, paste.FormatToApply);
            var text = paste.DataObject.GetData(paste.FormatToApply) as string;
            Assert.IsNotNull(text);
            editor.Selection.Text = text;
            editor.CaretPosition = editor.Selection.End;
        }
        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
