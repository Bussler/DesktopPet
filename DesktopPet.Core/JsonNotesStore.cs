using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet.Core;

public sealed class JsonNotesStore : INotesStore
{
    public const int MaximumStoreBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private readonly string _path;

    public JsonNotesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public string StorePath => _path;

    public NotesSnapshot? Load()
    {
        string json;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumStoreBytes)
            {
                throw new InvalidDataException($"The saved notes exceed the {MaximumStoreBytes / 1024 / 1024} MiB storage limit.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            json = reader.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (InvalidDataException exception)
        {
            throw ReadFailure(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw ReadFailure(exception);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !(root.TryGetProperty("schemaVersion", out var versionElement) ||
                  root.TryGetProperty("SchemaVersion", out versionElement)) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var version))
            {
                throw new InvalidDataException("Saved notes must be a JSON object with an integer schemaVersion.");
            }

            if (version is not (1 or 2 or 3))
            {
                throw new InvalidDataException($"Saved notes schema version {version} is unsupported; version 3 is required.");
            }

            var snapshot = version switch
            {
                1 => MigrateVersionOne(json),
                2 => MigrateVersionTwo(json),
                3 => JsonSerializer.Deserialize<NotesSnapshot>(json, JsonOptions)
                    ?? throw new InvalidDataException("The saved notes file is empty."),
                _ => throw new InvalidDataException($"Saved notes schema version {version} is unsupported; version 3 is required.")
            };
            NotesSnapshotValidator.Validate(snapshot);
            return snapshot;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw ReadFailure(exception);
        }
    }

    public void Save(NotesSnapshot snapshot)
    {
        try
        {
            NotesSnapshotValidator.Validate(snapshot);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > MaximumStoreBytes)
            {
                throw new InvalidDataException($"The notes exceed the {MaximumStoreBytes / 1024 / 1024} MiB storage limit; no text was truncated.");
            }

            AtomicFile.WriteAllText(_path, json);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            throw new NotesStoreException(
                _path,
                $"Notes changes could not be saved to '{_path}'. Check that the folder is writable and has enough space; the previous notes file was retained. {exception.Message}",
                exception);
        }
    }

    public string? ResetAfterAcknowledgement()
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The notes path must include a directory.");
        var archivedPath = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(_path)}.invalid-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{Path.GetExtension(_path)}");
        try
        {
            File.Move(_path, archivedPath);
            return archivedPath;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new NotesStoreException(
                _path,
                $"The damaged notes could not be archived. Check the local application-data folder permissions and retry. The original file was left in place. {exception.Message}",
                exception);
        }
    }

    private NotesStoreException ReadFailure(Exception exception) => new(
        _path,
        $"The saved notes could not be read safely. Keep this file to inspect it; Hood did not replace it. {exception.Message}",
        exception);

    private static NotesSnapshot MigrateVersionOne(string json)
    {
        var legacy = JsonSerializer.Deserialize<NotesSnapshotV1>(json, JsonOptions)
            ?? throw new InvalidDataException("The version 1 saved notes file is empty.");
        if (legacy.Notes is null || legacy.OpenNoteIds is null || legacy.Notes.Any(note => note is null || note.Text is null || note.Bounds is null))
        {
            throw new InvalidDataException("The version 1 saved notes contain invalid note text or membership.");
        }

        return new NotesSnapshot
        {
            SchemaVersion = 3,
            Notes = legacy.Notes.Select(note => new HoodNote
            {
                Id = note.Id,
                Text = note.Text,
                Bounds = note.Bounds
            }).ToList(),
            OpenNoteIds = [.. legacy.OpenNoteIds],
            GroupVisible = legacy.GroupVisible,
            FirstActivationCompleted = legacy.FirstActivationCompleted
        };
    }

    private static NotesSnapshot MigrateVersionTwo(string json)
    {
        var legacy = JsonSerializer.Deserialize<NotesSnapshot>(json, JsonOptions)
            ?? throw new InvalidDataException("The version 2 saved notes file is empty.");
        legacy.SchemaVersion = 3;
        return legacy;
    }

    private sealed class NotesSnapshotV1
    {
        public int SchemaVersion { get; set; } = 1;
        public List<NoteV1> Notes { get; set; } = [];
        public List<Guid> OpenNoteIds { get; set; } = [];
        public bool GroupVisible { get; set; }
        public bool FirstActivationCompleted { get; set; }
    }

    private sealed class NoteV1
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public NoteBounds Bounds { get; set; } = new(96, 96, 320, 300);
    }
}
