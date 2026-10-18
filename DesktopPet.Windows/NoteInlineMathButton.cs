using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;
using WpfMath.Controls;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace DesktopPet.Windows;

internal sealed class NoteInlineMathButton : WpfButton
{
    private readonly TextBlock _errorIndicator;
    private ScrollViewer? _displayMathViewer;
    private double _displayMaxWidth = double.PositiveInfinity;
    private double _displayMaxHeight = double.PositiveInfinity;

    public NoteTextRun NoteRun { get; }
    public FormulaControl? FormulaControl { get; }
    public bool HasRenderError { get; private set; }
    public bool IsDisplayMath { get; }

    public NoteInlineMathButton(NoteTextRun noteRun, double fontSize, WpfBrush foreground, bool isDisplayMath = false)
    {
        NoteRun = noteRun;
        IsDisplayMath = isDisplayMath;
        Background = System.Windows.Media.Brushes.Transparent;
        BorderBrush = System.Windows.Media.Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        HorizontalContentAlignment = isDisplayMath ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Left;
        VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
        AutomationProperties.SetName(this, isDisplayMath ? "Edit display equation source" : "Edit inline equation source");
        AutomationProperties.SetHelpText(this, "Activate to edit the LaTeX source.");

        _errorIndicator = new TextBlock
        {
            Text = "!",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = foreground,
            Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        try
        {
            if (isDisplayMath && noteRun.Text.Length == 0)
            {
                HasRenderError = false;
            }
            else if (!TryGetExpression(noteRun.Text, isDisplayMath, out var expression))
            {
                HasRenderError = true;
            }
            else
            {
                FormulaControl = new FormulaControl
                {
                    Formula = isDisplayMath ? AddDisplayLineBreaks(expression) : expression,
                    Scale = fontSize,
                    Foreground = foreground,
                    HorizontalAlignment = isDisplayMath ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Focusable = false,
                    IsHitTestVisible = false
                };
                HasRenderError = FormulaControl.HasError;
            }
        }
        catch (Exception)
        {
            HasRenderError = true;
        }

        SetContent();
        SetErrorDescription();
    }

    public void UpdateAppearance(double fontSize, WpfBrush foreground,
        double displayMaxWidth = double.PositiveInfinity, double displayMaxHeight = double.PositiveInfinity)
    {
        try
        {
            _displayMaxWidth = displayMaxWidth;
            _displayMaxHeight = displayMaxHeight;
            if (FormulaControl is not null)
            {
                FormulaControl.Scale = fontSize;
                FormulaControl.Foreground = foreground;
                HasRenderError = FormulaControl.HasError;
            }
        }
        catch (Exception)
        {
            HasRenderError = true;
        }
        _errorIndicator.Foreground = foreground;
        SetContent();
        SetErrorDescription();
    }

    private void SetContent()
    {
        if (!HasRenderError && FormulaControl is not null)
        {
            if (!IsDisplayMath)
            {
                Content = FormulaControl;
                return;
            }

            _displayMathViewer ??= new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = false,
                Focusable = false
            };
            if (!ReferenceEquals(_displayMathViewer.Content, FormulaControl))
                _displayMathViewer.Content = FormulaControl;
            _displayMathViewer.MaxWidth = _displayMaxWidth;
            _displayMathViewer.MaxHeight = _displayMaxHeight;
            if (!ReferenceEquals(Content, _displayMathViewer))
                Content = _displayMathViewer;
            return;
        }

        if (IsDisplayMath && NoteRun.Text.Length == 0)
        {
            Content = new TextBlock
            {
                Text = "Click to enter equation",
                Foreground = _errorIndicator.Foreground,
                FontStyle = FontStyles.Italic,
                Opacity = 0.7
            };
            return;
        }

        var content = Content as StackPanel ?? new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };
        content.Children.Clear();
        if (FormulaControl is not null) content.Children.Add(FormulaControl);
        content.Children.Add(_errorIndicator);
        if (!ReferenceEquals(Content, content))
            Content = content;
    }

    private void SetErrorDescription()
    {
        ToolTip = HasRenderError
            ? "This equation could not be rendered. Activate it to edit the source."
            : "Activate to edit the LaTeX source.";
        AutomationProperties.SetHelpText(this, HasRenderError
            ? "Equation rendering failed. Activate to edit its LaTeX source."
            : "Activate to edit the LaTeX source.");
    }

    private static bool TryGetExpression(string source, bool isDisplayMath, out string expression)
    {
        expression = string.Empty;
        if (isDisplayMath)
        {
            if (TryRemoveDisplayDelimiters(source, out expression))
                return !string.IsNullOrWhiteSpace(expression);
            if (source.Length >= 2 && source[0] == '$' && source[^1] == '$' &&
                (source.Length == 2 || source[1] != '$'))
            {
                expression = source[1..^1];
                return !string.IsNullOrWhiteSpace(expression);
            }
            expression = source;
            return !string.IsNullOrWhiteSpace(expression);
        }

        if (source.Length < 3 || source[0] != '$' || source[^1] != '$' ||
            source[1] == '$' || source[^2] == '$')
            return false;

        expression = source[1..^1];
        return !string.IsNullOrWhiteSpace(expression);
    }

    private static string AddDisplayLineBreaks(string expression)
    {
        var groupDepth = 0;
        var environmentDepth = 0;
        // Keep newlines within LaTeX groups/environments as formatting whitespace.
        return Regex.Replace(expression, @"\\(?:begin|end)\{[^}]+\}|\\[^\r\n]|[{}]|\r\n|\r|\n", match =>
        {
            var token = match.Value;
            if (token.StartsWith(@"\begin{")) environmentDepth++;
            else if (token.StartsWith(@"\end{")) environmentDepth = Math.Max(0, environmentDepth - 1);
            else if (token == "{") groupDepth++;
            else if (token == "}") groupDepth = Math.Max(0, groupDepth - 1);
            else if (token is "\r\n" or "\r" or "\n" && groupDepth == 0 && environmentDepth == 0)
            {
                // An explicit LaTeX row break already ends this source line.
                var preceding = expression.AsSpan(0, match.Index).TrimEnd(" \t");
                if (!preceding.EndsWith(@"\\", StringComparison.Ordinal)) return @"\\";
            }
            return token;
        });
    }

    private static bool TryRemoveDisplayDelimiters(string source, out string expression)
    {
        expression = string.Empty;
        var opening = source.IndexOf("$$", StringComparison.Ordinal);
        var closing = source.LastIndexOf("$$", StringComparison.Ordinal);
        if (opening < 0 || closing <= opening || source[..opening].Trim().Length > 0 ||
            source[(closing + 2)..].Trim().Length > 0)
            return false;

        var contentStart = opening + 2;
        var contentEnd = closing;
        if (contentStart < contentEnd && source[contentStart] == '\r') contentStart++;
        if (contentStart < contentEnd && source[contentStart] == '\n') contentStart++;
        if (contentEnd > contentStart && source[contentEnd - 1] == '\n') contentEnd--;
        if (contentEnd > contentStart && source[contentEnd - 1] == '\r') contentEnd--;
        expression = source[contentStart..contentEnd];
        return true;
    }
}

internal sealed class NoteInlineMathSourceSpan : System.Windows.Documents.Span
{
    // WPF copies dependency properties when it splits a span during typing.
    public static readonly DependencyProperty IsDisplayMathProperty = DependencyProperty.Register(
        nameof(IsDisplayMath), typeof(bool), typeof(NoteInlineMathSourceSpan), new PropertyMetadata(false));

    public bool IsDisplayMath
    {
        get => (bool)GetValue(IsDisplayMathProperty);
        set => SetValue(IsDisplayMathProperty, value);
    }
}
