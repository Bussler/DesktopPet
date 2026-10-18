using System.Runtime.InteropServices;

namespace DesktopPet.Windows;

/// <summary>Temporarily owns only the manual DND change made by this Focus cycle.</summary>
internal sealed class FocusNotifications(Func<string>? read = null, Action<string>? write = null)
{
    internal const string Unrestricted = "Microsoft.QuietHoursProfile.Unrestricted";
    internal const string PriorityOnly = "Microsoft.QuietHoursProfile.PriorityOnly";
    internal const string AlarmsOnly = "Microsoft.QuietHoursProfile.AlarmsOnly";
    private readonly Func<string> _read = read ?? WindowsFocusNotifications.Read;
    private readonly Action<string> _write = write ?? WindowsFocusNotifications.Write;
    private string? _previousProfile;
    private bool _active;
    internal string Error { get; private set; } = "";

    internal bool Update(bool active, bool force = false)
    {
        if (_active == active && !force) return false;
        _active = active;
        try
        {
            Error = "";
            if (active)
            {
                var profile = _read();
                if (profile is not (Unrestricted or PriorityOnly or AlarmsOnly))
                    throw new InvalidOperationException("This Windows notification profile is unsupported.");
                if (profile == Unrestricted)
                {
                    _previousProfile = profile; // Retain ownership even if a write only partially succeeds.
                    _write(PriorityOnly);
                    if (_read() != PriorityOnly)
                        throw new InvalidOperationException("Windows did not enable Do not disturb.");
                }
                else if (profile != PriorityOnly) _previousProfile = null;
            }
            else
            {
                if (_previousProfile is not null)
                {
                    // Preserve a different setting selected by the user or another app.
                    if (_read() == PriorityOnly)
                    {
                        _write(_previousProfile);
                        if (_read() != _previousProfile)
                            throw new InvalidOperationException("Windows did not restore the previous notification setting.");
                    }
                    _previousProfile = null;
                }
            }
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or
            InvalidOperationException or InvalidCastException or ArgumentException or TypeLoadException or NotImplementedException)
        {
            Error = active
                ? $"Could not enable Do not disturb. The timer still works. {exception.Message}"
                : $"Could not restore Do not disturb. Toggle the option to retry, or restore it in Windows. {exception.Message}";
        }
        return true;
    }
}
