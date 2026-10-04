using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NotesListInteractionTests
{
    [TestMethod]
    [DataRow("light", 540)]
    [DataRow("light", 380)]
    [DataRow("dark", 540)]
    [DataRow("dark", 380)]
    [DataRow("system", 540)]
    [DataRow("system", 380)]
    public void InitialListActionsHaveClickableHeightAndFitTheWindow(string theme, int height)
    {
        var window = new NotesListWindow(_ => [], _ => [], _ => { }, () => { }, _ => null, _ => null, () => null, theme);
        try
        {
            window.Width = window.MinWidth;
            window.Height = height;
            ShowHidden(window);
            var content = (FrameworkElement)window.Content;
            foreach (var name in new[] { "OpenButton", "TrashButton", "NewNoteButton" })
            {
                var button = (Button)window.FindName(name);
                Assert.IsTrue(button.ActualHeight >= 24, $"{name} must have a clickable height on first opening.");
                var position = button.TranslatePoint(new Point(), content);
                Assert.IsTrue(position.X >= 0 && position.X + button.ActualWidth <= content.ActualWidth + 1);
                Assert.IsTrue(position.Y >= 0 && position.Y + button.ActualHeight <= content.ActualHeight + 1);
            }
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public void BothListsSupportMultipleSelectionAndKeepItAcrossRefresh()
    {
        var notes = Enumerable.Range(0, 3).Select(_ => new NoteListItem(Guid.NewGuid(), "Task", true, "Task")).ToArray();
        var trash = Enumerable.Range(0, 3).Select(_ => new TrashedNoteListItem(Guid.NewGuid(), "Task", "Task")).ToArray();
        var opens = 0;
        var restores = 0;
        var window = new NotesListWindow(_ => notes.ToArray(), _ => trash.ToArray(), _ => opens++, () => { },
            _ => { restores++; return null; }, _ => null, () => null,
            moveNoteToTrash: _ => null);
        try
        {
            ShowHidden(window);
            var results = (ListBox)window.FindName("ResultsList");
            var trashResults = (ListBox)window.FindName("TrashResultsList");
            var views = (TabControl)window.FindName("Views");
            var lists = new[] { results, trashResults };
            for (var index = 0; index < lists.Length; index++)
            {
                views.SelectedIndex = index;
                var list = lists[index];
                Assert.AreEqual(SelectionMode.Extended, list.SelectionMode);
                list.SelectAll();
                Assert.AreEqual(3, list.SelectedItems.Count, "Selecting all must include every result.");
                list.UnselectAll();
                list.SelectedItems.Add(list.Items[0]);
                list.SelectedItems.Add(list.Items[2]);
            }
            notes[0] = notes[0] with { Name = "Renamed" };
            views.SelectedIndex = 0;
            window.Refresh();
            CollectionAssert.AreEquivalent(new[] { notes[0].Id, notes[2].Id },
                results.SelectedItems.Cast<NoteListItem>().Select(item => item.Id).ToArray());
            CollectionAssert.AreEquivalent(new[] { trash[0].Id, trash[2].Id },
                trashResults.SelectedItems.Cast<TrashedNoteListItem>().Select(item => item.Id).ToArray());
            Assert.IsTrue(((Button)window.FindName("TrashButton")).IsEnabled);
            Assert.IsFalse(((Button)window.FindName("OpenButton")).IsEnabled);
            ((Button)window.FindName("OpenButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, opens, "Open must not silently choose one note from a multiple selection.");
            views.SelectedIndex = 1;
            Assert.IsTrue(((Button)window.FindName("PermanentlyDeleteButton")).IsEnabled);
            Assert.IsFalse(((Button)window.FindName("RestoreButton")).IsEnabled);
            ((Button)window.FindName("RestoreButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, restores);
            trashResults.UnselectAll();
            Assert.IsFalse(((Button)window.FindName("PermanentlyDeleteButton")).IsEnabled);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BulkTrashUsesTheOriginalSelectionDespiteCallbackRefreshes(bool failSave)
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodBulkNotesTest-{Guid.NewGuid():N}.json");
        NotesListWindow? window = null;
        try
        {
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var ids = Enumerable.Range(0, 4).Select(index =>
            {
                var id = app.CreateNote();
                app.UpdateName(id, index < 3 ? $"Target {index}" : "Other");
                return id;
            }).ToArray();
            app.TryCloseNote(ids[2], out _);
            var calls = new List<Guid>();
            window = new NotesListWindow(app.Search, app.SearchTrash, _ => { }, () => { }, _ => null, _ => null,
                () => { app.Flush(); return app.LastSaveError; }, moveNoteToTrash: id =>
                {
                    calls.Add(id);
                    app.TryMoveToTrash(id, out var error);
                    window!.Refresh();
                    return error;
                });
            ShowHidden(window);
            ((TextBox)window.FindName("SearchBox")).Text = "Target";
            var results = (ListBox)window.FindName("ResultsList");
            results.SelectedItems.Add(results.Items[0]);
            results.SelectedItems.Add(results.Items[2]);
            var saved = File.ReadAllText(path);
            using (var lockedFile = failSave ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null)
                ((Button)window.FindName("TrashButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            Assert.AreEqual("Target", ((TextBox)window.FindName("SearchBox")).Text);
            if (failSave)
            {
                Assert.AreEqual(saved, File.ReadAllText(path));
                Assert.AreEqual(0, restarted.Trash.Count);
                Assert.AreEqual(1, calls.Count, "Stop the bulk action at the first failure.");
                Assert.AreEqual(2, results.SelectedItems.Count, "Failed notes must stay selected for retry.");
                Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("ManagerStatus")).Visibility);
            }
            else
            {
                CollectionAssert.AreEquivalent(new[] { ids[0], ids[2] }, restarted.Trash.Select(note => note.Id).ToArray());
                CollectionAssert.AreEquivalent(new[] { ids[1], ids[3] }, restarted.Notes.Select(note => note.Id).ToArray());
                Assert.IsFalse(restarted.OpenNoteIds.Contains(ids[0]));
                Assert.AreEqual(2, calls.Count);
                Assert.AreEqual(1, results.Items.Count);
                Assert.AreEqual(0, results.SelectedItems.Count);
            }
        }
        finally { window?.Close(); File.Delete(path); }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void BulkPermanentDeleteConfirmsOnceAndOnlyDeletesSelectedNotes(bool confirmed, bool failSave)
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodBulkDeleteTest-{Guid.NewGuid():N}.json");
        NotesListWindow? window = null;
        try
        {
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var ids = Enumerable.Range(0, 4).Select(index =>
            {
                var id = app.CreateNote();
                app.UpdateName(id, index < 3 ? $"Target {index}" : "Other");
                app.TryMoveToTrash(id, out _);
                return id;
            }).ToArray();
            var confirmations = 0;
            var calls = new List<Guid>();
            window = new NotesListWindow(app.Search, app.SearchTrash, _ => { }, () => { }, _ => null, id =>
                {
                    calls.Add(id);
                    app.TryPermanentlyDeleteFromTrash(id, confirmed: true, out var error);
                    window!.Refresh();
                    return error;
                }, () => null, confirmPermanentDelete: count =>
                {
                    confirmations++;
                    Assert.AreEqual(2, count, "Confirmation must include the complete selection.");
                    Assert.AreEqual(0, calls.Count, "Nothing may be deleted before confirmation.");
                    window!.Refresh();
                    return confirmed;
                });
            ShowHidden(window);
            ((TabControl)window.FindName("Views")).SelectedIndex = 1;
            ((TextBox)window.FindName("SearchBox")).Text = "Target";
            var results = (ListBox)window.FindName("TrashResultsList");
            results.SelectedItems.Add(results.Items[0]);
            results.SelectedItems.Add(results.Items[2]);
            var saved = File.ReadAllText(path);
            using (var lockedFile = failSave ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null)
                ((Button)window.FindName("PermanentlyDeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(1, confirmations);
            Assert.AreEqual("Target", ((TextBox)window.FindName("SearchBox")).Text);
            Assert.AreEqual(1, ((TabControl)window.FindName("Views")).SelectedIndex);
            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            if (!confirmed || failSave)
            {
                Assert.AreEqual(saved, File.ReadAllText(path));
                CollectionAssert.AreEquivalent(ids, restarted.Trash.Select(note => note.Id).ToArray());
                Assert.AreEqual(2, results.SelectedItems.Count);
                Assert.AreEqual(failSave ? 1 : 0, calls.Count);
                Assert.AreEqual(failSave ? Visibility.Visible : Visibility.Collapsed,
                    ((TextBlock)window.FindName("ManagerStatus")).Visibility);
            }
            else
            {
                CollectionAssert.AreEquivalent(new[] { ids[0], ids[2] }, calls);
                CollectionAssert.AreEquivalent(new[] { ids[1], ids[3] }, restarted.Trash.Select(note => note.Id).ToArray());
                Assert.AreEqual(1, results.Items.Count);
                Assert.AreEqual(0, results.SelectedItems.Count);
            }
        }
        finally { window?.Close(); File.Delete(path); }
    }

    [TestMethod]
    public void PartialBulkDeleteReportsProgressAndLeavesFailedAndRemainingNotesSelected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodPartialDeleteTest-{Guid.NewGuid():N}.json");
        NotesListWindow? window = null;
        try
        {
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var ids = Enumerable.Range(0, 4).Select(index =>
            {
                var id = app.CreateNote();
                app.TryMoveToTrash(id, out _);
                return id;
            }).ToArray();
            var calls = 0;
            window = new NotesListWindow(app.Search, app.SearchTrash, _ => { }, () => { }, _ => null, id =>
                {
                    calls++;
                    using var lockedFile = calls == 2 ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
                    app.TryPermanentlyDeleteFromTrash(id, confirmed: true, out var error);
                    window!.Refresh();
                    return error;
                }, () => null, confirmPermanentDelete: _ => true);
            ShowHidden(window);
            ((TabControl)window.FindName("Views")).SelectedIndex = 1;
            var results = (ListBox)window.FindName("TrashResultsList");
            foreach (var item in results.Items.Cast<object>().Take(3)) results.SelectedItems.Add(item);
            ((Button)window.FindName("PermanentlyDeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(2, calls);
            StringAssert.StartsWith(((TextBlock)window.FindName("ManagerStatus")).Text, "1 of 3 notes processed.");
            CollectionAssert.AreEquivalent(new[] { ids[1], ids[2], ids[3] },
                NotesApplication.Start(new JsonNotesStore(path)).Trash.Select(note => note.Id).ToArray());
            CollectionAssert.AreEquivalent(new[] { ids[1], ids[2] },
                results.SelectedItems.Cast<TrashedNoteListItem>().Select(item => item.Id).ToArray());
        }
        finally { window?.Close(); File.Delete(path); }
    }

    [TestMethod]
    public void EmptySelectionDoesNotConfirmOrModifyNotes()
    {
        var calls = 0;
        var window = new NotesListWindow(_ => [], _ => [], _ => { }, () => { }, _ => null,
            _ => { calls++; return null; }, () => null, moveNoteToTrash: _ => { calls++; return null; },
            confirmPermanentDelete: _ => { calls++; return true; });
        try
        {
            ((Button)window.FindName("TrashButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)window.FindName("PermanentlyDeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, calls);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow("dark")]
    [DataRow("light")]
    public void SearchFieldHasVisibleBackgroundAndHighContrastTextAndCaret(string theme)
    {
        var window = new NotesListWindow(_ => [], _ => [], _ => { }, () => { }, _ => null, _ => null, () => null, theme);
        try
        {
            var search = (TextBox)window.FindName("SearchBox");
            Assert.IsInstanceOfType<SolidColorBrush>(search.Background);
            var background = ((SolidColorBrush)search.Background).Color;
            Assert.AreEqual((byte)255, background.A, "The search field must have an opaque fill.");
            Assert.IsTrue(search.BorderThickness.Left >= 1);
            Assert.IsInstanceOfType<SolidColorBrush>(search.CaretBrush);
            Assert.IsInstanceOfType<SolidColorBrush>(search.Foreground);
            foreach (var brush in new[] { search.CaretBrush, search.Foreground })
                Assert.IsTrue(Math.Abs(Brightness(((SolidColorBrush)brush).Color) - Brightness(background)) > 100,
                    "Search text and caret must stand out against the field.");
            window.Opacity = 0;
            window.ShowInTaskbar = false;
            window.Left = window.Top = -10000;
            window.Show();
            window.Activate();
            Assert.IsTrue(search.Focus());
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
            Assert.IsTrue(Math.Abs(Brightness(((SolidColorBrush)host.Foreground).Color) - Brightness(background)) > 100,
                "Focusing the search field must preserve readable text.");
        }
        finally { window.Close(); }

        static double Brightness(Color color) => 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
    }

    [TestMethod]
    public void ActiveNoteSelectionOffersTrashAction()
    {
        var window = new NotesListWindow(_ => [new(Guid.NewGuid(), "A task", true, "Task")], _ => [],
            _ => { }, () => { }, _ => null, _ => null, () => null);
        try
        {
            var trash = window.FindName("TrashButton") as Button;
            Assert.IsNotNull(trash, "Active notes need an action to move the selection to Trash.");
            Assert.IsFalse(trash.IsEnabled);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ListTrashActionPersistsOrReportsSaveFailure(bool failSave)
    {
        var path = Path.Combine(Path.GetTempPath(), $"HoodNotesListTest-{Guid.NewGuid():N}.json");
        NotesListWindow? window = null;
        try
        {
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var id = app.CreateNote();
            app.UpdateName(id, "Target");
            app.UpdateDocument(id, NotesDocument.FromText("Task").ToggleListKind(0, 4, NoteBlockKind.Checklist).SetChecklistChecked(0, true));
            app.CreateNote();
            window = new NotesListWindow(app.Search, app.SearchTrash, _ => { }, () => { },
                noteId => { app.TryRestoreFromTrash(noteId, out var error); return error; }, _ => null,
                () => { app.Flush(); return app.LastSaveError; },
                moveNoteToTrash: noteId => { app.TryMoveToTrash(noteId, out var error); return error; });
            window.Opacity = 0;
            window.ShowInTaskbar = false;
            window.Left = window.Top = -10000;
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var search = (TextBox)window.FindName("SearchBox");
            var results = (ListBox)window.FindName("ResultsList");
            search.Text = "Target";
            results.SelectedIndex = 0;
            var trash = (Button)window.FindName("TrashButton");
            Assert.IsTrue(trash.IsEnabled);
            var previousFile = File.ReadAllText(path);
            using (var lockedFile = failSave ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null)
                trash.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual("Target", search.Text, "Trashing must preserve the search query.");
            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            if (failSave)
            {
                Assert.AreEqual(previousFile, File.ReadAllText(path));
                Assert.IsTrue(restarted.OpenNoteIds.Contains(id));
                Assert.AreEqual(1, results.Items.Count);
                Assert.AreEqual(Visibility.Visible, ((TextBlock)window.FindName("ManagerStatus")).Visibility);
                Assert.AreEqual(Visibility.Visible, ((Button)window.FindName("RetrySaveButton")).Visibility);
            }
            else
            {
                Assert.AreEqual(1, restarted.Notes.Count, "Unselected notes must remain active.");
                Assert.AreEqual(id, restarted.Trash.Single().Id);
                Assert.IsTrue(restarted.Trash.Single().Document.Paragraphs[0].IsChecked);
                Assert.IsFalse(restarted.OpenNoteIds.Contains(id));
                Assert.AreEqual(0, results.Items.Count);
                var views = (TabControl)window.FindName("Views");
                views.SelectedIndex = 1;
                Assert.IsFalse(trash.IsEnabled);
                ((ListBox)window.FindName("TrashResultsList")).SelectedIndex = 0;
                ((Button)window.FindName("RestoreButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(0, views.SelectedIndex);
                Assert.AreEqual("Task", NotesApplication.Start(new JsonNotesStore(path)).GetNote(id).Text);
                Assert.AreEqual(1, results.Items.Count);
            }
        }
        finally { window?.Close(); File.Delete(path); }
    }
    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.ShowInTaskbar = false;
        window.Left = window.Top = -10000;
        window.Show();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
}
