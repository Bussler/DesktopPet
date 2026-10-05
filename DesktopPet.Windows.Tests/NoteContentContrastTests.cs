using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteContentContrastTests
{
    [TestMethod]
    [DataRow("light", UiStyleCatalog.StickyNotes)]
    [DataRow("dark", UiStyleCatalog.StickyNotes)]
    [DataRow("light", UiStyleCatalog.Minimal)]
    [DataRow("dark", UiStyleCatalog.Minimal)]
    [DataRow("light", UiStyleCatalog.Soft)]
    [DataRow("dark", UiStyleCatalog.Soft)]
    [DataRow("light", UiStyleCatalog.Studio)]
    [DataRow("dark", UiStyleCatalog.Studio)]
    public void TablesAndCodeStayDistinctAcrossNoteColorsAndThemeChanges(string theme, string style)
    {
        var id = Guid.NewGuid();
        var document = new NotesDocument { Paragraphs = [
            new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("var answer = 42;")] },
            new NoteParagraph { Runs = [new NoteTextRun("inline", NoteTextStyle.Code)] },
            new NoteParagraph { Cell = new NoteTableCell(id, 0, 0), Runs = [new NoteTextRun("Header")] },
            new NoteParagraph { Cell = new NoteTableCell(id, 1, 0), Runs = [new NoteTextRun("Body")] }
        ] };
        var saves = 0;
        var window = new NoteWindow(new HoodNote { Document = document }, (_, _) => { saves++; return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = theme }, style);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var code = (Paragraph)editor.Document.Blocks.FirstBlock;
            var inline = ((Paragraph)editor.Document.Blocks.ElementAt(1)).Inlines.OfType<Run>().Single();
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var header = table.RowGroups[0].Rows[0].Cells[0];
            var body = table.RowGroups[0].Rows[1].Cells[0];
            var variants = new Dictionary<(string Theme, string Surface), HashSet<Color>>();
            var spacing = new Dictionary<string, (double Cell, double Code)>();
            // Reuse the same document to verify that existing content updates dynamically.
            foreach (var currentTheme in new[] { theme, theme == "light" ? "dark" : "light", theme })
            foreach (var currentStyle in UiStyleCatalog.Supported)
            foreach (var color in NoteColorPalette.Colors)
            {
                window.ApplyAppearance(new NotesConfiguration { Theme = currentTheme }, color, currentStyle);
                spacing[currentStyle] = (header.Padding.Left, code.Padding.Left);
                var noteFill = ((SolidColorBrush)window.Background).Color;
                var tableFill = Composite(table.Background, noteFill);
                var headerFill = Composite(header.Background, tableFill);
                var codeFill = Composite(code.Background, noteFill);
                var inlineFill = Composite(inline.Background, noteFill);
                foreach (var (name, fill) in new[] { ("code", codeFill), ("inline", inlineFill), ("table", tableFill),
                    ("header", headerFill), ("border", ((SolidColorBrush)table.BorderBrush).Color) })
                {
                    var key = (currentTheme, name);
                    if (!variants.TryGetValue(key, out var fills)) variants[key] = fills = [];
                    fills.Add(fill);
                }
                if (currentTheme == "light")
                    Assert.IsTrue(Contrast(inlineFill, noteFill) >= 1.5, $"Inline code blends into note: {currentStyle}/{color}.");
                var text = ((SolidColorBrush)editor.Foreground).Color;
                Assert.IsTrue(Contrast(headerFill, tableFill) >= 1.5, $"Header blends into rows: {currentTheme}/{currentStyle}/{color}.");
                Assert.IsTrue(Contrast(Composite(header.BorderBrush, headerFill), headerFill) >= 3,
                    $"Grid blends into header: {currentTheme}/{currentStyle}/{color}.");
                Assert.IsTrue(Contrast(Composite(body.BorderBrush, tableFill), tableFill) >= 3);
                Assert.IsTrue(header.BorderThickness.Bottom >= 1 && table.BorderThickness.Left >= 1);
                Assert.IsTrue(Contrast(Composite(table.BorderBrush, tableFill), tableFill) >= 3);
                Assert.IsTrue(code.BorderThickness.Left >= 1, "Code blocks need a visible outline.");
                Assert.IsTrue(Contrast(Composite(code.BorderBrush, codeFill), codeFill) >= 3);
                foreach (var fill in new[] { headerFill, tableFill, codeFill, inlineFill })
                    Assert.IsTrue(Contrast(text, fill) >= 4.5, $"Content text lost contrast: {currentTheme}/{currentStyle}/{color}.");
            }
            foreach (var variant in variants)
                Assert.AreEqual(NoteColorPalette.Colors.Count * UiStyleCatalog.Supported.Count, variant.Value.Count,
                    $"{variant.Key.Surface} must follow both the UI style and note color in {variant.Key.Theme} mode.");
            Assert.IsTrue(spacing[UiStyleCatalog.Minimal].Cell < spacing[UiStyleCatalog.StickyNotes].Cell);
            Assert.IsTrue(spacing[UiStyleCatalog.Soft].Cell > spacing[UiStyleCatalog.StickyNotes].Cell);
            Assert.IsTrue(spacing[UiStyleCatalog.Minimal].Code < spacing[UiStyleCatalog.StickyNotes].Code);
            Assert.IsTrue(spacing[UiStyleCatalog.Soft].Code > spacing[UiStyleCatalog.StickyNotes].Code);
            Assert.AreEqual(0, saves, "Appearance changes must not edit the note document.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static Color Composite(Brush? brush, Color background)
    {
        if (brush is not SolidColorBrush solid) return background;
        var alpha = solid.Color.A / 255.0 * solid.Opacity;
        return Color.FromRgb(Blend(solid.Color.R, background.R), Blend(solid.Color.G, background.G), Blend(solid.Color.B, background.B));
        byte Blend(byte foreground, byte behind) => (byte)Math.Round(foreground * alpha + behind * (1 - alpha));
    }

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    private static double Linear(byte value) => value / 255.0 <= .04045
        ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + .055) / 1.055, 2.4);
}
