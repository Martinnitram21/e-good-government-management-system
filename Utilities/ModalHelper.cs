using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GoodGovernanceApp.Utilities;

/// <summary>
/// Shows an app modal dialog over a fullscreen dim backdrop that fills the
/// entire screen, so the modal stands out. Native OS file/print dialogs are
/// intentionally NOT routed through here (system windows).
/// </summary>
public static class ModalHelper
{
    public static bool? Show(Window dialog, Window? owner = null)
    {
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                  ?? Application.Current?.MainWindow;

        var overlay = new Window
        {
            Title = string.Empty,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Focusable = false,
            Opacity = 0
        };

        // A WPF window cannot be shown maximized while ShowActivated is false.
        // Use explicit virtual-screen bounds instead. Unlike owner.ActualWidth /
        // ActualHeight, these bounds are not clipped to the content area and do
        // not leave uncovered strips when Windows display scaling is enabled.
        overlay.WindowState = WindowState.Normal;
        if (owner != null)
            overlay.Owner = owner;

        overlay.Left = SystemParameters.VirtualScreenLeft;
        overlay.Top = SystemParameters.VirtualScreenTop;
        overlay.Width = SystemParameters.VirtualScreenWidth;
        overlay.Height = SystemParameters.VirtualScreenHeight;
        overlay.Show();

        overlay.BeginAnimation(Window.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180))));

        // Borderless themed chrome (min / max-restore / close + drag) on the modal.
        ModalChrome.Apply(dialog);

        dialog.Owner = overlay;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            try { overlay.Close(); } catch { }
        }
    }
}
