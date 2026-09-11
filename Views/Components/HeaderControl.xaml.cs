using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GoodGovernanceApp.Views.Components
{
    public partial class HeaderControl : UserControl
    {
        public HeaderControl()
        {
            InitializeComponent();
        }

        // Borderless-window drag handle. Ignores presses that start on buttons
        // and does nothing while maximized (restore via taskbar first).
        private void HeaderDragMove(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            DependencyObject? d = e.OriginalSource as DependencyObject;
            while (d != null)
            {
                if (d is System.Windows.Controls.Button) return;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            var win = Window.GetWindow(this);
            if (win == null || win.WindowState != WindowState.Normal) return;
            try { win.DragMove(); } catch { }
        }
    }
}
