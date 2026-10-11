using System.Windows;
using System.Windows.Controls.Primitives;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class RoamingAreaEditorWindow : Window
{
    private readonly HoodSession _session;

    internal RoamingAreaEditorWindow(HoodSession session, HoodOverlayWindow owner)
    {
        _session = session;
        InitializeComponent();
        Owner = owner;
        Topmost = owner.Topmost;
        MinWidth = session.MinimumRoamingArea.Width;
        MinHeight = session.MinimumRoamingArea.Height;
        ApplyPlacement();
        SourceInitialized += (_, _) => ApplyPlacement();
    }

    internal void ApplyPlacement()
    {
        MinWidth = _session.MinimumRoamingArea.Width;
        MinHeight = _session.MinimumRoamingArea.Height;
        WindowsDisplayProvider.ApplyPosition(this, new PetPosition(_session.RoamingArea.X, _session.RoamingArea.Y));
        Width = _session.RoamingArea.Width;
        Height = _session.RoamingArea.Height;
    }

    private void OnMoveDragDelta(object sender, DragDeltaEventArgs e)
    {
        _session.MoveRoamingAreaBy(e.HorizontalChange, e.VerticalChange, persist: false);
        ApplyPlacement();
    }

    private void OnMoveDragCompleted(object sender, DragCompletedEventArgs e) => SaveEdit();

    private void OnResizeDragDelta(object sender, DragDeltaEventArgs e)
    {
        var handle = (Thumb)sender;
        _session.ResizeRoamingAreaBy(
            handle.HorizontalAlignment == System.Windows.HorizontalAlignment.Stretch ? 0 : e.HorizontalChange,
            handle.VerticalAlignment == System.Windows.VerticalAlignment.Stretch ? 0 : e.VerticalChange,
            fromLeft: handle.HorizontalAlignment == System.Windows.HorizontalAlignment.Left,
            fromTop: handle.VerticalAlignment == System.Windows.VerticalAlignment.Top,
            persist: false);
        ApplyPlacement();
    }

    private void OnResizeDragCompleted(object sender, DragCompletedEventArgs e) => SaveEdit();

    private void OnDoneClick(object sender, RoutedEventArgs e) => Close();

    private void SaveEdit()
    {
        try
        {
            _session.SaveCurrentState();
        }
        catch (SessionStateException exception)
        {
            System.Windows.MessageBox.Show(
                $"The area edit is active, but it was not saved. The previous saved state was retained.\n\n{exception.Message}",
                "Hood placement save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
