using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}