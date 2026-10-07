using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using DesktopPet.Core;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace DesktopPet.Windows;

internal sealed class NoteLinkEditorDialog : Window
{
    private readonly WpfTextBox _labelInput;
    private readonly WpfTextBox _destinationInput;
    private readonly TextBlock _errorText;

    public string LinkLabel => _labelInput.Text;
    public string Destination => _destinationInput.Text;

    public NoteLinkEditorDialog(NotesConfiguration settings, NoteColor color, string uiStyle,
        string label, string destination)
    {
        Title = "Edit link";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        MinWidth = 360;
        MaxWidth = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        var darkTheme = NotesAppearance.Apply(this, settings.Theme, uiStyle);
        NotesAppearance.ApplyContent(this, darkTheme, color, uiStyle);
        SetResourceReference(BackgroundProperty, "NotesSurfaceBrush");
        SetResourceReference(ForegroundProperty, "NotesTextBrush");

        _labelInput = CreateInput("Link label", label);
        _destinationInput = CreateInput("Web address", destination);
        _errorText = new TextBlock
        {
            Foreground = (System.Windows.Media.Brush)FindResource("NotesErrorBrush"),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var cancel = new WpfButton { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var save = new WpfButton { Content = "Save", IsDefault = true };
        save.Click += (_, _) =>
        {
            if (TryAccept()) DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(CreateField("Label", _labelInput));
        content.Children.Add(CreateField("Address", _destinationInput));
        content.Children.Add(_errorText);
        content.Children.Add(buttons);
        Content = content;
    }

    internal bool TryAccept()
    {
        if (string.IsNullOrWhiteSpace(LinkLabel) || LinkLabel.Contains('\r') || LinkLabel.Contains('\n'))
        {
            ShowError("Enter a one-line link label.");
            return false;
        }
        if (!NoteLinkFormatting.IsValidDestination(Destination))
        {
            ShowError("Enter an absolute HTTP or HTTPS address.");
            return false;
        }

        _errorText.Text = string.Empty;
        _errorText.Visibility = Visibility.Collapsed;
        return true;
    }

    private WpfTextBox CreateInput(string name, string text)
    {
        var input = new WpfTextBox { Text = text, MinWidth = 320, Margin = new Thickness(0, 4, 0, 12) };
        AutomationProperties.SetName(input, name);
        return input;
    }

    private static StackPanel CreateField(string label, WpfTextBox input)
    {
        var text = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 2) };
        AutomationProperties.SetLabeledBy(input, text);
        var field = new StackPanel();
        field.Children.Add(text);
        field.Children.Add(input);
        return field;
    }

    private void ShowError(string text)
    {
        _errorText.Text = text;
        _errorText.Visibility = Visibility.Visible;
    }
}
