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
        // Size the non-activating overlay to the owner instead; this keeps the
        // complete application dimmed without triggering that invalid state.
        overlay.WindowState = WindowState.Normal;
        if (owner != null && owner.ActualWidth > 0 && owner.ActualHeight > 0)
        {
            overlay.Owner = owner;
            overlay.Left = owner.Left;
            overlay.Top = owner.Top;
            overlay.Width = owner.ActualWidth;
            overlay.Height = owner.ActualHeight;
        }
        else
        {
            overlay.Left = SystemParameters.WorkArea.Left;
            overlay.Top = SystemParameters.WorkArea.Top;
            overlay.Width = SystemParameters.WorkArea.Width;
            overlay.Height = SystemParameters.WorkArea.Height;
        }
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
