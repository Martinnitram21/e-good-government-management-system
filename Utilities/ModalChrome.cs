using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace GoodGovernanceApp.Utilities;

/// <summary>
/// Wraps every app-owned dialog in one themed main container with a dedicated
/// title bar and custom minimize, maximize/restore, and close controls.
/// </summary>
public static class ModalChrome
{
    private const double TitleBarHeight = 42;

    private static readonly DependencyProperty IsAppliedProperty =
        DependencyProperty.RegisterAttached(
            "IsApplied",
            typeof(bool),
            typeof(ModalChrome),
            new PropertyMetadata(false));

    public static void Apply(Window dialog)
    {
        if ((bool)dialog.GetValue(IsAppliedProperty)) return;
        dialog.SetValue(IsAppliedProperty, true);

        if (dialog.Content is not UIElement originalContent) return;

        var surfaceBrush = dialog.Background ?? Brushes.White;

        // Preserve the designed content height after reserving a title bar.
        if (dialog.SizeToContent is not SizeToContent.Height and not SizeToContent.WidthAndHeight &&
            !double.IsNaN(dialog.Height) && dialog.Height > 0)
        {
            dialog.Height += TitleBarHeight;
        }

        dialog.Content = null;
        dialog.WindowStyle = WindowStyle.None;
        dialog.AllowsTransparency = true;
        dialog.Background = Brushes.Transparent;
        dialog.ShowInTaskbar = true;

        var frame = new Border
        {
            Background = surfaceBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleBarHeight) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        frame.Child = layout;

        var titleBar = new Border
        {
            Background = surfaceBrush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xDF, 0xA8)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        Grid.SetRow(titleBar, 0);
        layout.Children.Add(titleBar);

        var titleLayout = new Grid { Margin = new Thickness(16, 0, 6, 0) };
        titleLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.Child = titleLayout;

        titleLayout.Children.Add(new TextBlock
        {
            Text = dialog.Title,
            Foreground = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17)),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(controls, 1);
        titleLayout.Children.Add(controls);

        controls.Children.Add(MakeButton(PackIconKind.WindowMinimize, "Minimize", false,
            () => dialog.WindowState = WindowState.Minimized));

        var maximizeIcon = new PackIcon
        {
            Kind = PackIconKind.WindowMaximize,
            Foreground = Brushes.Black,
            Width = 14,
            Height = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var maximizeButton = MakeButton(maximizeIcon, "Maximize", false, () =>
        {
            dialog.WindowState = dialog.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        });
        controls.Children.Add(maximizeButton);

        controls.Children.Add(MakeButton(PackIconKind.Close, "Close", true, () =>
        {
            try { dialog.Close(); } catch { }
        }));

        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (controls.IsMouseOver || e.ChangedButton != MouseButton.Left) return;

            if (e.ClickCount == 2)
            {
                dialog.WindowState = dialog.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            if (dialog.WindowState == WindowState.Normal)
            {
                try { dialog.DragMove(); } catch { }
            }
        };

        Grid.SetRow(originalContent, 1);
        layout.Children.Add(originalContent);

        dialog.StateChanged += (_, _) =>
        {
            bool maximized = dialog.WindowState == WindowState.Maximized;
            maximizeIcon.Kind = maximized ? PackIconKind.WindowRestore : PackIconKind.WindowMaximize;
            maximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
            frame.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(14);
        };

        dialog.Content = frame;
    }

    private static Border MakeButton(PackIconKind kind, string tooltip, bool isClose, Action onClick)
    {
        var icon = new PackIcon
        {
            Kind = kind,
            Foreground = Brushes.Black,
            Width = 14,
            Height = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return MakeButton(icon, tooltip, isClose, onClick);
    }

    private static Border MakeButton(PackIcon icon, string tooltip, bool isClose, Action onClick)
    {
        var button = new Border
        {
            Width = 34,
            Height = 30,
            Margin = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(7),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Child = icon
        };

        button.MouseEnter += (_, _) =>
        {
            button.Background = isClose
                ? new SolidColorBrush(Color.FromRgb(0xD9, 0x00, 0x00))
                : new SolidColorBrush(Color.FromRgb(0xE5, 0xDF, 0xA8));
            if (isClose) icon.Foreground = Brushes.White;
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = Brushes.Transparent;
            icon.Foreground = Brushes.Black;
        };
        button.MouseLeftButtonUp += (_, _) => onClick();

        return button;
    }
}
