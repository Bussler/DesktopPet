using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet.Core;

public sealed class JsonSessionStore : ISessionStore
{
    private const long MaximumFileSizeBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _path;

    public JsonSessionStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public string StatePath => _path;

    public HoodSessionSnapshot? Load()
    {
        string json;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileSizeBytes)
            {
                throw new InvalidDataException("The saved session exceeds the 64 KiB size limit.");
            }

            using var reader = new StreamReader(stream);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SessionStateException(
                _path,
                $"The saved session could not be read: {exception.Message} Keep this file to inspect it, or choose Reset saved session to archive it and start fresh.",
                exception);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("schemaVersion", out var schemaVersionElement) ||
                schemaVersionElement.ValueKind != JsonValueKind.Number ||
                !schemaVersionElement.TryGetInt32(out var schemaVersion))
            {
                throw new InvalidDataException("The saved session must be a JSON object with an integer schemaVersion.");
            }

            var snapshot = schemaVersion switch
            {
                1 => MigrateVersionOne(json),
                2 => JsonSerializer.Deserialize<HoodSessionSnapshot>(json, JsonOptions)
                    ?? throw new InvalidDataException("The saved session is empty."),
                _ => throw new InvalidDataException($"Saved session schema version {schemaVersion} is unsupported; version 2 is required.")
            };
            HoodSessionSnapshotValidator.Validate(snapshot);
            return snapshot;
        }
        catch (SessionStateException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new SessionStateException(
                _path,
                $"The saved session is invalid or could not be read: {exception.Message} Keep this file to inspect it, or choose Reset saved session to archive it and start fresh.",
                exception);
        }
    }

    public void Save(HoodSessionSnapshot snapshot)
    {
        try
        {
            HoodSessionSnapshotValidator.Validate(snapshot);
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new SessionStateException(
                _path,
                $"The session could not be saved. Check that the local application-data folder is writable and has free space; the previous state file was retained. {exception.Message}",
                exception);
        }
    }

    public string? ResetAfterAcknowledgement()
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The session state path must include a directory.");
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
            throw new SessionStateException(
                _path,
                $"The damaged state could not be archived. Check the local application-data folder permissions and retry. {exception.Message}",
                exception);
        }
    }

    private static HoodSessionSnapshot MigrateVersionOne(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacySnapshotV1>(json, JsonOptions)
            ?? throw new InvalidDataException("The version 1 saved session is empty.");
        return new HoodSessionSnapshot
        {
            Position = legacy.Position,
            Action = legacy.Action,
            FrameIndex = legacy.FrameIndex,
            Needs = NeedLevels.Initial,
            Mode = HoodSessionMode.Awake
        };
    }

    private sealed record LegacySnapshotV1(
        int SchemaVersion,
        PetPosition Position,
        PetAction Action,
        int FrameIndex);
}
