using System.Windows;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            var passwordBox = (System.Windows.Controls.PasswordBox)sender;
            viewModel.Password = passwordBox.Password;
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void PasswordVisibilityToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel viewModel || PasswordBoxControl == null)
            return;

        if (!viewModel.IsPasswordVisible && PasswordBoxControl.Password != viewModel.Password)
            PasswordBoxControl.Password = viewModel.Password ?? string.Empty;

        PasswordPlaceholder.Visibility = viewModel.IsPasswordVisible || !string.IsNullOrEmpty(viewModel.Password)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // Borderless-window chrome: drag handle + themed minimize/close.
    private void LoginDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        try { DragMove(); } catch { }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
