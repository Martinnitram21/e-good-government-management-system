using System.Windows;
using System.Windows.Controls;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.Views;

public partial class CrsBeneficiaryView : UserControl
{
    private bool _initialLoadStarted;

    public CrsBeneficiaryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadStarted || DataContext is not CrsBeneficiaryViewModel viewModel)
            return;

        _initialLoadStarted = true;
        try
        {
            await viewModel.LoadAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CRS] Initial load failed: {ex}");
        }
    }
}
