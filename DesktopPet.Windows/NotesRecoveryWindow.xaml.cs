using System.Windows;

namespace DesktopPet.Windows;

public partial class NotesRecoveryWindow : Window
{
    internal NotesRecoveryWindow(string notesPath, string errorMessage)
    {
        InitializeComponent();
        NotesPathText.Text = notesPath;
        ErrorText.Text = errorMessage;
    }

    private void OnAcknowledgeResetChecked(object sender, RoutedEventArgs e) =>
        ResetButton.IsEnabled = AcknowledgeResetCheckBox.IsChecked == true;

    private void OnKeepFileClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (AcknowledgeResetCheckBox.IsChecked == true)
        {
            DialogResult = true;
        }
    }
}
