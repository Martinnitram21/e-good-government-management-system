using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.Views;

public partial class DatabaseSettingsWindow : Window
{
    public DatabaseSettingsWindow()
    {
        InitializeComponent();
        if (App.AppHost != null)
        {
            DataContext = App.AppHost.Services.GetRequiredService<SettingsViewModel>();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
