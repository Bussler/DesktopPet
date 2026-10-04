namespace DesktopPet.Core;

public sealed record NotesVisibilityTransition(long Revision, bool Visible);

public sealed class NotesVisibilityTransitionCoordinator
{
    private long _revision;

    public NotesVisibilityTransition Request(bool visible)
    {
        _revision = checked(_revision + 1);
        return new NotesVisibilityTransition(_revision, visible);
    }

    public bool IsCurrent(NotesVisibilityTransition transition) => transition.Revision == _revision;
}
