using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace ElecKit.Core
{
    /// <summary>Colours and button styles shared by every ElecKit window.</summary>
    internal static class Theme
    {
        public static readonly Brush HeaderBackground = Solid(0x0F, 0x2A, 0x4A);
        public static readonly Brush HeaderLabel = Solid(0x8F, 0xB4, 0xE8);
        public static readonly Brush FooterBackground = Solid(0xF5, 0xF7, 0xFA);
        public static readonly Brush Line = Solid(0xDD, 0xE2, 0xE9);
        public static readonly Brush Text = Solid(0x1B, 0x1F, 0x24);
        public static readonly Brush Muted = Solid(0x5B, 0x67, 0x78);
        public static readonly Brush Danger = Solid(0xC5, 0x30, 0x30);

        private static ResourceDictionary _styles;
        private static bool _stylesLoaded;

        private const string StylesXaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  <Style x:Key=""Primary"" TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""White""/>
    <Setter Property=""Background"" Value=""#FF1F6FEB""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""chrome"" Background=""{TemplateBinding Background}"" CornerRadius=""4"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""chrome"" Property=""Background"" Value=""#FF1A5FCC""/></Trigger>
            <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""chrome"" Property=""Background"" Value=""#FF154FA8""/></Trigger>
            <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""chrome"" Property=""Opacity"" Value=""0.45""/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key=""Secondary"" TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""#FF1B1F24""/>
    <Setter Property=""Background"" Value=""#FFE6EAF0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""chrome"" Background=""{TemplateBinding Background}"" CornerRadius=""4"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""chrome"" Property=""Background"" Value=""#FFD6DBE4""/></Trigger>
            <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""chrome"" Property=""Background"" Value=""#FFC5CBD6""/></Trigger>
            <Trigger Property=""IsEnabled"" Value=""False""><Setter TargetName=""chrome"" Property=""Opacity"" Value=""0.45""/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        /// <summary>The button style, or null (plain buttons) if the styles could not be loaded.</summary>
        public static Style ButtonStyle(bool primary)
        {
            if (!_stylesLoaded)
            {
                _stylesLoaded = true;
                try { _styles = (ResourceDictionary)XamlReader.Parse(StylesXaml); }
                catch (Exception ex) { Log.Error(ex); }
            }
            return _styles == null ? null : (Style)_styles[primary ? "Primary" : "Secondary"];
        }

        public static TextBlock Prompt(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460,
                Foreground = Text
            };
        }

        private static Brush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>The common frame: a title band, the content, and a button bar. Every ElecKit window uses it.</summary>
    internal sealed class DialogShell
    {
        private readonly StackPanel _buttons = new StackPanel();

        public Window Window { get; private set; }

        public DialogShell(string title, UIElement body, bool resizable, double width, double height)
        {
            Window = new Window
            {
                Title = "ElecKit - " + title,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true,
                Background = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };
            if (resizable)
            {
                Window.ResizeMode = ResizeMode.CanResize;
                Window.Width = width;
                Window.Height = height;
                Window.MinWidth = 520;
                Window.MinHeight = 340;
            }
            else
            {
                Window.ResizeMode = ResizeMode.NoResize;
                Window.SizeToContent = SizeToContent.WidthAndHeight;
            }

            var heading = new StackPanel();
            heading.Children.Add(new TextBlock
            {
                Text = "ELECKIT",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.HeaderLabel
            });
            heading.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 640
            });
            var header = new Border
            {
                Background = Theme.HeaderBackground,
                Padding = new Thickness(18, 12, 18, 12),
                Child = heading
            };

            _buttons.Orientation = Orientation.Horizontal;
            _buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var footer = new Border
            {
                Background = Theme.FooterBackground,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 10, 18, 12),
                Child = _buttons
            };

            var content = new Border
            {
                Padding = new Thickness(18, 14, 18, 14),
                MinWidth = resizable ? 0 : 460,
                Child = body
            };

            var root = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(header);
            root.Children.Add(footer);
            root.Children.Add(content);
            Window.Content = root;
        }

        public DialogShell(string title, UIElement body) : this(title, body, false, 0, 0) { }

        public Button AddButton(string text, bool primary, bool isDefault, bool isCancel, RoutedEventHandler onClick)
        {
            var button = new Button
            {
                Content = text,
                IsDefault = isDefault,
                IsCancel = isCancel,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(16, 6, 16, 6),
                MinWidth = 84
            };
            Style style = Theme.ButtonStyle(primary);
            if (style != null) button.Style = style;
            if (onClick != null) button.Click += onClick;
            _buttons.Children.Add(button);
            return button;
        }

        public bool? Show()
        {
            new WindowInteropHelper(Window).Owner = Process.GetCurrentProcess().MainWindowHandle;
            return Window.ShowDialog();
        }
    }
}
