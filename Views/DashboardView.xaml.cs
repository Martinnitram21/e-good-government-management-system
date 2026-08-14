using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            DataContext = App.AppHost?.Services.GetRequiredService<GoodGovernanceApp.ViewModels.DashboardViewModel>();
    }
}
