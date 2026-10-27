using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ElecKit.Core
{
    /// <summary>Draws a coloured 32 px icon with the tool's initials. Colour depends on the panel.</summary>
    public static class IconFactory
    {
        private static readonly Dictionary<string, Color> PanelColors = new Dictionary<string, Color>
        {
            { "Tools", Color.FromRgb(0xC5, 0x30, 0x30) },
            { "Drafting", Color.FromRgb(0x2B, 0x6C, 0xB0) },
            { "Wiring", Color.FromRgb(0xC0, 0x56, 0x21) },
            { "Annotation", Color.FromRgb(0x6B, 0x46, 0xC1) },
            { "Model", Color.FromRgb(0x2F, 0x85, 0x5A) },
            { "Analysis", Color.FromRgb(0xB8, 0x32, 0x80) },
            { "Schedules", Color.FromRgb(0x2C, 0x7A, 0x7B) },
            { "Panels & SLD", Color.FromRgb(0x4A, 0x55, 0x68) },
            { "Cable & Tray", Color.FromRgb(0x97, 0x5A, 0x16) },
            { "Data", Color.FromRgb(0x31, 0x82, 0xCE) },
            { "Circuits", Color.FromRgb(0xB7, 0x79, 0x1F) },
            { "Settings", Color.FromRgb(0x71, 0x80, 0x96) }
        };

        private static readonly string[] SkipWords = { "to", "by", "of", "and", "from", "a", "the" };

        public static ImageSource For(ToolInfo tool)
        {
            Color color;
            if (!PanelColors.TryGetValue(tool.Panel, out color)) color = Color.FromRgb(0x55, 0x55, 0x55);
            return Create(Initials(tool.Title), color, 32);
        }

        /// <summary>16 px icon used beside the name when buttons are stacked.</summary>
        public static ImageSource Small(ToolInfo tool)
        {
            Color color;
            if (!PanelColors.TryGetValue(tool.Panel, out color)) color = Color.FromRgb(0x55, 0x55, 0x55);
            return Create(Initials(tool.Title), color, 16);
        }

        /// <summary>"CAD to\nDetail Lines" gives "CD"; a single word gives its first two letters.</summary>
        public static string Initials(string title)
        {
            var words = new List<string>();
            foreach (string word in title.Replace('\n', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Array.IndexOf(SkipWords, word.ToLowerInvariant()) < 0) words.Add(word);
            }
            if (words.Count == 0) return "?";
            if (words.Count == 1) return words[0].Length > 1 ? words[0].Substring(0, 2) : words[0];
            return words[0].Substring(0, 1).ToUpperInvariant() + words[1].Substring(0, 1).ToUpperInvariant();
        }

        /// <summary>Lighter (positive amount) or darker (negative) version of a colour.</summary>
        private static Color Shade(Color c, double amount)
        {
            Func<byte, byte> mix = v => (byte)Math.Max(0, Math.Min(255, amount >= 0 ? v + (255 - v) * amount : v * (1 + amount)));
            return Color.FromRgb(mix(c.R), mix(c.G), mix(c.B));
        }

        public static BitmapSource Create(string label, Color color, int size)
        {
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                var fill = new LinearGradientBrush(Shade(color, 0.22), Shade(color, -0.18), 90.0);
                var edge = new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1.0);
                context.DrawRoundedRectangle(fill, edge, new Rect(1, 1, size - 2, size - 2), size <= 16 ? 3 : 6, size <= 16 ? 3 : 6);
                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), size <= 16 ? size * 0.5 : size * 0.42, Brushes.White, 1.0);
                context.DrawText(text, new Point((size - text.Width) / 2.0, (size - text.Height) / 2.0));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
