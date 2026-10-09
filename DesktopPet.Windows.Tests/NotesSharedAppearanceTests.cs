using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NotesSharedAppearanceTests
{
    [TestMethod]
    public void ClosedNoteCanBeCollectedAfterDispatcherWorkDrains()
    {
        var closedNote = CreateClosedNoteWeakReference();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(closedNote.TryGetTarget(out _), "Shared appearance resources must not retain a closed editor.");
    }

    [TestMethod]
    public void NotesShareStableThemeDictionariesWithoutSharingMutableAppearance()
    {
        var first = CreateNote(NoteColor.Yellow, "light", UiStyleCatalog.StickyNotes);
        var second = CreateNote(NoteColor.Pink, "light", UiStyleCatalog.Soft);
        NoteWindow? third = null;

        try
        {
            var firstStyles = GetNotesStyles(first);
            var secondStyles = GetNotesStyles(second);
            Assert.AreSame(firstStyles, secondStyles, "Notes with the same effective theme should reuse stable styles and templates.");
            Assert.AreSame(GetNotesStyles(GetHeaderMenu(first)), GetNotesStyles(GetHeaderMenu(second)));
            var firstEditor = (RichTextBox)first.FindName("Editor");
            var secondEditor = (RichTextBox)second.FindName("Editor");
            Assert.AreNotSame(firstEditor.Document, secondEditor.Document);

            var secondCodeBrush = (SolidColorBrush)second.Resources["NotesCodeBrush"];
            var secondBackground = second.Background;
            first.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 19, LineSpacing = 1.5 },
                NoteColor.Blue, UiStyleCatalog.StickyNotes);

            var firstDarkStyles = GetNotesStyles(first);
            Assert.AreNotSame(firstStyles, firstDarkStyles, "A theme change should swap variants rather than mutate the light dictionary.");
            Assert.AreSame(secondStyles, GetNotesStyles(second), "Changing one note must not retheme its sibling.");
            Assert.IsTrue(GetThemeSource(firstDarkStyles).EndsWith("Fluent.Dark.xaml", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(GetThemeSource(secondStyles).EndsWith("Fluent.Light.xaml", StringComparison.OrdinalIgnoreCase));
            Assert.AreSame(firstDarkStyles, GetNotesStyles(GetHeaderMenu(first)));
            Assert.AreSame(GetNotesStyles(second), GetNotesStyles(GetHeaderMenu(second)));
            Assert.AreSame(secondCodeBrush, second.Resources["NotesCodeBrush"]);
            Assert.AreSame(secondBackground, second.Background);
            Assert.AreNotSame(first.Resources["NotesCodeBrush"], second.Resources["NotesCodeBrush"]);
            Assert.AreEqual(16, secondEditor.FontSize);

            third = CreateNote(NoteColor.Lavender, "dark", UiStyleCatalog.Studio);
            Assert.AreSame(firstDarkStyles, GetNotesStyles(third), "Sharing follows effective theme while palettes stay local to each note.");
            Assert.AreNotSame(first.Resources["NotesCodeBrush"], third.Resources["NotesCodeBrush"]);
        }
        finally
        {
            third?.CloseWithoutMembershipChange();
            second.CloseWithoutMembershipChange();
            first.CloseWithoutMembershipChange();
        }
    }

    private static NoteWindow CreateNote(NoteColor color, string theme, string style) => new(
        new HoodNote { Color = color, Document = NotesDocument.FromText("Independent note content") },
        (_, _) => null,
        (_, _) => null,
        (_, _) => null,
        (_, _) => (null, null),
        _ => (true, null),
        () => { },
        () => { },
        () => { },
        (_, _) => null,
        _ => null,
        () => null,
        new NotesConfiguration { Theme = theme },
        style);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<NoteWindow> CreateClosedNoteWeakReference()
    {
        var note = CreateNote(NoteColor.Yellow, "light", UiStyleCatalog.StickyNotes);
        note.Left = -10000;
        note.Top = -10000;
        note.Show();
        note.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var weak = new WeakReference<NoteWindow>(note);
        note.CloseWithoutMembershipChange();
        note.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        return weak;
    }

    private static ContextMenu GetHeaderMenu(NoteWindow note) => ((Button)note.FindName("MenuButton")).ContextMenu!;

    private static ResourceDictionary GetNotesStyles(FrameworkElement surface) => surface.Resources.MergedDictionaries
        .Single(dictionary => dictionary.Source?.OriginalString.EndsWith("NotesStyles.xaml", StringComparison.OrdinalIgnoreCase) == true);

    private static string GetThemeSource(ResourceDictionary styles) => styles.MergedDictionaries[0].Source!.OriginalString;
}
