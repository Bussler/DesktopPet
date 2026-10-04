using System.Text.Json.Serialization;

namespace DesktopPet.Core;

public sealed record NoteBounds(double Left, double Top, double Width, double Height);

public sealed record HoodNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Note" : Name;

    public NotesDocument Document { get; init; } = NotesDocument.FromText(string.Empty);
    public NoteBounds Bounds { get; init; } = new(96, 96, 320, 300);
    public NoteColor Color { get; init; } = NoteColor.Yellow;

    [JsonIgnore]
    public string Text
    {
        get => Document.GetPlainText();
        init => Document = NotesDocument.FromText(value);
    }
}

public sealed class NotesSnapshot
{
    public int SchemaVersion { get; set; } = 3;
    public List<HoodNote> Notes { get; set; } = [];
    public List<HoodNote> Trash { get; set; } = [];
    public List<Guid> OpenNoteIds { get; set; } = [];
    public bool GroupVisible { get; set; }
    public bool FirstActivationCompleted { get; set; }

    internal NotesSnapshot Copy() => new()
    {
        SchemaVersion = SchemaVersion,
        Notes = Notes.Select(note => note with
        {
            Document = note.Document.Copy(),
            Bounds = note.Bounds with { }
        }).ToList(),
        Trash = Trash.Select(note => note with
        {
            Document = note.Document.Copy(),
            Bounds = note.Bounds with { }
        }).ToList(),
        OpenNoteIds = [.. OpenNoteIds],
        GroupVisible = GroupVisible,
        FirstActivationCompleted = FirstActivationCompleted
    };
}

public static class NotesSnapshotValidator
{
    public const int MaximumNotes = 200;
    public const int MaximumTextCharacters = 1_000_000;
    public const double MinimumNoteWidth = 240;
    public const double MinimumNoteHeight = 180;
    public const double MaximumNoteWidth = 1920;
    public const double MaximumNoteHeight = 1080;

    public static void Validate(NotesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 3)
        {
            throw new InvalidDataException("The notes data must use schema version 3.");
        }

        if (snapshot.Notes is null || snapshot.Trash is null || snapshot.OpenNoteIds is null || snapshot.Notes.Count + snapshot.Trash.Count > MaximumNotes)
        {
            throw new InvalidDataException($"Active notes and Trash must contain no more than {MaximumNotes} notes and valid open-note membership.");
        }

        var noteIds = new HashSet<Guid>();
        foreach (var note in snapshot.Notes.Concat(snapshot.Trash))
        {
            if (note is null || note.Id == Guid.Empty || !noteIds.Add(note.Id))
            {
                throw new InvalidDataException("Each note must have a unique, non-empty identity.");
            }

            if (note.Document is null)
            {
                throw new InvalidDataException("Each note must contain a valid document.");
            }

            if (!Enum.IsDefined(note.Color))
            {
                throw new InvalidDataException("Each note must use a supported color.");
            }

            ValidateDocument(note.Document);
            ValidateName(note.Name);

            ValidateBounds(note.Bounds);
        }

        var activeIds = snapshot.Notes.Select(note => note.Id).ToHashSet();
        var openIds = new HashSet<Guid>();
        foreach (var id in snapshot.OpenNoteIds)
        {
            if (id == Guid.Empty || !openIds.Add(id) || !activeIds.Contains(id))
            {
                throw new InvalidDataException("Open-note membership must refer to unique active note identities.");
            }
        }

        if (snapshot.GroupVisible && openIds.Count == 0)
        {
            throw new InvalidDataException("The notes group cannot be visible when no note windows are open.");
        }

        if (noteIds.Count > 0 && !snapshot.FirstActivationCompleted)
        {
            throw new InvalidDataException("The first-activation marker must be set when notes already exist.");
        }
    }

    public static void ValidateBounds(NoteBounds? bounds)
    {
        if (bounds is null ||
            !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top) ||
            !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
            Math.Abs(bounds.Left) > 100_000 || Math.Abs(bounds.Top) > 100_000 ||
            bounds.Width < MinimumNoteWidth || bounds.Width > MaximumNoteWidth ||
            bounds.Height < MinimumNoteHeight || bounds.Height > MaximumNoteHeight)
        {
            throw new InvalidDataException("Note bounds must use finite logical coordinates and a size between 240×180 and 1920×1080.");
        }
    }

    public static void ValidateName(string name)
    {
        if (name is null || name.Length > MaximumTextCharacters || name.Contains('\r') || name.Contains('\n'))
        {
            throw new InvalidDataException("Note names must contain a single line of text within the note text limit.");
        }
    }

    public static void ValidateDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Paragraphs is null || document.Paragraphs.Count == 0 || document.Paragraphs.Count > MaximumTextCharacters)
        {
            throw new InvalidDataException("Each note must contain a valid document with at least one paragraph.");
        }

        foreach (var paragraph in document.Paragraphs)
        {
                if (paragraph is null || !Enum.IsDefined(paragraph.Kind) || paragraph.Runs is null ||
                    (paragraph.IsChecked && paragraph.Kind != NoteBlockKind.Checklist))
            {
                throw new InvalidDataException("Each note paragraph must use a supported block kind and contain valid text runs.");
            }

            foreach (var run in paragraph.Runs)
            {
                if (run is null || run.Text is null || ((int)run.Style & ~15) != 0)
                {
                    throw new InvalidDataException("Each text run must contain readable text and supported inline styles.");
                }

            }
        }

        if (document.GetPlainText().Length > MaximumTextCharacters)
        {
            throw new InvalidDataException($"Each note must contain no more than {MaximumTextCharacters:N0} characters.");
        }
    }
}

public enum NotesActivationKind
{
    CreatedFirstNote,
    Shown,
    Hidden,
    ShowNotesList
}

public sealed record NotesActivationResult(NotesActivationKind Kind, Guid? NoteId, bool GroupVisible);
public sealed record NoteListItem(Guid Id, string Preview, bool IsOpen, string Name = "Note")
{
    public string StateLabel => IsOpen ? "Open" : "Closed";
}
public sealed record TrashedNoteListItem(Guid Id, string Preview, string Name = "Note");

public interface INotesStore
{
    NotesSnapshot? Load();
    void Save(NotesSnapshot snapshot);
}

public sealed class NotesStoreException : Exception
{
    public NotesStoreException(string storePath, string message, Exception? innerException = null)
        : base(message, innerException) => StorePath = storePath;

    public string StorePath { get; }
}

public sealed class NotesApplication
{
    private readonly INotesStore _store;
    private NotesSnapshot _state;
    private bool _isDirty;
    private readonly Dictionary<Guid, DocumentHistory> _history = [];
    private NoteColor _defaultColor;

    private NotesApplication(INotesStore store, NotesSnapshot state, NoteColor defaultColor)
    {
        _store = store;
        _state = state.Copy();
        _defaultColor = defaultColor;
    }

    public IReadOnlyList<HoodNote> Notes => _state.Notes.ToArray();
    public IReadOnlyList<HoodNote> Trash => _state.Trash.ToArray();
    public IReadOnlyList<Guid> OpenNoteIds => _state.OpenNoteIds.ToArray();
    public bool GroupVisible => _state.GroupVisible;
    public bool FirstActivationCompleted => _state.FirstActivationCompleted;
    public bool HasUnsavedChanges => _isDirty;
    public string? LastSaveError { get; private set; }

    public HoodNote GetNote(Guid noteId) => _state.Notes.FirstOrDefault(note => note.Id == noteId)
        ?? throw new ArgumentException("The requested note does not exist.", nameof(noteId));

    public bool CanUndo(Guid noteId) => _history.TryGetValue(noteId, out var history) && history.Undo.Count > 0;
    public bool CanRedo(Guid noteId) => _history.TryGetValue(noteId, out var history) && history.Redo.Count > 0;

    public static NotesApplication Start(INotesStore store, NoteColor defaultColor = NoteColor.Yellow)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!Enum.IsDefined(defaultColor))
        {
            throw new ArgumentOutOfRangeException(nameof(defaultColor));
        }

        var state = store.Load() ?? new NotesSnapshot();
        NotesSnapshotValidator.Validate(state);
        return new NotesApplication(store, state, defaultColor);
    }

    public NotesActivationResult Activate()
    {
        if (!_state.FirstActivationCompleted)
        {
            var id = CreateNote();
            return new NotesActivationResult(NotesActivationKind.CreatedFirstNote, id, GroupVisible);
        }

        if (_state.GroupVisible)
        {
            SetGroupVisible(false);
            return new NotesActivationResult(NotesActivationKind.Hidden, null, GroupVisible);
        }

        if (_state.OpenNoteIds.Count > 0)
        {
            SetGroupVisible(true);
            return new NotesActivationResult(NotesActivationKind.Shown, null, GroupVisible);
        }

        return new NotesActivationResult(NotesActivationKind.ShowNotesList, null, GroupVisible);
    }

    public Guid CreateNote()
    {
        if (_state.Notes.Count + _state.Trash.Count >= NotesSnapshotValidator.MaximumNotes)
        {
            throw new InvalidDataException($"You can keep up to {NotesSnapshotValidator.MaximumNotes} notes, including Trash. Permanently delete unwanted notes from the Notes list's Trash tab before creating another note.");
        }

        var note = new HoodNote { Color = _defaultColor };
        var next = _state.Copy();
        next.Notes.Add(note);
        next.OpenNoteIds.Add(note.Id);
        next.GroupVisible = true;
        next.FirstActivationCompleted = true;
        SaveAfterUpdate(next);
        return note.Id;
    }

    public void SetDefaultColor(NoteColor color)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color));
        }

        _defaultColor = color;
    }

    public string? UpdateColor(Guid noteId, NoteColor color)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color));
        }

        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        if (next.Notes[index].Color == color)
        {
            return LastSaveError;
        }

        next.Notes[index] = next.Notes[index] with { Color = color };
        SaveAfterUpdate(next);
        return LastSaveError;
    }

    public void OpenNote(Guid noteId)
    {
        var next = _state.Copy();
        if (next.Notes.All(note => note.Id != noteId))
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        if (!next.OpenNoteIds.Contains(noteId))
        {
            next.OpenNoteIds.Add(noteId);
        }

        next.FirstActivationCompleted = true;
        next.GroupVisible = true;
        SaveAfterUpdate(next);
    }

    public void UpdateName(Guid noteId, string name)
    {
        NotesSnapshotValidator.ValidateName(name);
        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        if (next.Notes[index].Name == name)
        {
            return;
        }

        next.Notes[index] = next.Notes[index] with { Name = name };
        SaveAfterUpdate(next);
    }

    public void UpdateDocument(Guid noteId, NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        NotesSnapshotValidator.ValidateDocument(document);
        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        var previous = next.Notes[index].Document;
        if (DocumentsEqual(previous, document))
        {
            return;
        }

        var history = GetHistory(noteId);
        PushHistory(history.Undo, previous);
        history.Redo.Clear();
        next.Notes[index] = next.Notes[index] with { Document = document.Copy() };
        SaveAfterUpdate(next);
    }

    public bool Undo(Guid noteId) => MoveHistory(noteId, undo: true);

    public bool Redo(Guid noteId) => MoveHistory(noteId, undo: false);

    public void UpdateBounds(Guid noteId, NoteBounds bounds)
    {
        NotesSnapshotValidator.ValidateBounds(bounds);
        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        next.Notes[index] = next.Notes[index] with { Bounds = bounds };
        SaveAfterUpdate(next);
    }

    public bool RecoverNotePlacements(IReadOnlyCollection<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var next = _state.Copy();
        var changed = false;
        next.Notes = RecoverPlacements(next.Notes, displays, ref changed);
        next.Trash = RecoverPlacements(next.Trash, displays, ref changed);
        if (!changed)
        {
            return false;
        }

        SaveAfterUpdate(next);
        return true;
    }

    public IReadOnlyList<NoteListItem> Search(string? query)
    {
        var openIds = _state.OpenNoteIds.ToHashSet();
        return _state.Notes
            .Where(note => string.IsNullOrWhiteSpace(query) || note.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || note.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(note => new NoteListItem(note.Id, CreatePreview(note.Text), openIds.Contains(note.Id), note.DisplayName))
            .ToArray();
    }

    public IReadOnlyList<TrashedNoteListItem> SearchTrash(string? query) => _state.Trash
        .Where(note => string.IsNullOrWhiteSpace(query) || note.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || note.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
        .Select(note => new TrashedNoteListItem(note.Id, CreatePreview(note.Text), note.DisplayName))
        .ToArray();

    public bool TryMoveToTrash(Guid noteId, out string? error)
    {
        if (_state.Notes.All(note => note.Id != noteId))
        {
            error = "The selected active note no longer exists.";
            return false;
        }

        if (!TrySaveCurrent())
        {
            error = LastSaveError;
            return false;
        }

        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        next.Trash.Add(next.Notes[index]);
        next.Notes.RemoveAt(index);
        next.OpenNoteIds.Remove(noteId);
        if (next.OpenNoteIds.Count == 0)
        {
            next.GroupVisible = false;
        }

        if (!TrySave(next))
        {
            error = LastSaveError;
            return false;
        }

        _state = next;
        _isDirty = false;
        error = null;
        return true;
    }

    public bool TryRestoreFromTrash(Guid noteId, out string? error)
    {
        var index = _state.Trash.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            error = "The selected trashed note no longer exists.";
            return false;
        }

        if (!TrySaveCurrent())
        {
            error = LastSaveError;
            return false;
        }

        var next = _state.Copy();
        var noteIndex = next.Trash.FindIndex(note => note.Id == noteId);
        next.Notes.Add(next.Trash[noteIndex]);
        next.Trash.RemoveAt(noteIndex);
        if (next.Notes.Count > 0)
        {
            next.FirstActivationCompleted = true;
        }

        if (!TrySave(next))
        {
            error = LastSaveError;
            return false;
        }

        _state = next;
        _isDirty = false;
        error = null;
        return true;
    }

    public bool TryPermanentlyDeleteFromTrash(Guid noteId, bool confirmed, out string? error)
    {
        if (!confirmed)
        {
            error = null;
            return false;
        }

        if (_state.Trash.All(note => note.Id != noteId))
        {
            error = "The selected trashed note no longer exists.";
            return false;
        }

        if (!TrySaveCurrent())
        {
            error = LastSaveError;
            return false;
        }

        var next = _state.Copy();
        next.Trash.RemoveAll(note => note.Id == noteId);
        if (!TrySave(next))
        {
            error = LastSaveError;
            return false;
        }

        _state = next;
        _history.Remove(noteId);
        _isDirty = false;
        error = null;
        return true;
    }

    public void SetGroupVisible(bool visible)
    {
        if (visible && _state.OpenNoteIds.Count == 0)
        {
            return;
        }

        var next = _state.Copy();
        next.GroupVisible = visible;
        SaveAfterUpdate(next);
    }

    public bool TryCloseNote(Guid noteId, out string? error)
    {
        if (_state.Notes.All(note => note.Id != noteId) || !_state.OpenNoteIds.Contains(noteId))
        {
            error = null;
            return true;
        }

        if (!TrySaveCurrent())
        {
            error = LastSaveError;
            return false;
        }

        var next = _state.Copy();
        next.OpenNoteIds.Remove(noteId);
        if (next.OpenNoteIds.Count == 0)
        {
            next.GroupVisible = false;
        }

        if (!TrySave(next))
        {
            error = LastSaveError;
            return false;
        }

        _state = next;
        _isDirty = false;
        error = null;
        return true;
    }

    public bool Flush() => TrySaveCurrent();

    private void SaveAfterUpdate(NotesSnapshot next)
    {
        NotesSnapshotValidator.Validate(next);
        _state = next;
        _isDirty = true;
        TrySaveCurrent();
    }

    private static List<HoodNote> RecoverPlacements(
        IEnumerable<HoodNote> notes,
        IReadOnlyCollection<DisplayInfo> displays,
        ref bool changed)
    {
        var recovered = new List<HoodNote>();
        foreach (var note in notes)
        {
            var bounds = NotePlacementPolicy.Restore(note.Bounds, displays);
            if (bounds == note.Bounds)
            {
                recovered.Add(note);
                continue;
            }

            changed = true;
            recovered.Add(note with { Bounds = bounds });
        }

        return recovered;
    }

    private bool TrySaveCurrent()
    {
        if (!TrySave(_state.Copy()))
        {
            return false;
        }

        _isDirty = false;
        return true;
    }

    private bool TrySave(NotesSnapshot snapshot)
    {
        try
        {
            NotesSnapshotValidator.Validate(snapshot);
            _store.Save(snapshot.Copy());
            LastSaveError = null;
            return true;
        }
        catch (Exception exception) when (exception is NotesStoreException or IOException or UnauthorizedAccessException)
        {
            LastSaveError = exception.Message;
            return false;
        }
    }

    private bool MoveHistory(Guid noteId, bool undo)
    {
        var history = GetHistory(noteId);
        var source = undo ? history.Undo : history.Redo;
        var target = undo ? history.Redo : history.Undo;
        if (source.Count == 0)
        {
            return false;
        }

        var next = _state.Copy();
        var index = next.Notes.FindIndex(note => note.Id == noteId);
        if (index < 0)
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        PushHistory(target, next.Notes[index].Document);
        var document = source[^1];
        source.RemoveAt(source.Count - 1);
        next.Notes[index] = next.Notes[index] with { Document = document.Copy() };
        SaveAfterUpdate(next);
        return true;
    }

    private DocumentHistory GetHistory(Guid noteId)
    {
        if (_state.Notes.All(note => note.Id != noteId))
        {
            throw new ArgumentException("The requested note does not exist.", nameof(noteId));
        }

        if (!_history.TryGetValue(noteId, out var history))
        {
            history = new DocumentHistory();
            _history.Add(noteId, history);
        }

        return history;
    }

    private static void PushHistory(List<NotesDocument> history, NotesDocument document)
    {
        history.Add(document.Copy());
        while (history.Count > 100 || history.Sum(item => item.GetPlainText().Length) > 2_000_000)
        {
            history.RemoveAt(0);
        }
    }

    private static bool DocumentsEqual(NotesDocument left, NotesDocument right)
    {
        if (left.Paragraphs.Count != right.Paragraphs.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Paragraphs.Count; index++)
        {
            var leftParagraph = left.Paragraphs[index];
            var rightParagraph = right.Paragraphs[index];
            if (leftParagraph.Kind != rightParagraph.Kind ||
                leftParagraph.IsChecked != rightParagraph.IsChecked ||
                !leftParagraph.Runs.SequenceEqual(rightParagraph.Runs))
            {
                return false;
            }
        }

        return true;
    }

    private static string CreatePreview(string text)
    {
        var preview = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (preview.Length == 0)
        {
            return "Empty note";
        }

        const int previewLength = 140;
        return preview.Length <= previewLength ? preview : $"{preview[..(previewLength - 1)]}…";
    }

    private sealed class DocumentHistory
    {
        public List<NotesDocument> Undo { get; } = [];
        public List<NotesDocument> Redo { get; } = [];
    }
}
